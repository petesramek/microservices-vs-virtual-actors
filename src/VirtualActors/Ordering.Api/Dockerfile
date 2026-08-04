FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Workbench/Workbench.Contracts/Workbench.Contracts.csproj src/Workbench/Workbench.Contracts/
COPY src/Workbench/Workbench.Gateway/Workbench.Gateway.csproj src/Workbench/Workbench.Gateway/
COPY src/Workbench/Workbench.Ui/Workbench.Ui.csproj src/Workbench/Workbench.Ui/
COPY src/Microservices/Orders.Api/Orders.Api.csproj src/Microservices/Orders.Api/
COPY src/Microservices/Inventory.Api/Inventory.Api.csproj src/Microservices/Inventory.Api/
COPY src/Microservices/Payments.Api/Payments.Api.csproj src/Microservices/Payments.Api/
COPY src/VirtualActors/Ordering.Api/Ordering.Api.csproj src/VirtualActors/Ordering.Api/
COPY src/VirtualActors/Ordering.Grains/Ordering.Grains.csproj src/VirtualActors/Ordering.Grains/
COPY src/VirtualActors/Ordering.Silo/Ordering.Silo.csproj src/VirtualActors/Ordering.Silo/
RUN dotnet restore src/VirtualActors/Ordering.Api/Ordering.Api.csproj

COPY src src
RUN dotnet publish src/VirtualActors/Ordering.Api/Ordering.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ordering.Api.dll"]
