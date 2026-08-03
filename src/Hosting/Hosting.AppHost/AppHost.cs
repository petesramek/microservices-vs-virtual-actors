internal static class Program {
    private static void Main(string[] args) {
        IDistributedApplicationBuilder builder =
            DistributedApplication.CreateBuilder(args);

        IResourceBuilder<ProjectResource> inventoryApi = builder
            .AddProject<Projects.Inventory_Api>("inventory-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Inventory API");

        IResourceBuilder<ProjectResource> paymentsApi = builder
            .AddProject<Projects.Payments_Api>("payments-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Payments API");

        IResourceBuilder<ProjectResource> ordersApi = builder
            .AddProject<Projects.Orders_Api>("orders-api")
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
            .WithUrlForEndpoint(
                "http",
                url => {
                    url.Url = "/dashboard";
                    url.DisplayText = "Orleans Dashboard";
                });

        IResourceBuilder<ProjectResource> orderingApi = builder
            .AddProject<Projects.Ordering_Api>("ordering-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Ordering API")
            .WaitFor(orderingSilo);

        IResourceBuilder<ProjectResource> comparisonGateway = builder
            .AddProject<Projects.Comparison_Gateway>("comparison-gateway")
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
