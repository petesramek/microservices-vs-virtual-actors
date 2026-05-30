Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$patterns = @(
    "add-phase*.ps1",
    "fix-*.ps1",
    "create-*.ps1",
    "*-template.zip",
    "*.db",
    "*.db-shm",
    "*.db-wal"
)

$found = @()
foreach ($pattern in $patterns) {
    $found += Get-ChildItem -Path . -Recurse -File -Filter $pattern | Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\" -and
        $_.FullName -notmatch "\\.git\\"
    }
}

if ($found.Count -eq 0) {
    Write-Host "Repository hygiene check passed. No generated scripts, template zips, or local database files found."
    exit 0
}

Write-Host "Repository hygiene check found files that should usually not be committed:"
foreach ($file in $found | Sort-Object FullName) {
    Write-Host " - $($file.FullName)"
}

exit 1
