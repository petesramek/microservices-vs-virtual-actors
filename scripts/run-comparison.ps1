Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting comparison gateway and Blazor Server UI."
Write-Host "Gateway -> http://localhost:5100"
Write-Host "UI      -> http://localhost:5000"

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Comparison/Comparison.Gateway --urls http://localhost:5100"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Comparison/Comparison.Ui --urls http://localhost:5000"
