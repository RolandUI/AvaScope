param(
    [string]$Configuration = "Release",
    [string]$ExecutableRoot = "artifacts/executables",
    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64"),
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$ExecutablePackageKind = "framework-dependent",
    [string]$InnoSetupCompilerPath,
    [string]$WindowsSignToolPath,
    [string[]]$WindowsSignToolArguments = @()
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

function Remove-InstallerItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot,
        [switch]$Recurse
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedPath -Directory $AllowedRoot)) {
        throw "Refusing to delete an installer path outside the artifact root: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Force -Recurse:$Recurse
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Resolve-InnoSetupCompiler {
    param([string]$ExplicitPath)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates += $ExplicitPath
    }
    if (-not [string]::IsNullOrWhiteSpace($env:INNO_SETUP_COMPILER)) {
        $candidates += $env:INNO_SETUP_COMPILER
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }

    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $candidates += @(
        (Join-Path $programFilesX86 "Inno Setup 7/ISCC.exe"),
        (Join-Path $programFilesX86 "Inno Setup 6/ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 7/ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe")
    )

    foreach ($candidate in $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Inno Setup compiler was not found. Install it with 'winget install --id JRSoftware.InnoSetup -e' or pass -InnoSetupCompilerPath."
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$executableRootPath = if ([System.IO.Path]::IsPathRooted($ExecutableRoot)) {
    [System.IO.Path]::GetFullPath($ExecutableRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ExecutableRoot))
}

if (-not (Test-IsUnderDirectory -Path $executableRootPath -Directory $repoRoot)) {
    throw "Executable root must stay inside the repository: $executableRootPath"
}

$installerProject = Join-Path $repoRoot "src/AvaScope.Installer/AvaScope.Installer.csproj"
$innoSetupScript = Join-Path $repoRoot "eng/installer/AvaScope.iss"
$buildRoot = Join-Path $executableRootPath ".installer-build"
New-Item -ItemType Directory -Path $executableRootPath -Force | Out-Null
Remove-InstallerItem -Path $buildRoot -AllowedRoot $executableRootPath -Recurse

Get-ChildItem -LiteralPath $executableRootPath -Filter "avascope-*-installer*" -File -ErrorAction SilentlyContinue |
    ForEach-Object {
        Remove-InstallerItem -Path $_.FullName -AllowedRoot $executableRootPath
    }
Remove-InstallerItem -Path (Join-Path $executableRootPath "AvaScopeSetup.exe") -AllowedRoot $executableRootPath

$createdInstallers = @()
try {
    foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
        if ($runtimeIdentifier -notmatch "^(win|linux)-[A-Za-z0-9_.-]+$") {
            throw "Installer runtime identifier must target Windows or Linux: $runtimeIdentifier"
        }

        $payloadPath = Join-Path $executableRootPath "avascope-$runtimeIdentifier-$ExecutablePackageKind.zip"
        if (-not (Test-Path -LiteralPath $payloadPath -PathType Leaf)) {
            throw "Installer payload does not exist: $payloadPath. Run eng/package-executables.ps1 first."
        }

        if ($runtimeIdentifier.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase)) {
            if (-not $runtimeIdentifier.Equals("win-x64", [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "The Windows wizard currently supports win-x64 only: $runtimeIdentifier"
            }

            $compilerPath = Resolve-InnoSetupCompiler -ExplicitPath $InnoSetupCompilerPath
            $payloadDirectory = Join-Path $buildRoot "$runtimeIdentifier-payload"
            $outputDirectory = Join-Path $buildRoot "$runtimeIdentifier-inno"
            New-Item -ItemType Directory -Path $payloadDirectory, $outputDirectory -Force | Out-Null
            Expand-Archive -LiteralPath $payloadPath -DestinationPath $payloadDirectory -Force

            [xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props")
            $version = [string]$buildProps.Project.PropertyGroup.Version
            & $compilerPath `
                "/Qp" `
                "/DAppVersion=$version" `
                "/DPayloadDir=$payloadDirectory" `
                "/DRepoRoot=$repoRoot" `
                "/O$outputDirectory" `
                "/FAvaScopeSetup" `
                $innoSetupScript
            if ($LASTEXITCODE -ne 0) {
                throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
            }

            $publishedInstaller = Join-Path $outputDirectory "AvaScopeSetup.exe"
            $installerPath = Join-Path $executableRootPath "AvaScopeSetup.exe"
        }
        else {
            $publishDirectory = Join-Path $buildRoot $runtimeIdentifier
            New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
            $installerSelfContained = if ($ExecutablePackageKind -eq "self-contained") { "true" } else { "false" }
            Invoke-DotNet -Arguments @(
                "publish",
                $installerProject,
                "-c",
                $Configuration,
                "-r",
                $runtimeIdentifier,
                "--self-contained",
                $installerSelfContained,
                "--output",
                $publishDirectory,
                "-p:PublishSingleFile=true",
                "-p:PublishTrimmed=false",
                "-p:SelfContained=$installerSelfContained",
                "-p:AvaScopeInstallerPayload=$payloadPath")

            $publishedInstaller = Join-Path $publishDirectory "avascope-installer"
            $installerPath = Join-Path $executableRootPath "avascope-$runtimeIdentifier-installer"
        }

        if (-not (Test-Path -LiteralPath $publishedInstaller -PathType Leaf)) {
            throw "Installer build did not produce the expected artifact: $publishedInstaller"
        }
        Copy-Item -LiteralPath $publishedInstaller -Destination $installerPath -Force

        if ($runtimeIdentifier.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase) -and
            -not [string]::IsNullOrWhiteSpace($WindowsSignToolPath)) {
            & $WindowsSignToolPath @WindowsSignToolArguments $installerPath
            if ($LASTEXITCODE -ne 0) {
                throw "Windows installer signing failed with exit code $LASTEXITCODE."
            }

            $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
            if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
                throw "Windows installer signature is not valid after signing: $($signature.Status)"
            }
        }

        $installer = Get-Item -LiteralPath $installerPath
        Write-Host "Created $runtimeIdentifier installer artifact: $($installer.FullName)"
        $createdInstallers += $installer
    }
}
finally {
    Remove-InstallerItem -Path $buildRoot -AllowedRoot $executableRootPath -Recurse
}

$createdInstallers
