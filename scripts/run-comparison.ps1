Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

$gatewayProject = Join-Path `
    $repositoryRoot `
    "src/Workbench/Workbench.Gateway/Workbench.Gateway.csproj"

$uiProject = Join-Path `
    $repositoryRoot `
    "src/Workbench/Workbench.Ui/Workbench.Ui.csproj"

$projects = @(
    $gatewayProject
    $uiProject
)

foreach ($projectPath in $projects) {
    if (-not (Test-Path $projectPath -PathType Leaf)) {
        throw "Project file was not found: $projectPath"
    }
}

Write-Host "Building the solution..."
dotnet build $repositoryRoot

if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed."
}

Write-Host "Starting comparison gateway and Blazor Server UI."
Write-Host "Gateway -> http://localhost:5100"
Write-Host "UI      -> http://localhost:5000"

Start-Process powershell.exe `
    -WorkingDirectory $repositoryRoot `
    -ArgumentList @(
        "-NoExit"
        "-Command"
        "dotnet run --no-build --project `"$gatewayProject`" --urls `"http://localhost:5100`""
    )

Start-Process powershell.exe `
    -WorkingDirectory $repositoryRoot `
    -ArgumentList @(
        "-NoExit"
        "-Command"
        "dotnet run --no-build --project `"$uiProject`" --urls `"http://localhost:5000`""
    )
