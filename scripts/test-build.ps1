Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot

try {
    Write-Host "Cleaning the solution..."
    dotnet clean

    if ($LASTEXITCODE -ne 0) {
        throw "Solution clean failed."
    }

    Write-Host "Restoring dependencies..."
    dotnet restore

    if ($LASTEXITCODE -ne 0) {
        throw "Solution restore failed."
    }

    Write-Host "Building the solution in Release configuration..."
    dotnet build `
        --configuration Release `
        --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed."
    }

    Write-Host "Running tests in Release configuration..."
    dotnet test `
        --configuration Release `
        --no-build `
        --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "Solution tests failed."
    }

    Write-Host "Build and tests completed successfully."
}
finally {
    Pop-Location
}
