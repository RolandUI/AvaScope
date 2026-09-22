param(
    [string]$Configuration = 'Release',
    [string]$OutputRoot = 'artifacts/providers'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$stage = [IO.Path]::GetFullPath((Join-Path $output 'avascope-bridge-provider'))
$archive = Join-Path $output 'avascope-bridge-provider.zip'
$comparison = if ($env:OS -eq 'Windows_NT') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, $comparison) -or
    -not $stage.StartsWith($output + [IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'Provider staging paths must stay under the repository artifacts directory.'
}
for ($ancestor = $output; $ancestor.Length -ge $artifactRoot.Length; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Provider staging ancestors cannot be symbolic links.'
    }
}

[xml]$props = Get-Content -Raw (Join-Path $repoRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
if (Test-Path -LiteralPath $stage) {
    if ((Get-Item -LiteralPath $stage).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Provider staging directory cannot be a symbolic link.'
    }
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& dotnet publish (Join-Path $repoRoot 'src/AvaScope.Bridge/AvaScope.Bridge.csproj') -c $Configuration -o $stage -p:AvaScopeStandaloneProvider=true --nologo
if ($LASTEXITCODE -ne 0) { throw 'Provider publish failed.' }

# Avalonia is supplied exclusively by the host. Preserve its type identity and native backend.
$sharedIdentities = [ordered]@{}
$shared = @(Get-ChildItem -LiteralPath $stage -Filter 'Avalonia*.dll' -File | Sort-Object Name | ForEach-Object {
    $identity = [Reflection.AssemblyName]::GetAssemblyName($_.FullName)
    $sharedIdentities[$identity.Name] = $identity.FullName
    $identity.Name
})
if ($shared -notcontains 'Avalonia.Base' -or $shared -notcontains 'Avalonia.Controls') {
    throw 'Provider publish did not contain the expected host-shared Avalonia dependency set.'
}
Get-ChildItem -LiteralPath $stage -Filter 'Avalonia*' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
foreach ($legal in @('LICENSE','NOTICE','LICENSE-SCOPE.md','THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $legal) -Destination (Join-Path $stage $legal)
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/STANDALONE_PROVIDER.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/examples/OptionalProviderLoader.cs') -Destination (Join-Path $stage 'OptionalProviderLoader.cs')

$files = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($stage.Length + 1).Replace('\','/')
        length = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{
    schemaVersion = 1
    providerVersion = $version
    dotnetMajor = 10
    avaloniaMinimumVersion = '12.1.0'
    avaloniaMaximumExclusiveVersion = '12.2.0'
    runtimeIdentifiers = @('win-x64','linux-x64','osx-arm64','osx-x64')
    bootstrapAssembly = 'AvaScope.Bridge.dll'
    bootstrapType = 'AvaScope.Bridge.Bootstrap'
    bootstrapMethod = 'Start'
    hostSharedAssemblies = $shared
    hostSharedAssemblyIdentities = $sharedIdentities
    deployment = 'framework-dependent, untrimmed, dynamic assembly loading'
    files = $files
}
$manifestPath = Join-Path $stage 'provider-manifest.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName) {
        # Windows PowerShell/.NET Framework otherwise emits backslashes in ZIP entry names.
        $entryName = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $zip.Dispose() }
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$archiveHash  avascope-bridge-provider.zip" + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'verify-provider.ps1') -Archive $archive -ExpectedVersion $version
Write-Output "Packaged provider $version with $($files.Count) verified files and $($shared.Count) host-shared assemblies."
Write-Output "Manifest SHA-256: $((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant())"
Write-Output $archive
