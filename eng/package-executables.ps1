param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/executables",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

function Test-IsUnderDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)

    return $fullPath.Equals($fullDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith(
            $fullDirectory + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}

if (-not (Test-IsUnderDirectory -Path $outputRootPath -Directory $repoRoot)) {
    throw "Output root must stay inside the repository: $outputRootPath"
}

$publishDirectory = Join-Path $outputRootPath "avascope"
$zipPath = Join-Path $outputRootPath "avascope-win-framework-dependent.zip"
$projectPath = Join-Path $repoRoot "src/AvaScope.Cli/AvaScope.Cli.csproj"

if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedPublishDirectory = (Resolve-Path -LiteralPath $publishDirectory).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedPublishDirectory -Directory $outputRootPath)) {
        throw "Refusing to delete a publish directory outside the repository: $resolvedPublishDirectory"
    }

    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$publishArguments = @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "--output",
    $publishDirectory
)

if ($NoBuild) {
    $publishArguments += "--no-build"
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$requiredFiles = @(
    "avascope.exe",
    "avascope.dll",
    "avascope.deps.json",
    "avascope.runtimeconfig.json",
    "AvaScope.Mcp.exe",
    "AvaScope.Mcp.dll",
    "AvaScope.Mcp.deps.json",
    "AvaScope.Mcp.runtimeconfig.json",
    "AvaScope.PreviewHost.exe",
    "AvaScope.PreviewHost.dll",
    "AvaScope.PreviewHost.deps.json",
    "AvaScope.PreviewHost.runtimeconfig.json",
    "AvaScope.Core.dll",
    "AvaScope.Protocol.dll"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $publishDirectory $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required executable artifact file was not produced: $path"
    }
}

if (Test-Path -LiteralPath $zipPath) {
    $resolvedZipPath = (Resolve-Path -LiteralPath $zipPath).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedZipPath -Directory $outputRootPath)) {
        throw "Refusing to replace a zip outside the output root: $resolvedZipPath"
    }

    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -Force

$zip = Get-Item -LiteralPath $zipPath
Write-Host "Published executable artifact directory: $publishDirectory"
Write-Host "Created executable artifact zip: $($zip.FullName)"
$zip
