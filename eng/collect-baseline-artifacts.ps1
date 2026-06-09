param(
    [Parameter(Mandatory = $true)]
    [string]$Report,

    [Parameter(Mandatory = $true)]
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-JsonPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Resolve-ArtifactPath {
    param(
        [AllowNull()]
        [object]$PathValue
    )

    if ($null -eq $PathValue) {
        return $null
    }

    $path = [string]$PathValue
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($path)) {
        return [System.IO.Path]::GetFullPath($path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $reportDirectory $path))
}

function Get-UniqueDestinationPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FileName)
    $extension = [System.IO.Path]::GetExtension($FileName)
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $Directory $FileName))
    $suffix = 1

    while (-not $usedDestinationPaths.Add($candidate)) {
        $candidateFileName = "{0}-{1}{2}" -f $baseName, $suffix.ToString("00"), $extension
        $candidate = [System.IO.Path]::GetFullPath((Join-Path $Directory $candidateFileName))
        $suffix++
    }

    return $candidate
}

function Copy-Artifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Kind,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [int]$EntryIndex = -1
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Baseline report references a missing $Kind artifact: $SourcePath"
    }

    $kindDirectory = Join-Path $artifactRoot $Kind
    New-Item -ItemType Directory -Force -Path $kindDirectory | Out-Null

    $fileName = [System.IO.Path]::GetFileName($SourcePath)
    $destinationPath = Get-UniqueDestinationPath -Directory $kindDirectory -FileName $fileName
    Copy-Item -LiteralPath $SourcePath -Destination $destinationPath -Force

    return [pscustomobject]@{
        kind = $Kind
        entryIndex = $EntryIndex
        sourcePath = [System.IO.Path]::GetFullPath($SourcePath)
        artifactPath = [System.IO.Path]::GetFullPath($destinationPath)
    }
}

$reportPath = (Resolve-Path -LiteralPath $Report).Path
$reportDirectory = Split-Path -Parent $reportPath
$artifactRoot = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$usedDestinationPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$reportPayload = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$entriesValue = Get-JsonPropertyValue -Object $reportPayload -Name "entries"
if ($null -eq $entriesValue) {
    throw "Baseline check report must contain an entries array."
}

$entries = @($entriesValue)
$reportArtifact = Copy-Artifact -Kind "report" -SourcePath $reportPath
$currentArtifacts = [System.Collections.Generic.List[object]]::new()
$diffArtifacts = [System.Collections.Generic.List[object]]::new()

for ($i = 0; $i -lt $entries.Count; $i++) {
    $entry = $entries[$i]
    $currentPath = Resolve-ArtifactPath (Get-JsonPropertyValue -Object $entry -Name "currentImagePath")
    $diffPath = Resolve-ArtifactPath (Get-JsonPropertyValue -Object $entry -Name "diffPath")

    if ($null -ne $currentPath) {
        $currentArtifacts.Add((Copy-Artifact -Kind "current" -SourcePath $currentPath -EntryIndex $i)) | Out-Null
    }

    if ($null -ne $diffPath) {
        $diffArtifacts.Add((Copy-Artifact -Kind "diff" -SourcePath $diffPath -EntryIndex $i)) | Out-Null
    }
}

$manifestPath = Join-Path $artifactRoot "artifact-manifest.json"
$artifactManifest = [ordered]@{
    sourceReportPath = $reportPath
    artifactRoot = $artifactRoot
    generatedAt = [DateTimeOffset]::UtcNow.ToString("O")
    report = $reportArtifact
    currentImages = @($currentArtifacts.ToArray())
    diffImages = @($diffArtifacts.ToArray())
}

$artifactManifest |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Collected AvaScope baseline artifacts."
Write-Host "Report: $($reportArtifact.artifactPath)"
Write-Host "Current images: $($currentArtifacts.Count)"
Write-Host "Diff images: $($diffArtifacts.Count)"
Write-Host "Artifact manifest: $manifestPath"
