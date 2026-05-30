Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting virtual actor-style backend."
Write-Host "Ordering.Api -> http://localhost:5300"

dotnet run --project src/VirtualActors/Ordering.Api --urls http://localhost:5300
