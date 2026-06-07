param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/executables",
    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64"),
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

$projectPath = Join-Path $repoRoot "src/AvaScope.Cli/AvaScope.Cli.csproj"

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

$legacyPublishDirectory = Join-Path $outputRootPath "avascope"
if (Test-Path -LiteralPath $legacyPublishDirectory) {
    $resolvedLegacyPublishDirectory = (Resolve-Path -LiteralPath $legacyPublishDirectory).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedLegacyPublishDirectory -Directory $outputRootPath)) {
        throw "Refusing to delete a legacy publish directory outside the output root: $resolvedLegacyPublishDirectory"
    }

    Remove-Item -LiteralPath $resolvedLegacyPublishDirectory -Recurse -Force
}

Get-ChildItem -LiteralPath $outputRootPath -Filter "avascope-*-framework-dependent.zip" -File | ForEach-Object {
    if (-not (Test-IsUnderDirectory -Path $_.FullName -Directory $outputRootPath)) {
        throw "Refusing to delete a zip outside the output root: $($_.FullName)"
    }

    Remove-Item -LiteralPath $_.FullName -Force
}

Get-ChildItem -LiteralPath $outputRootPath -Filter "avascope-*-framework-dependent" -Directory | ForEach-Object {
    if (-not (Test-IsUnderDirectory -Path $_.FullName -Directory $outputRootPath)) {
        throw "Refusing to delete a publish directory outside the output root: $($_.FullName)"
    }

    Remove-Item -LiteralPath $_.FullName -Recurse -Force
}

$createdZips = @()
foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        throw "Runtime identifier cannot be empty."
    }

    if ($runtimeIdentifier -notmatch "^[A-Za-z0-9_.-]+$") {
        throw "Runtime identifier contains unsupported characters: $runtimeIdentifier"
    }

    $artifactName = "avascope-$runtimeIdentifier-framework-dependent"
    $publishDirectory = Join-Path $outputRootPath $artifactName
    $zipPath = Join-Path $outputRootPath "$artifactName.zip"

    if (Test-Path -LiteralPath $publishDirectory) {
        $resolvedPublishDirectory = (Resolve-Path -LiteralPath $publishDirectory).Path
        if (-not (Test-IsUnderDirectory -Path $resolvedPublishDirectory -Directory $outputRootPath)) {
            throw "Refusing to delete a publish directory outside the output root: $resolvedPublishDirectory"
        }

        Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    $publishArguments = @(
        "publish",
        $projectPath,
        "-c",
        $Configuration,
        "-r",
        $runtimeIdentifier,
        "--self-contained",
        "false",
        "--output",
        $publishDirectory
    )

    if ($NoBuild) {
        $publishArguments += "--no-build"
    }

    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $runtimeIdentifier with exit code $LASTEXITCODE."
    }

    $appHostExtension = if ($runtimeIdentifier.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase)) {
        ".exe"
    } else {
        ""
    }

    $requiredFiles = @(
        "avascope$appHostExtension",
        "avascope.dll",
        "avascope.deps.json",
        "avascope.runtimeconfig.json",
        "AvaScope.Mcp$appHostExtension",
        "AvaScope.Mcp.dll",
        "AvaScope.Mcp.deps.json",
        "AvaScope.Mcp.runtimeconfig.json",
        "AvaScope.PreviewHost$appHostExtension",
        "AvaScope.PreviewHost.dll",
        "AvaScope.PreviewHost.deps.json",
        "AvaScope.PreviewHost.runtimeconfig.json",
        "AvaScope.Core.dll",
        "AvaScope.Protocol.dll"
    )

    foreach ($file in $requiredFiles) {
        $path = Join-Path $publishDirectory $file
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required executable artifact file was not produced for ${runtimeIdentifier}: $path"
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
    Write-Host "Published $runtimeIdentifier executable artifact directory: $publishDirectory"
    Write-Host "Created $runtimeIdentifier executable artifact zip: $($zip.FullName)"
    $createdZips += $zip
}

$createdZips
