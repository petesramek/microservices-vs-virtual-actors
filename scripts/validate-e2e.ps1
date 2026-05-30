param(
    [switch]$SkipDocker
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments
    )

    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FileName $($Arguments -join ' ')"
    }
}

Write-Host "Running clean build and tests..."
Invoke-NativeCommand dotnet clean
Invoke-NativeCommand dotnet restore
Invoke-NativeCommand dotnet build --configuration Release --no-restore
Invoke-NativeCommand dotnet test --configuration Release --no-build

if ($SkipDocker) {
    Write-Host "Skipping Docker Compose image build because -SkipDocker was specified."
    exit 0
}

$dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCommand) {
    Write-Host "Docker CLI was not found. Skipping Docker Compose image build."
    Write-Host "Run this step in CI or on a machine with Docker installed:"
    Write-Host "  docker compose -f deploy/docker-compose.full.yml build"
    exit 0
}

Write-Host "Checking Docker daemon availability..."
& docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker CLI exists, but Docker daemon is not reachable. Skipping Docker Compose image build."
    Write-Host "Run this step in CI or on a machine with Docker daemon access:"
    Write-Host "  docker compose -f deploy/docker-compose.full.yml build"
    exit 0
}

Write-Host "Building Docker Compose images..."
Invoke-NativeCommand docker compose -f deploy/docker-compose.full.yml build

Write-Host "Validation complete. To run the full stack:"
Write-Host "  docker compose -f deploy/docker-compose.full.yml up --build"
