param(
    [string]$ProjectPath = ".",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path $ProjectPath
$docsPath = Join-Path $root "docs"

if (-not (Test-Path $docsPath)) {
    throw "Docs folder was not found at '$docsPath'. Pass -ProjectPath with the repository root."
}

$canonicalDocs = @(
    "01-problem.md",
    "02-microservices-design.md",
    "03-virtual-actors-design.md",
    "04-development-comparison.md",
    "05-deployment-comparison.md",
    "06-scaling-comparison.md",
    "07-tradeoffs.md",
    "08-organizational-scaling-and-architecture-fit.md",
    "09-local-validation.md",
    "10-ui-dashboard.md",
    "11-end-to-end-validation.md",
    "12-scenario-guide.md",
    "13-correlation-id-logging.md",
    "14-release-versioning-and-rollback.md",
    "15-maintenance-and-evolution.md",
    "16-observability-and-operations.md",
    "17-known-limitations.md",
    "18-out-of-scope.md"
)

$renamedOrMergedDocs = [ordered]@{
    "08-local-validation.md" = "09-local-validation.md"
    "09-ui-dashboard.md" = "10-ui-dashboard.md"
    "10-end-to-end-validation.md" = "11-end-to-end-validation.md"
    "11-out-of-scope.md" = "18-out-of-scope.md"
    "12-hot-product-contention.md" = "12-scenario-guide.md"
    "13-payment-timeout-after-reservation.md" = "12-scenario-guide.md"
    "14-scenario-expected-results.md" = "12-scenario-guide.md"
    "15-correlation-id-logging.md" = "13-correlation-id-logging.md"
    "16-scenario-comparison-matrix.md" = "12-scenario-guide.md"
    "17-release-deployment-and-versioning.md" = "14-release-versioning-and-rollback.md"
    "18-maintenance-and-evolution.md" = "15-maintenance-and-evolution.md"
    "18-organizational-scaling-and-architecture-fit.md" = "08-organizational-scaling-and-architecture-fit.md"
    "19-observability-and-operations.md" = "16-observability-and-operations.md"
    "20-known-limitations.md" = "17-known-limitations.md"
}

function Get-LinkForTarget {
    param(
        [string]$CurrentFilePath,
        [string]$TargetFileName
    )

    $currentDirectory = Split-Path -Path $CurrentFilePath -Parent
    $docsFullPath = [System.IO.Path]::GetFullPath($script:docsPath)
    $currentDirectoryFullPath = [System.IO.Path]::GetFullPath($currentDirectory)

    if ($currentDirectoryFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) -ieq $docsFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)) {
        return $TargetFileName
    }

    return "docs/$TargetFileName"
}

function Replace-DocReferenceInLine {
    param(
        [string]$Line,
        [string]$CurrentFilePath,
        [string]$SourceFileName,
        [string]$TargetFileName
    )

    $currentName = Split-Path -Path $CurrentFilePath -Leaf
    if ($currentName -ieq $TargetFileName) {
        return $Line
    }

    $targetLink = Get-LinkForTarget -CurrentFilePath $CurrentFilePath -TargetFileName $TargetFileName
    $label = $TargetFileName

    # If the target already appears as a markdown link on this line, leave the line alone for this target.
    $escapedTarget = [regex]::Escape($TargetFileName)
    if ($Line -match "\[[^\]]*?$escapedTarget[^\]]*?\]\([^\)]*\)") {
        return $Line
    }

    $escapedSource = [regex]::Escape($SourceFileName)

    # Match docs/file.md, `docs/file.md`, file.md, or `file.md`, but avoid matching inside longer path/file tokens.
    $pattern = "(?<![A-Za-z0-9_./-])`?(?:docs/)?$escapedSource`?(?![A-Za-z0-9_./-])"

    return [regex]::Replace($Line, $pattern, "[$label]($targetLink)")
}

$files = @()
$readme = Join-Path $root "README.md"
if (Test-Path $readme) {
    $files += Get-Item $readme
}

$files += Get-ChildItem -Path $docsPath -Filter "*.md" -File | Sort-Object Name

$changedFiles = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $originalLines = Get-Content -Path $file.FullName
    $updatedLines = New-Object System.Collections.Generic.List[string]

    foreach ($line in $originalLines) {
        $updatedLine = $line

        foreach ($entry in $renamedOrMergedDocs.GetEnumerator()) {
            $updatedLine = Replace-DocReferenceInLine `
                -Line $updatedLine `
                -CurrentFilePath $file.FullName `
                -SourceFileName $entry.Key `
                -TargetFileName $entry.Value
        }

        foreach ($doc in $canonicalDocs) {
            $updatedLine = Replace-DocReferenceInLine `
                -Line $updatedLine `
                -CurrentFilePath $file.FullName `
                -SourceFileName $doc `
                -TargetFileName $doc
        }

        $updatedLines.Add($updatedLine)
    }

    $originalText = ($originalLines -join "`n")
    $updatedText = ($updatedLines -join "`n")

    if ($updatedText -ne $originalText) {
        $relativePath = Resolve-Path -Path $file.FullName -Relative
        $changedFiles.Add($relativePath)

        if ($DryRun) {
            Write-Host "DRY RUN: Would update $relativePath"
        }
        else {
            Set-Content -Path $file.FullName -Value $updatedLines -Encoding UTF8
            Write-Host "Updated $relativePath"
        }
    }
}

if ($changedFiles.Count -eq 0) {
    Write-Host "No documentation references needed updating."
}
else {
    Write-Host ""
    if ($DryRun) {
        Write-Host "Dry run complete. Files that would be updated:"
    }
    else {
        Write-Host "Documentation reference update complete. Updated files:"
    }

    $changedFiles | ForEach-Object { Write-Host "- $_" }
}

Write-Host ""
Write-Host "Recommended follow-up checks:"
Write-Host "Select-String -Path .\README.md,.\docs\*.md -Pattern '12-hot-product-contention|13-payment-timeout-after-reservation|14-scenario-expected-results|15-correlation-id-logging|16-scenario-comparison-matrix|17-release-deployment-and-versioning|18-maintenance-and-evolution|18-organizational-scaling-and-architecture-fit|19-observability-and-operations|20-known-limitations'"
Write-Host "Select-String -Path .\README.md,.\docs\*.md -Pattern 'docs/[0-9][0-9]-.*\.md|[0-9][0-9]-.*\.md'"
