FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/ArchitectureComparison.Contracts/ArchitectureComparison.Contracts.csproj src/Shared/ArchitectureComparison.Contracts/
COPY src/Comparison/Comparison.Gateway/Comparison.Gateway.csproj src/Comparison/Comparison.Gateway/
COPY src/Comparison/Comparison.Ui/Comparison.Ui.csproj src/Comparison/Comparison.Ui/
COPY src/Microservices/Orders.Api/Orders.Api.csproj src/Microservices/Orders.Api/
COPY src/Microservices/Inventory.Api/Inventory.Api.csproj src/Microservices/Inventory.Api/
COPY src/Microservices/Payments.Api/Payments.Api.csproj src/Microservices/Payments.Api/
COPY src/VirtualActors/Ordering.Api/Ordering.Api.csproj src/VirtualActors/Ordering.Api/
COPY src/VirtualActors/Ordering.Grains/Ordering.Grains.csproj src/VirtualActors/Ordering.Grains/
COPY src/VirtualActors/Ordering.Silo/Ordering.Silo.csproj src/VirtualActors/Ordering.Silo/
RUN dotnet restore src/Microservices/Inventory.Api/Inventory.Api.csproj

COPY src src
RUN dotnet publish src/Microservices/Inventory.Api/Inventory.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
