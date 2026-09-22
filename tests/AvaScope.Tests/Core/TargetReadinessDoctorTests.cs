using System.Diagnostics;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class TargetReadinessDoctorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"target-doctor-{Guid.NewGuid():N}");
    public TargetReadinessDoctorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task HealthyHeadlessProbeReturnsBoundedEvidenceAndExitsWithoutBridgeActivation()
    {
        var result = await new TargetReadinessDoctor().CheckAsync(new(Backend: "headless"));
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("available", result.Value!.Status);
        Assert.False(result.Value.BridgeActivated);
        Assert.Contains(result.Value.Checks, check => check.Name == "skia_renderer" && check.Status == "available");
        var cleanup = Assert.Single(result.Value.Checks, check => check.Name == "probe_cleanup");
        var processId = int.Parse(cleanup.Evidence!["processId"]);
        try { using var process = Process.GetProcessById(processId); Assert.True(process.HasExited); }
        catch (ArgumentException) { }
        Assert.True(result.Value.Checks.Count <= 64);
    }

    [Fact]
    public async Task TargetCompatibilityMissingDependenciesAndConflictingRootsAreDistinct()
    {
        var project = Path.Combine(_root, "Host.csproj");
        File.WriteAllText(project, "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Avalonia\" Version=\"11.3.0\" /></ItemGroup></Project>");
        var result = await new TargetReadinessDoctor().CheckAsync(new(ProjectPath: project, AssemblyPath: Path.Combine(_root, "missing.dll"),
            ProviderDirectory: Path.Combine(_root, "provider"), Backend: "headless", ExpectedInstallationRoot: _root));
        var errors = result.Value!.Checks.Where(check => check.Error is not null).Select(check => check.Error!.Code).ToArray();
        Assert.Contains("target_runtime_incompatible", errors);
        Assert.Contains("target_avalonia_incompatible", errors);
        Assert.Contains("target_assembly_missing", errors);
        Assert.Contains("target_installation_root_conflict", errors);
        Assert.Contains("AVASCOPE_PROVIDER_MANIFEST_INVALID", errors);
        Assert.False(result.Value.BridgeActivated);
    }

    [Fact]
    public async Task UnsupportedRequestedNativeOperationsAreExplicitAndMcpReusesDoctor()
    {
        var result = await AvaScopeMcpTools.DoctorTarget(new(Backend: "headless", NativeInput: true));
        Assert.True(result.Success);
        Assert.Equal("unavailable", result.Value!.Status);
        Assert.Contains(result.Value.Checks, check => check.Error?.Code == "target_native_operation_unsupported");
        Assert.DoesNotContain(result.Value.Checks, check => check.Name is "accessibility_permission" or "screen_capture_permission");
    }

    [Fact]
    public async Task InvalidSelectionsAndMalformedMetadataReturnStructuredFailures()
    {
        Assert.False((await new TargetReadinessDoctor().CheckAsync(new(ProjectPath: "relative.csproj"))).Success);
        Assert.False((await new TargetReadinessDoctor().CheckAsync(new(ProfileFile: Path.Combine(_root, "profile.json")))).Success);
        Assert.False((await new TargetReadinessDoctor().CheckAsync(new(TimeoutMs: 0))).Success);
        var assembly = Path.Combine(_root, "bad.dll");
        File.WriteAllText(assembly, "not an assembly");
        var result = await new TargetReadinessDoctor().CheckAsync(new(AssemblyPath: assembly, Backend: "headless"));
        Assert.Equal("unavailable", result.Value!.Status);
        Assert.Contains(result.Value.Checks, check => check.Error?.Code == "target_metadata_invalid");
        File.Copy(typeof(TargetReadinessDoctor).Assembly.Location, assembly, overwrite: true);
        File.WriteAllText(Path.ChangeExtension(assembly, ".runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":[]}}");
        var malformed = await new TargetReadinessDoctor().CheckAsync(new(AssemblyPath: assembly, Backend: "headless"));
        Assert.Contains(malformed.Value!.Checks, check => check.Error?.Code == "target_metadata_invalid");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
