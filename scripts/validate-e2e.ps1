param(
    [switch]$SkipDocker
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "deploy/docker-compose.full.yml"
$dockerDaemonTimeoutMilliseconds = 10000

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FileName @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FileName $($Arguments -join ' ')"
    }
}

function Test-DockerDaemon {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DockerPath,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DockerPath
    $startInfo.Arguments = "info"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    try {
        if (-not $process.Start()) {
            return $false
        }

        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            try {
                $process.Kill()
            }
            catch {
                # The process may have exited between the timeout and termination attempt.
            }

            Write-Host "Docker daemon check timed out after $($TimeoutMilliseconds / 1000) seconds."
            return $false
        }

        return $process.ExitCode -eq 0
    }
    finally {
        $process.Dispose()
    }
}

Push-Location $repositoryRoot

try {
    Write-Host "Running clean build and tests..."

    Invoke-NativeCommand dotnet clean
    Invoke-NativeCommand dotnet restore
    Invoke-NativeCommand dotnet build `
        --configuration Release `
        --no-restore
    Invoke-NativeCommand dotnet test `
        --configuration Release `
        --no-build `
        --no-restore

    if ($SkipDocker) {
        Write-Host "Skipping Docker Compose image build because -SkipDocker was specified."
        Write-Host "Validation completed successfully."
        return
    }

    if (-not (Test-Path $composeFile -PathType Leaf)) {
        throw "Docker Compose file was not found: $composeFile"
    }

    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue

    if (-not $dockerCommand) {
        Write-Host "Docker CLI was not found. Skipping Docker Compose image build."
        Write-Host "Run this step in CI or on a machine with Docker installed:"
        Write-Host "  docker compose -f `"$composeFile`" build"
        Write-Host "Validation completed without Docker image validation."
        return
    }

    Write-Host "Checking Docker daemon availability..."

    if (-not (Test-DockerDaemon `
        -DockerPath $dockerCommand.Source `
        -TimeoutMilliseconds $dockerDaemonTimeoutMilliseconds)) {
        Write-Host "Docker CLI exists, but the Docker daemon is not reachable."
        Write-Host "Skipping Docker Compose image build."
        Write-Host "Run this step in CI or on a machine with Docker daemon access:"
        Write-Host "  docker compose -f `"$composeFile`" build"
        Write-Host "Validation completed without Docker image validation."
        return
    }

    Write-Host "Building Docker Compose images..."
    Invoke-NativeCommand docker compose `
        --file $composeFile `
        build

    Write-Host "Validation completed successfully."
    Write-Host "To run the full stack:"
    Write-Host "  docker compose -f `"$composeFile`" up --build"
}
finally {
    Pop-Location
}
