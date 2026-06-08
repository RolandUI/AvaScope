[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $scriptDirectory = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    else {
        $PSScriptRoot
    }

    $Root = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
}

$Root = (Resolve-Path -LiteralPath $Root).Path.TrimEnd("\", "/")
$reportPaths = New-Object System.Collections.Generic.List[string]

$indexPaths = @(
    (Join-Path $Root "docs\BUG_REPORTS.md"),
    (Join-Path $Root "docs\FEATURE_REQUESTS.md")
)

foreach ($indexPath in $indexPaths) {
    if (Test-Path -LiteralPath $indexPath) {
        $reportPaths.Add((Resolve-Path -LiteralPath $indexPath).Path)
    }
}

$intakeDirectories = @(
    (Join-Path $Root "docs\bug-reports"),
    (Join-Path $Root "docs\feature-requests")
)

foreach ($intakeDirectory in $intakeDirectories) {
    if (Test-Path -LiteralPath $intakeDirectory) {
        Get-ChildItem -LiteralPath $intakeDirectory -Recurse -File -Filter "*.md" |
            Sort-Object FullName |
            ForEach-Object { $reportPaths.Add($_.FullName) }
    }
}

if ($reportPaths.Count -eq 0) {
    Write-Host "No intake files found."
    exit 0
}

$patterns = New-Object System.Collections.Generic.List[object]
$patterns.Add([pscustomobject]@{
    Name = "Windows absolute path"
    Pattern = '(?i)\b[A-Z]:[\\/][^\s`"<>|]+'
})
$patterns.Add([pscustomobject]@{
    Name = "UNC path"
    Pattern = '\\\\[^\\/\s]+[\\/][^\\/\s]+'
})
$patterns.Add([pscustomobject]@{
    Name = "Unix home path"
    Pattern = '(?i)/(Users|home)/[A-Za-z0-9._-]+'
})
$patterns.Add([pscustomobject]@{
    Name = "Email address"
    Pattern = '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b'
})
$patterns.Add([pscustomobject]@{
    Name = "Secret assignment"
    Pattern = '(?i)\b(api[_-]?key|password|secret|token)\s*[:=]\s*[^ \r\n]+'
})

if (-not [string]::IsNullOrWhiteSpace($env:USERNAME)) {
    $patterns.Add([pscustomobject]@{
        Name = "Current local username"
        Pattern = "(?i)\b$([regex]::Escape($env:USERNAME))\b"
    })
}

$violations = New-Object System.Collections.Generic.List[string]

foreach ($path in $reportPaths) {
    $relativePath = if ($path.StartsWith($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        $path.Substring($Root.Length).TrimStart("\", "/")
    }
    else {
        $path
    }

    $lines = Get-Content -LiteralPath $path

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        foreach ($pattern in $patterns) {
            if ($line -match $pattern.Pattern) {
                $lineNumber = $lineIndex + 1
                $violations.Add("${relativePath}:${lineNumber}: $($pattern.Name)")
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error "Intake privacy validation failed:"
    foreach ($violation in $violations) {
        Write-Error "  $violation"
    }

    exit 1
}

Write-Host "Intake privacy validation passed: $($reportPaths.Count) file(s) scanned."
