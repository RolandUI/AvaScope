#requires -Version 7.0
param([string]$Configuration = 'Release', [switch]$Native, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/onboarding-validation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cli = Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll"
$mcp = Join-Path $repoRoot "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll"
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$provider = Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider'
$feed = Join-Path $root 'feed'
$packages = Join-Path $root 'packages'
if (-not $SkipBuild) {
    & dotnet build (Join-Path $repoRoot 'AvaScope.slnx') -c $Configuration *> (Join-Path $root 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
    & (Join-Path $PSScriptRoot 'package-provider.ps1') -Configuration $Configuration *> (Join-Path $root 'provider.log')
}
foreach ($name in @('AvaScope.Protocol','AvaScope.Core','AvaScope.Bridge')) {
    & dotnet pack (Join-Path $repoRoot "src/$name/$name.csproj") -c $Configuration --no-build -o $feed *> (Join-Path $root "pack-$name.log")
    if ($LASTEXITCODE -ne 0) { throw "Local fixture package creation failed: $name" }
}

$projectText = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>InspectionHost</AssemblyName><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" Version="12.1.0" />
    <PackageReference Include="Avalonia.Headless" Version="12.1.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
  </ItemGroup>
</Project>
'@
$source = @'
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        var builder = AppBuilder.Configure<App>();
        if (args.Contains("--headless")) builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        else builder.UsePlatformDetect();
        builder.StartWithClassicDesktopLifetime(args);
    }
}
class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Window { Title = "Generated integration", Width = 400, Height = 200, Content = new TextBox { Name = "SafeField", Text = "Inspection ready" } };
        base.OnFrameworkInitializationCompleted();
    }
}
'@
$results = @()
foreach ($mode in @('standalone','package')) {
    $projectRoot = Join-Path $root $mode
    New-Item -ItemType Directory -Force -Path $projectRoot | Out-Null
    $project = Join-Path $projectRoot 'InspectionHost.csproj'
    $sourcePath = Join-Path $projectRoot 'App.cs'
    [IO.File]::WriteAllText($project, $projectText)
    [IO.File]::WriteAllText($sourcePath, $source)
    $guideJson = & dotnet $cli integration-guide --project $project
    if ($LASTEXITCODE -ne 0) { throw "Integration guidance failed: $guideJson" }
    $guide = ($guideJson -join [Environment]::NewLine | ConvertFrom-Json).value
    if ($guide.status -ne 'guidance_available') { throw "No actionable guidance: $guideJson" }
    [IO.File]::WriteAllText((Join-Path $projectRoot 'guidance.json'), ($guide | ConvertTo-Json -Depth 15))
    $projectSnippet = ($guide.guidance | Where-Object { $_.mode -eq $mode -and $_.location.path -eq $project }).snippet
    $sourceSnippet = ($guide.guidance | Where-Object { $_.mode -eq $mode -and $_.location.path -eq $sourcePath }).snippet
    # Apply only the concrete returned guidance in this owned disposable fixture.
    [IO.File]::WriteAllText($project, $projectText.Replace('</Project>', "$projectSnippet`n</Project>"))
    [IO.File]::WriteAllText($sourcePath, $source.Replace('base.OnFrameworkInitializationCompleted();', "$sourceSnippet`n        base.OnFrameworkInitializationCompleted();"))
    if ($mode -eq 'standalone') { Copy-Item -LiteralPath (Join-Path $provider 'OptionalProviderLoader.cs') -Destination $projectRoot }
    $repeat = (& dotnet $cli integration-guide --project $project | ConvertFrom-Json).value
    if ($repeat.status -ne 'already_integrated' -or $repeat.guidance.Count -ne 0) { throw 'Repeated analysis proposed duplicate integration.' }

    foreach ($variant in @('enabled','disabled')) {
        $output = Join-Path $projectRoot "output-$variant"
        $enabled = if ($variant -eq 'enabled') { 'true' } else { 'false' }
        & dotnet publish $project -c $Configuration -o $output --artifacts-path (Join-Path $root "build-$mode-$variant") --packages $packages "-p:RestoreAdditionalProjectSources=$feed" "-p:EnableUiInspection=$enabled" *> (Join-Path $projectRoot "publish-$variant.log")
        if ($LASTEXITCODE -ne 0) { throw "Generated $mode/$variant failed to build: $projectRoot/publish-$variant.log" }
        if (($mode -eq 'standalone' -or $variant -eq 'disabled') -and @(Get-ChildItem -LiteralPath $output -Recurse -Filter 'AvaScope.*.dll').Count -ne 0) {
            throw "Unexpected AvaScope dependency in $mode/$variant output."
        }
        $arguments = @((Join-Path $output 'InspectionHost.dll'))
        if (-not $Native) { $arguments += '--headless' }
        $launchEnvironment = @{}
        foreach ($name in @('DISPLAY','XAUTHORITY','WAYLAND_DISPLAY','XDG_RUNTIME_DIR','DBUS_SESSION_BUS_ADDRESS','LANG','LC_ALL')) {
            $value = [Environment]::GetEnvironmentVariable($name)
            if ($value) { $launchEnvironment[$name] = $value }
        }
        $request = @{
            launch = @{ command = 'dotnet'; argumentList = $arguments; timeoutMs = 20000; environment = $launchEnvironment }
            outputDirectory = (Join-Path $projectRoot "verification-$variant")
            bootstrapDisabled = ($variant -eq 'disabled')
        }
        if ($mode -eq 'standalone') { $request.providerDirectory = $provider }
        if ($variant -eq 'disabled') {
            $request.productionOutputDirectory = $output
            $request.observationMs = 1500
        }
        else { $request.safeInputTarget = @{ name = 'SafeField' }; $request.safeInputTargetDeclared = $true }
        $requestPath = Join-Path $projectRoot "$variant-request.json"
        [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Depth 15))
        $json = & dotnet $cli verify-integration --request $requestPath
        $exitCode = $LASTEXITCODE
        [IO.File]::WriteAllText((Join-Path $projectRoot "$variant-cli.json"), ($json -join [Environment]::NewLine))
        $response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
        if ($exitCode -ne 0 -or $response.status -ne 'passed') { throw "Generated $mode/$variant CLI verification failed: $json" }
        $results += @{ mode=$mode; variant=$variant; adapter='cli'; report=$response.reportPath }
        $mcpRequestPath = Join-Path $projectRoot "$variant-mcp-request.json"
        [IO.File]::WriteAllText($mcpRequestPath, (@{ request=$request } | ConvertTo-Json -Depth 15))
        $json = & dotnet $client $mcp $mcpRequestPath $projectRoot verify_integration
        $exitCode = $LASTEXITCODE
        [IO.File]::WriteAllText((Join-Path $projectRoot "$variant-mcp.json"), ($json -join [Environment]::NewLine))
        $response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
        if ($exitCode -ne 0 -or $response.status -ne 'passed') { throw "Generated $mode/$variant MCP verification failed: $json" }
        $results += @{ mode=$mode; variant=$variant; adapter='mcp'; report=$response.reportPath }
    }
}
[IO.File]::WriteAllText((Join-Path $root 'validation.json'), (@{success=$true; runs=$results; native=$Native.IsPresent} | ConvertTo-Json -Depth 15))
Write-Output "Generated both integration modes, built enabled/disabled outputs, rejected duplicate guidance and passed all 8 CLI/MCP lifecycle runs: $root"
