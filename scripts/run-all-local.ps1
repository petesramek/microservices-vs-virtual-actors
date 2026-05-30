Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting all local services for the comparison dashboard."
Write-Host "Inventory.Api        -> http://localhost:5201"
Write-Host "Payments.Api         -> http://localhost:5202"
Write-Host "Orders.Api           -> http://localhost:5200"
Write-Host "Ordering.Api         -> http://localhost:5300"
Write-Host "Comparison.Gateway   -> http://localhost:5100"
Write-Host "Comparison.Ui        -> http://localhost:5000"

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Inventory.Api"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Payments.Api"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Orders.Api"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/VirtualActors/Ordering.Api"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Comparison/Comparison.Gateway"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Comparison/Comparison.Ui"
