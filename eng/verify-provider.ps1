param(
    [string]$Archive = 'artifacts/providers/avascope-bridge-provider.zip',
    [string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($Archive)) { $Archive = Join-Path $repoRoot $Archive }
if (-not $ExpectedVersion) {
    [xml]$props = Get-Content -Raw (Join-Path $repoRoot 'Directory.Build.props')
    $ExpectedVersion = [string]$props.Project.PropertyGroup.Version
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    $entries = @{}
    foreach ($entry in $zip.Entries | Where-Object { -not $_.FullName.EndsWith('/') }) {
        if ($entries.ContainsKey($entry.FullName) -or $entry.FullName.Contains('\') -or
            [IO.Path]::IsPathRooted($entry.FullName) -or $entry.FullName.Split('/') -contains '..') {
            throw 'Provider ZIP has an unsafe or duplicate entry.'
        }
        $entries[$entry.FullName] = $entry
    }
    if (-not $entries.ContainsKey('provider-manifest.json')) { throw 'Provider manifest is missing.' }
    $reader = [IO.StreamReader]::new($entries['provider-manifest.json'].Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($manifest.schemaVersion -ne 1 -or $manifest.providerVersion -ne $ExpectedVersion -or
        $manifest.bootstrapAssembly -ne 'AvaScope.Bridge.dll' -or $manifest.bootstrapType -ne 'AvaScope.Bridge.Bootstrap' -or
        $manifest.bootstrapMethod -ne 'Start' -or $manifest.dotnetMajor -ne 10 -or
        $manifest.avaloniaMinimumVersion -ne '12.1.0' -or $manifest.avaloniaMaximumExclusiveVersion -ne '12.2.0') {
        throw 'Provider compatibility metadata or product version is incorrect.'
    }
    if ($entries.Count -ne @($manifest.files).Count + 1) { throw 'Provider inventory does not cover every ZIP file.' }
    $seen = @{}
    foreach ($file in $manifest.files) {
        if ($seen.ContainsKey($file.path) -or -not $entries.ContainsKey($file.path)) { throw 'Provider dependency inventory is incomplete or duplicated.' }
        $seen[$file.path] = $true
        $entry = $entries[$file.path]
        $stream = $entry.Open()
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
        finally { $algorithm.Dispose(); $stream.Dispose() }
        if ($entry.Length -ne $file.length -or $hash -ne $file.sha256) { throw "Provider dependency is corrupt: $($file.path)" }
    }
    foreach ($name in @('AvaScope.Bridge.dll','AvaScope.Bridge.deps.json','AvaScope.Core.dll','AvaScope.Protocol.dll','SkiaSharp.dll',
        'runtimes/linux-x64/native/libSkiaSharp.so','runtimes/osx/native/libSkiaSharp.dylib','runtimes/win-x64/native/libSkiaSharp.dll',
        'OptionalProviderLoader.cs','README.md','LICENSE','NOTICE','LICENSE-SCOPE.md','THIRD-PARTY-NOTICES.md')) {
        if (-not $seen.ContainsKey($name)) { throw "Required provider dependency is missing: $name" }
    }
    if (@($entries.Keys | Where-Object { $_ -like 'Avalonia*.dll' }).Count -ne 0) { throw 'Host-shared Avalonia assemblies must not be shipped as private dependencies.' }
    foreach ($name in @('Avalonia.Base','Avalonia.Controls')) {
        if ($manifest.hostSharedAssemblies -notcontains $name) { throw "Host-shared dependency is undeclared: $name" }
    }
    foreach ($name in $manifest.hostSharedAssemblies) {
        if (-not $manifest.hostSharedAssemblyIdentities.$name) { throw "Host-shared assembly version is undeclared: $name" }
    }
}
finally { $zip.Dispose() }
$archiveHash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = (Get-Content -Raw -LiteralPath "$Archive.sha256").Trim()
if ($checksum -ne "$archiveHash  $([IO.Path]::GetFileName($Archive))") { throw 'Provider ZIP checksum sidecar does not match.' }
Write-Output "Verified standalone provider $ExpectedVersion, $($manifest.files.Count) dependencies, and ZIP SHA-256."
