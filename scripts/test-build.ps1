Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet clean
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
