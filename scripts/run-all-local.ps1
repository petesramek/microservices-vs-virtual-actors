Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Building the solution..."
dotnet build $repositoryRoot

if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed."
}

Write-Host "Starting all local services for the comparison dashboard."
Write-Host "Inventory.Api        -> http://localhost:5201"
Write-Host "Payments.Api         -> http://localhost:5202"
Write-Host "Orders.Api           -> http://localhost:5200"
Write-Host "Ordering.Api         -> http://localhost:5300"
Write-Host "Comparison.Gateway   -> http://localhost:5100"
Write-Host "Comparison.Ui        -> http://localhost:5000"

$projects = @(
    "src/Microservices/Inventory.Api/Inventory.Api.csproj"
    "src/Microservices/Payments.Api/Payments.Api.csproj"
    "src/Microservices/Orders.Api/Orders.Api.csproj"
    "src/VirtualActors/Ordering.Api/Ordering.Api.csproj"
    "src/Workbench/Workbench.Gateway/Workbench.Gateway.csproj"
    "src/Workbench/Workbench.Ui/Workbench.Ui.csproj"
)

foreach ($project in $projects) {
    $projectPath = Join-Path $repositoryRoot $project

    if (-not (Test-Path $projectPath -PathType Leaf)) {
        throw "Project file was not found: $projectPath"
    }

    Start-Process powershell.exe `
        -WorkingDirectory $repositoryRoot `
        -ArgumentList @(
            "-NoExit"
            "-Command"
            "dotnet run --no-build --project `"$projectPath`""
        )
}
