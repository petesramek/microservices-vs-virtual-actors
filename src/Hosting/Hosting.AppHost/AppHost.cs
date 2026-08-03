internal static class Program {
    private static void Main(string[] args) {
        IDistributedApplicationBuilder builder =
            DistributedApplication.CreateBuilder(args);

        IResourceBuilder<DashboardGroupResource> comparison =
            builder.AddResource(new DashboardGroupResource("comparison"));

        IResourceBuilder<DashboardGroupResource> microservices =
            builder.AddResource(new DashboardGroupResource("microservices"));

        IResourceBuilder<DashboardGroupResource> virtualActors =
            builder.AddResource(new DashboardGroupResource("virtual-actors"));

        IResourceBuilder<ProjectResource> inventoryApi = builder
            .AddProject<Projects.Inventory_Api>("inventory-api")
            .WithParentRelationship(microservices)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Inventory API");

        IResourceBuilder<ProjectResource> paymentsApi = builder
            .AddProject<Projects.Payments_Api>("payments-api")
            .WithParentRelationship(microservices)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Payments API");

        IResourceBuilder<ProjectResource> ordersApi = builder
            .AddProject<Projects.Orders_Api>("orders-api")
            .WithParentRelationship(microservices)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Orders API")
            // Override Services:InventoryBaseUrl with the Aspire-managed endpoint.
            .WithEnvironment(
                "Services__InventoryBaseUrl",
                inventoryApi.GetEndpoint("http"))
            // Override Services:PaymentsBaseUrl with the Aspire-managed endpoint.
            .WithEnvironment(
                "Services__PaymentsBaseUrl",
                paymentsApi.GetEndpoint("http"))
            .WaitFor(inventoryApi)
            .WaitFor(paymentsApi);

        IResourceBuilder<ProjectResource> orderingSilo = builder
            .AddProject<Projects.Ordering_Silo>("ordering-silo")
            .WithParentRelationship(virtualActors)
            .WithUrlForEndpoint(
                "http",
                url => {
                    url.Url = "/dashboard";
                    url.DisplayText = "Orleans Dashboard";
                });

        IResourceBuilder<ProjectResource> orderingApi = builder
            .AddProject<Projects.Ordering_Api>("ordering-api")
            .WithParentRelationship(virtualActors)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Ordering API")
            .WaitFor(orderingSilo);

        IResourceBuilder<ProjectResource> comparisonGateway = builder
            .AddProject<Projects.Comparison_Gateway>("comparison-gateway")
            .WithParentRelationship(comparison)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Comparison Gateway")
            // Override ServiceEndpoints:MicroservicesBaseUrl with Orders.Api.
            .WithEnvironment(
                "ServiceEndpoints__MicroservicesBaseUrl",
                ordersApi.GetEndpoint("http"))
            // Override ServiceEndpoints:VirtualActorsBaseUrl with Ordering.Api.
            .WithEnvironment(
                "ServiceEndpoints__VirtualActorsBaseUrl",
                orderingApi.GetEndpoint("http"));

        builder
            .AddProject<Projects.Comparison_Ui>("comparison-ui")
            .WithParentRelationship(comparison)
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Comparison UI")
            // Override Gateway:BaseUrl with the Aspire-managed Gateway endpoint.
            .WithEnvironment(
                "Gateway__BaseUrl",
                comparisonGateway.GetEndpoint("http"))
            .WaitFor(comparisonGateway);

        builder.Build().Run();
    }
}

internal sealed class DashboardGroupResource(string name)
    : Resource(name);
