using Hosting.AppHost.Observability.Topology;

internal static class Program {
    private static void Main(string[] args) {
        IDistributedApplicationBuilder builder =
            DistributedApplication.CreateBuilder(args);

        IResourceBuilder<ProjectResource> inventoryApi = builder
            .AddProject<Projects.Inventory_Api>("inventory-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Inventory API")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http");

        IResourceBuilder<ProjectResource> paymentsApi = builder
            .AddProject<Projects.Payments_Api>("payments-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Payments API")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http");

        IResourceBuilder<ProjectResource> ordersApi = builder
            .AddProject<Projects.Orders_Api>("orders-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Orders API")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http")
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
                })
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http");

        IResourceBuilder<ProjectResource> orderingApi = builder
            .AddProject<Projects.Ordering_Api>("ordering-api")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Ordering API")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http")
            .WaitFor(orderingSilo);

        IResourceBuilder<ProjectResource> workbenchGateway = builder
            .AddProject<Projects.Workbench_Gateway>("workbench-gateway")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Workbench Gateway")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http")
            // Override ServiceEndpoints:MicroservicesBaseUrl with Orders.Api.
            .WithEnvironment(
                "ServiceEndpoints__MicroservicesBaseUrl",
                ordersApi.GetEndpoint("http"))
            // Override ServiceEndpoints:VirtualActorsBaseUrl with Ordering.Api.
            .WithEnvironment(
                "ServiceEndpoints__VirtualActorsBaseUrl",
                orderingApi.GetEndpoint("http"));

        IResourceBuilder<ProjectResource> workbenchUi = builder
            .AddProject<Projects.Workbench_Ui>("workbench-ui")
            .WithUrlForEndpoint(
                "http",
                url => url.DisplayText = "Workbench UI")
            .WithHttpHealthCheck(
                path: "/health",
                endpointName: "http")
            // Override Gateway:BaseUrl with the Aspire-managed Gateway endpoint.
            .WithEnvironment(
                "Gateway__BaseUrl",
                workbenchGateway.GetEndpoint("http"))
            .WaitFor(workbenchGateway);

        builder.AddTopology(
            "Workbench UI",
            workbenchUi,
            topology => {
                topology.AddGroup(
                    "workbench",
                    "Workbench",
                    group => {
                        group.AddService(
                            workbenchGateway,
                            "Workbench Gateway");
                    });

                topology.AddGroup(
                    "microservices",
                    "Microservices",
                    group => {
                        group.AddService(
                            ordersApi,
                            "Orders API",
                            orders => {
                                orders.AddStorage(
                                    "orders-database",
                                    "Orders Database");

                                orders.AddService(
                                    inventoryApi,
                                    "Inventory API",
                                    inventory => {
                                        inventory.AddStorage(
                                            "inventory-database",
                                            "Inventory Database");
                                    });

                                orders.AddService(
                                    paymentsApi,
                                    "Payments API",
                                    payments => {
                                        payments.AddStorage(
                                            "payments-database",
                                            "Payments Database");
                                    });
                            });
                    });

                topology.AddGroup(
                    "virtual-actors",
                    "Virtual Actors",
                    group => {
                        group.AddService(
                            orderingApi,
                            "Ordering API",
                            ordering => {
                                ordering.AddService(
                                    orderingSilo,
                                    "Ordering Silo",
                                    silo => {
                                        silo.AddStorage(
                                            "ordering-database",
                                            "Ordering Database");
                                    });
                            });
                    });
        });

        builder.Build().Run();
    }
}

