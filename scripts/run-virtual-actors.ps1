Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

$services = @(
    @{
        Name = "Ordering.Api"
        Project = "src/VirtualActors/Ordering.Api/Ordering.Api.csproj"
        Url = "http://localhost:5300"
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

Write-Host "Starting Virtual Actors implementation."

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
