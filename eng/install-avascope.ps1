param(
    [string]$SourcePath,
    [string]$InstallRoot = $(if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Join-Path $HOME ".avascope"
    } else {
        Join-Path $env:LOCALAPPDATA "AvaScope"
    }),
    [switch]$SkipPathUpdate
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "AvaScope installer currently supports Windows only."
}

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

function Remove-InstallItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$InstallRootPath
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedPath -Directory $InstallRootPath)) {
        throw "Refusing to delete a path outside the install root: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Resolve-SourcePath {
    param(
        [string]$ConfiguredSourcePath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredSourcePath)) {
        return [System.IO.Path]::GetFullPath($ConfiguredSourcePath)
    }

    $installerPayload = Join-Path $PSScriptRoot "payload"
    if (Test-Path -LiteralPath $installerPayload) {
        return [System.IO.Path]::GetFullPath($installerPayload)
    }

    $repoArtifact = Join-Path $PSScriptRoot "..\artifacts\executables\avascope-win-x64-framework-dependent"
    return [System.IO.Path]::GetFullPath($repoArtifact)
}

function Get-PayloadDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedSourcePath,
        [Parameter(Mandatory = $true)]
        [string]$ExtractionRoot
    )

    if (Test-Path -LiteralPath $ResolvedSourcePath -PathType Leaf) {
        if ([System.IO.Path]::GetExtension($ResolvedSourcePath) -ne ".zip") {
            throw "SourcePath must be a publish directory or a .zip file: $ResolvedSourcePath"
        }

        New-Item -ItemType Directory -Path $ExtractionRoot -Force | Out-Null
        Expand-Archive -LiteralPath $ResolvedSourcePath -DestinationPath $ExtractionRoot -Force
        $ResolvedSourcePath = $ExtractionRoot
    }

    if (-not (Test-Path -LiteralPath $ResolvedSourcePath -PathType Container)) {
        throw "AvaScope executable package was not found: $ResolvedSourcePath"
    }

    $directExe = Join-Path $ResolvedSourcePath "avascope.exe"
    if (Test-Path -LiteralPath $directExe -PathType Leaf) {
        return $ResolvedSourcePath
    }

    $matchingDirectories = @(Get-ChildItem -LiteralPath $ResolvedSourcePath -Directory -Recurse |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "avascope.exe") -PathType Leaf })
    if ($matchingDirectories.Count -eq 1) {
        return $matchingDirectories[0].FullName
    }

    throw "Could not locate avascope.exe in the executable package: $ResolvedSourcePath"
}

function Add-UserPathEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $entries = @()
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $entries = @($userPath.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries))
    }

    $alreadyPresent = $entries | Where-Object {
        [System.IO.Path]::GetFullPath($_).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar).Equals(
                $fullDirectory,
                [System.StringComparison]::OrdinalIgnoreCase)
    }

    if (-not $alreadyPresent) {
        $newPath = if ([string]::IsNullOrWhiteSpace($userPath)) {
            $fullDirectory
        } else {
            "$userPath;$fullDirectory"
        }
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    }

    $processEntries = @($env:Path.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries))
    $processHasEntry = $processEntries | Where-Object {
        [System.IO.Path]::GetFullPath($_).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar).Equals(
                $fullDirectory,
                [System.StringComparison]::OrdinalIgnoreCase)
    }
    if (-not $processHasEntry) {
        $env:Path = "$fullDirectory;$env:Path"
    }
}

$resolvedInstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$resolvedSourcePath = Resolve-SourcePath -ConfiguredSourcePath $SourcePath
$currentDirectory = Join-Path $resolvedInstallRoot "current"
$binDirectory = Join-Path $resolvedInstallRoot "bin"
$manifestPath = Join-Path $resolvedInstallRoot "avascope.discovery.json"
$tempDirectory = Join-Path $resolvedInstallRoot ".installing"
$extractionRoot = Join-Path $tempDirectory "source"
$stagingDirectory = Join-Path $tempDirectory "current"

New-Item -ItemType Directory -Path $resolvedInstallRoot -Force | Out-Null
Remove-InstallItem -Path $tempDirectory -InstallRootPath $resolvedInstallRoot
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

try {
    $payloadDirectory = Get-PayloadDirectory -ResolvedSourcePath $resolvedSourcePath -ExtractionRoot $extractionRoot
    foreach ($item in Get-ChildItem -LiteralPath $payloadDirectory -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $stagingDirectory -Recurse -Force
    }

    $stagedExe = Join-Path $stagingDirectory "avascope.exe"
    $stagedMcp = Join-Path $stagingDirectory "AvaScope.Mcp.dll"
    $stagedPreviewHost = Join-Path $stagingDirectory "AvaScope.PreviewHost.dll"
    foreach ($requiredPath in @($stagedExe, $stagedMcp, $stagedPreviewHost)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required AvaScope install file is missing from the package: $requiredPath"
        }
    }

    Remove-InstallItem -Path $currentDirectory -InstallRootPath $resolvedInstallRoot
    Move-Item -LiteralPath $stagingDirectory -Destination $currentDirectory
}
finally {
    Remove-InstallItem -Path $tempDirectory -InstallRootPath $resolvedInstallRoot
}

New-Item -ItemType Directory -Path $binDirectory -Force | Out-Null
$cmdShimPath = Join-Path $binDirectory "avascope.cmd"
$legacyPsShimPath = Join-Path $binDirectory "avascope.ps1"
Remove-InstallItem -Path $legacyPsShimPath -InstallRootPath $resolvedInstallRoot
Set-Content -LiteralPath $cmdShimPath -Encoding ascii -Value @"
@echo off
setlocal
"%~dp0..\current\avascope.exe" %*
exit /b %ERRORLEVEL%
"@

$installedExe = Join-Path $currentDirectory "avascope.exe"
$versionOutput = & $installedExe --version
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($versionOutput)) {
    throw "Installed avascope.exe did not report a version."
}

$version = ($versionOutput | Select-Object -First 1).Trim()
$installedAt = [DateTimeOffset]::UtcNow.ToString("O", [System.Globalization.CultureInfo]::InvariantCulture)
$manifest = [ordered]@{
    schemaVersion = 1
    product = "AvaScope"
    serviceName = "avascope"
    version = $version
    installMode = "per-user"
    installedAt = $installedAt
    installRoot = $resolvedInstallRoot
    installPath = $currentDirectory
    shimDirectory = $binDirectory
    commandPath = $cmdShimPath
    executablePath = $installedExe
    mcp = [ordered]@{
        transport = "stdio"
        serverName = "avascope"
        commandPath = $cmdShimPath
        arguments = @("mcp")
        assemblyPath = (Join-Path $currentDirectory "AvaScope.Mcp.dll")
    }
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if (-not $SkipPathUpdate) {
    Add-UserPathEntry -Directory $binDirectory
}

Write-Host "Installed AvaScope $version."
Write-Host "Command shim: $cmdShimPath"
Write-Host "Discovery manifest: $manifestPath"
if ($SkipPathUpdate) {
    Write-Host "PATH update skipped."
} else {
    Write-Host "User PATH includes: $binDirectory"
}
