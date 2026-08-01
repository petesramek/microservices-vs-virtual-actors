Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

$services = @(
    @{
        Name = "Inventory.Api"
        Project = "src/Microservices/Inventory.Api/Inventory.Api.csproj"
        Url = "http://localhost:5201"
    }
    @{
        Name = "Payments.Api"
        Project = "src/Microservices/Payments.Api/Payments.Api.csproj"
        Url = "http://localhost:5202"
    }
    @{
        Name = "Orders.Api"
        Project = "src/Microservices/Orders.Api/Orders.Api.csproj"
        Url = "http://localhost:5200"
    }
)

foreach ($service in $services) {
    $projectPath = Join-Path $repositoryRoot $service.Project

    if (-not (Test-Path $projectPath -PathType Leaf)) {
        throw "Project file was not found: $projectPath"
    }

    $service.ProjectPath = $projectPath
}

Write-Host "Building the solution..."
dotnet build $repositoryRoot

if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed."
}

Write-Host "Starting Microservices implementation."

foreach ($service in $services) {
    Write-Host "$($service.Name) -> $($service.Url)"

    Start-Process powershell.exe `
        -WorkingDirectory $repositoryRoot `
        -ArgumentList @(
            "-NoExit"
            "-Command"
            "dotnet run --no-build --project `"$($service.ProjectPath)`" --urls `"$($service.Url)`""
        )
}
