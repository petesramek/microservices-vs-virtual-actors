Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting microservice-style backend. Open three terminals manually if you need independent logs."
Write-Host "Inventory.Api -> http://localhost:5201"
Write-Host "Payments.Api  -> http://localhost:5202"
Write-Host "Orders.Api    -> http://localhost:5200"

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Inventory.Api --urls http://localhost:5201"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Payments.Api --urls http://localhost:5202"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "dotnet run --project src/Microservices/Orders.Api --urls http://localhost:5200"
