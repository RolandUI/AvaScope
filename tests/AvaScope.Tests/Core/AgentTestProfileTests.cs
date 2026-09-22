using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Core;
using AvaScope.Mcp;

namespace AvaScope.Tests.Core;

public sealed class AgentTestProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"test-profiles-{Guid.NewGuid():N}");
    private string ProfilePath => Path.Combine(_root, "profiles.json");
    private const string Profile = """
        {"schemaVersion":1,"profiles":{"smoke":{
          "scenario":{
            "launch":{"command":"dotnet","argumentList":["{profileDir}/host.dll"],"timeoutMs":1000},
            "steps":[{"action":"wait","waitMs":1}],"outputDirectory":"evidence/{runId}",
            "build":{"projectPath":"Host.csproj","configuration":"Release"}},
          "platforms":{
            "windows":{"scenario":{"launch":{"argumentList":["{profileDir}/windows.dll"]}}},
            "linux":{"scenario":{"launch":{"timeoutMs":2000},"build":null}},
            "macos":{"scenario":{"launch":{"timeoutMs":3000}}}}
        }}}
        """;

    public AgentTestProfileTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(ProfilePath, Profile);
    }

    [Fact]
    public void PlatformPrecedenceArrayReplacementNullRemovalAndRelativePathsAreDeterministic()
    {
        var windows = AgentTestProfiles.Resolve(ProfilePath, "smoke", "windows");
        Assert.True(windows.Success, windows.Error?.Message);
        var scenario = windows.Value!.Scenario;
        Assert.Equal(1000, scenario.GetProperty("launch").GetProperty("timeoutMs").GetInt32());
        Assert.Equal(_root + "/windows.dll", scenario.GetProperty("launch").GetProperty("argumentList")[0].GetString());
        Assert.Equal(Path.Combine(_root, "Host.csproj"), scenario.GetProperty("build").GetProperty("projectPath").GetString());
        Assert.Equal(Path.Combine(_root, "evidence", "{runId}"), scenario.GetProperty("outputDirectory").GetString());
        Assert.Equal(64, windows.Value.ProfileSha256.Length);
        Assert.Equal(JsonSerializer.Serialize(windows.Value), JsonSerializer.Serialize(AgentTestProfiles.Resolve(ProfilePath, "smoke", "windows").Value));
        var linux = AgentTestProfiles.Resolve(ProfilePath, "smoke", "linux").Value!.Scenario;
        Assert.False(linux.TryGetProperty("build", out _));
        Assert.Equal(2000, linux.GetProperty("launch").GetProperty("timeoutMs").GetInt32());
        Assert.Equal(3000, AgentTestProfiles.Resolve(ProfilePath, "smoke", "macos").Value!.Scenario.GetProperty("launch").GetProperty("timeoutMs").GetInt32());
        Assert.Single(Directory.GetFiles(_root));
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public void EnvironmentReferencesAreRedactedAndNeverPersistedDuringResolution()
    {
        var name = "AVASCOPE_TEST_PROFILE_" + Guid.NewGuid().ToString("N");
        const string secret = "profile-secret-12345";
        Environment.SetEnvironmentVariable(name, secret);
        try
        {
            var profile = JsonNode.Parse(Profile)!.AsObject();
            profile["profiles"]!["smoke"]!["environmentReferences"] = new JsonObject
            {
                ["launch"] = new JsonObject { ["TOKEN"] = new JsonObject { ["name"] = name } }
            };
            File.WriteAllText(ProfilePath, profile.ToJsonString());
            var resolved = AgentTestProfiles.Resolve(ProfilePath, "smoke");
            Assert.True(resolved.Success, resolved.Error?.Message);
            Assert.DoesNotContain(secret, JsonSerializer.Serialize(resolved.Value), StringComparison.Ordinal);
            Assert.Equal("[REDACTED]", resolved.Value!.Scenario.GetProperty("launch").GetProperty("environment").GetProperty("TOKEN").GetString());
            Assert.Equal(JsonSerializer.Serialize(resolved.Value), JsonSerializer.Serialize(AvaScopeMcpTools.ResolveTestProfile(ProfilePath, "smoke").Value));
            Assert.DoesNotContain(secret, File.ReadAllText(ProfilePath), StringComparison.Ordinal);
        }
        finally { Environment.SetEnvironmentVariable(name, null); }
    }

    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2", "test_profile_version_unsupported")]
    [InlineData("\"timeoutMs\":1000", "\"unknownTimeout\":1000", "test_profile_invalid")]
    [InlineData("\"command\":\"dotnet\"", "\"environment\":{\"SECRET\":\"literal\"},\"command\":\"dotnet\"", "test_profile_invalid")]
    [InlineData("\"windows\":", "\"unknown-os\":", "test_profile_invalid")]
    public void InvalidVersionAndFieldsFailBeforeLaunch(string oldValue, string newValue, string code)
    {
        File.WriteAllText(ProfilePath, Profile.Replace(oldValue, newValue, StringComparison.Ordinal));
        var result = AgentTestProfiles.Resolve(ProfilePath, "smoke", "windows");
        Assert.False(result.Success);
        Assert.Equal(code, result.Error!.Code);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public void MissingRequiredEnvironmentAndIncompatibleProviderHaveClearFailures()
    {
        var profile = JsonNode.Parse(Profile)!;
        profile["profiles"]!["smoke"]!["environmentReferences"] = new JsonObject
        {
            ["launch"] = new JsonObject { ["TOKEN"] = new JsonObject { ["name"] = "UNSET_" + Guid.NewGuid().ToString("N") } }
        };
        File.WriteAllText(ProfilePath, profile.ToJsonString());
        Assert.Contains("unavailable", AgentTestProfiles.Resolve(ProfilePath, "smoke").Error!.Message, StringComparison.Ordinal);
        profile["profiles"]!["smoke"]!.AsObject().Remove("environmentReferences");
        profile["profiles"]!["smoke"]!["provider"] = new JsonObject { ["directory"] = "missing-provider" };
        File.WriteAllText(ProfilePath, profile.ToJsonString());
        Assert.Equal("AVASCOPE_PROVIDER_MANIFEST_INVALID", AgentTestProfiles.Resolve(ProfilePath, "smoke").Error!.Code);
    }

    [Fact]
    public async Task ProfileAndExplicitRequestSelectionCannotBeMixed()
    {
        var result = await AvaScopeMcpTools.RunScenario(new LocalBridgeClient(), profileFile: ProfilePath);
        Assert.False(result.Success);
        Assert.Equal("test_profile_selection_invalid", result.Error!.Code);
        Assert.False(AgentTestProfiles.Resolve(ProfilePath, "smoke", "unsupported").Success);
        Assert.False(AgentTestProfiles.Resolve("profiles.json", "smoke").Success);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
