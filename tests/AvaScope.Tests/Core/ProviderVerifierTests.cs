using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using OptionalDiagnostics;

namespace AvaScope.Tests.Core;

public sealed class ProviderVerifierTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"provider-{Guid.NewGuid():N}");
    private readonly JsonObject _manifest;

    public ProviderVerifierTests()
    {
        Directory.CreateDirectory(_directory);
        foreach (var assembly in new[] { typeof(Bootstrap).Assembly, typeof(ProviderVerifier).Assembly, typeof(AvaScopeProduct).Assembly })
        {
            File.Copy(assembly.Location, Path.Combine(_directory, Path.GetFileName(assembly.Location)));
        }

        File.WriteAllText(Path.Combine(_directory, "AvaScope.Bridge.deps.json"), "{}");
        _manifest = new JsonObject
        {
            ["schemaVersion"] = 1, ["providerVersion"] = AvaScopeProduct.Version, ["dotnetMajor"] = 10,
            ["avaloniaMinimumVersion"] = "12.1.0", ["avaloniaMaximumExclusiveVersion"] = "12.2.0",
            ["runtimeIdentifiers"] = new JsonArray("win-x64", "linux-x64", "osx-arm64", "osx-x64"),
            ["bootstrapAssembly"] = "AvaScope.Bridge.dll", ["bootstrapType"] = "AvaScope.Bridge.Bootstrap", ["bootstrapMethod"] = "Start",
            ["hostSharedAssemblies"] = new JsonArray("Avalonia.Base", "Avalonia.Controls"),
            ["hostSharedAssemblyIdentities"] = new JsonObject
            {
                ["Avalonia.Base"] = typeof(Avalonia.AvaloniaObject).Assembly.FullName,
                ["Avalonia.Controls"] = typeof(Avalonia.Controls.Window).Assembly.FullName
            },
            ["files"] = new JsonArray(Directory.GetFiles(_directory).Select(path => (JsonNode)new JsonObject
            {
                ["path"] = Path.GetFileName(path), ["length"] = new FileInfo(path).Length,
                ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
            }).ToArray())
        };
        WriteManifest();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void VerifiesExactPinsWithoutActivatingAndMcpPreservesIdentity()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(_directory, "provider-manifest.json"))));
        var core = ProviderVerifier.Verify(_directory, AvaScopeProduct.Version, hash);
        var mcp = AvaScopeMcpTools.VerifyProvider(_directory, AvaScopeProduct.Version, hash);
        Assert.True(core.Success, core.Error?.Message);
        Assert.True(mcp.Success, mcp.Error?.Message);
        Assert.Equal(core.Value, mcp.Value);
        Assert.False(core.Value!.Activated);
        Assert.Equal("checked_by_host_at_activation", core.Value.HostCompatibility);
        Assert.Equal(hash, core.Value.ManifestSha256);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("tamper")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("traversal")]
    [InlineData("runtime")]
    [InlineData("avalonia")]
    [InlineData("assembly-version")]
    public void RejectsInvalidProvidersBeforeActivation(string kind)
    {
        switch (kind)
        {
            case "tamper": File.AppendAllText(Path.Combine(_directory, "AvaScope.Bridge.dll"), "tampered"); break;
            case "missing": File.Delete(Path.Combine(_directory, "AvaScope.Core.dll")); break;
            case "duplicate": _manifest["files"]!.AsArray().Add(_manifest["files"]![0]!.DeepClone()); break;
            case "traversal": _manifest["files"]![0]!["path"] = "../outside.dll"; break;
            case "runtime": _manifest["dotnetMajor"] = 11; break;
            case "avalonia": _manifest["avaloniaMinimumVersion"] = "13.0.0"; break;
            case "assembly-version": _manifest["providerVersion"] = "99.0.0"; break;
        }

        WriteManifest();
        var result = ProviderVerifier.Verify(_directory,
            expectedVersion: kind == "version" ? "99.0.0" : null,
            expectedManifestSha256: kind == "hash" ? new string('0', 64) : null);
        Assert.False(result.Success);
        Assert.StartsWith("AVASCOPE_", result.Error!.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void UnsetOptionalPathIsDisabledWithoutProviderResolution()
    {
        var result = OptionalProviderLoader.TryStartFromEnvironment("UNSET_PROVIDER_" + Guid.NewGuid().ToString("N"));
        Assert.True(result.Success);
        Assert.False(result.Activated);
        Assert.Equal("AVASCOPE_PROVIDER_NOT_CONFIGURED", result.Code);
    }

    [Fact]
    public void RefusesASecondBridgeWhenHostAlreadyLoadedPackageIntegration()
    {
        var result = OptionalProviderLoader.TryStart(_directory);
        Assert.False(result.Success);
        Assert.False(result.Activated);
        Assert.Equal("AVASCOPE_PROVIDER_ALREADY_LOADED", result.Code);
    }

    private void WriteManifest() => File.WriteAllText(Path.Combine(_directory, "provider-manifest.json"), _manifest.ToJsonString());
}
