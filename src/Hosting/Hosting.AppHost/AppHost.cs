namespace Hosting.AppHost;

using global::Observability.Health;
using global::Observability.Health.Abstraction;
using Hosting.AppHost.Extensions;
using Hosting.AppHost.Observability.Topology;

/// <summary>
/// Defines and runs the distributed application model for the Workbench host.
/// </summary>
/// <remarks>
/// Resource registration, service wiring, and topology publication are kept in
/// separate methods so that changes to one concern do not obscure the others.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name")]
internal static class Program {
    /// <summary>
    /// Builds the distributed application model and runs the AppHost.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments forwarded to the distributed application
    /// builder.
    /// </param>
    private static void Main(string[] args) {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

        HealthStatusEvaluator healthStatusEvaluator = new();

        Microservices microservices = AddMicroservices(builder);
        VirtualActorServices virtualActors = AddVirtualActorServices(builder);
        WorkbenchServices workbench = AddWorkbenchServices(
            builder,
            microservices.OrdersApi,
            virtualActors.OrderingApi);

        AddObservabilityTopology(
            builder,
            workbench,
            microservices,
            virtualActors,
            healthStatusEvaluator);

        builder.Build().Run();
    }

    /// <summary>
    /// Registers the order-processing microservices and their runtime
    /// dependencies.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder that receives the project
    /// resources.
    /// </param>
    /// <returns>The registered microservice resources.</returns>
    private static Microservices AddMicroservices(
        IDistributedApplicationBuilder builder) {
        IResourceBuilder<ProjectResource> inventoryApi = builder
            .AddProject<Projects.Inventory_Api>(ResourceNames.InventoryApi)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Inventory API")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            .WithObservabilityConfiguration(builder.Configuration);

        IResourceBuilder<ProjectResource> paymentsApi = builder
            .AddProject<Projects.Payments_Api>(ResourceNames.PaymentsApi)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Payments API")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            .WithObservabilityConfiguration(builder.Configuration);

        IResourceBuilder<ProjectResource> ordersApi = builder
            .AddProject<Projects.Orders_Api>(ResourceNames.OrdersApi)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Orders API")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            // Replace configured service URLs with Aspire-managed endpoints.
            .WithEnvironment(
                ConfigurationKeys.InventoryBaseUrl,
                inventoryApi.GetEndpoint(Endpoints.HttpProtocol))
            .WithEnvironment(
                ConfigurationKeys.PaymentsBaseUrl,
                paymentsApi.GetEndpoint(Endpoints.HttpProtocol))
            .WithObservabilityConfiguration(builder.Configuration)
            .WaitFor(inventoryApi)
            .WaitFor(paymentsApi);

        return new Microservices(
            ordersApi,
            inventoryApi,
            paymentsApi);
    }

    /// <summary>
    /// Registers the Orleans-based ordering API and silo.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder that receives the project
    /// resources.
    /// </param>
    /// <returns>The registered virtual-actor resources.</returns>
    private static VirtualActorServices AddVirtualActorServices(
        IDistributedApplicationBuilder builder) {
        IResourceBuilder<ProjectResource> orderingSilo = builder
            .AddProject<Projects.Ordering_Silo>(ResourceNames.OrderingSilo)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => {
                    url.Url = Endpoints.OrleansDashboardPath;
                    url.DisplayText = "Orleans Dashboard";
                })
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            .WithObservabilityConfiguration(builder.Configuration);

        IResourceBuilder<ProjectResource> orderingApi = builder
            .AddProject<Projects.Ordering_Api>(ResourceNames.OrderingApi)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Ordering API")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            .WaitFor(orderingSilo)
            .WithObservabilityConfiguration(builder.Configuration);

        return new VirtualActorServices(
            orderingApi,
            orderingSilo);
    }

    /// <summary>
    /// Registers the Workbench gateway and user interface and connects them to
    /// the application APIs.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder that receives the project
    /// resources.
    /// </param>
    /// <param name="ordersApi">
    /// The microservices API exposed through the gateway.
    /// </param>
    /// <param name="orderingApi">
    /// The virtual-actors API exposed through the gateway.
    /// </param>
    /// <returns>The registered Workbench resources.</returns>
    private static WorkbenchServices AddWorkbenchServices(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> ordersApi,
        IResourceBuilder<ProjectResource> orderingApi) {
        IResourceBuilder<ProjectResource> workbenchGateway = builder
            .AddProject<Projects.Workbench_Gateway>(ResourceNames.WorkbenchGateway)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Workbench Gateway")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            // Replace configured API URLs with Aspire-managed endpoints.
            .WithEnvironment(
                ConfigurationKeys.MicroservicesBaseUrl,
                ordersApi.GetEndpoint(Endpoints.HttpProtocol))
            .WithEnvironment(
                ConfigurationKeys.VirtualActorsBaseUrl,
                orderingApi.GetEndpoint(Endpoints.HttpProtocol))
            .WithObservabilityConfiguration(builder.Configuration);

        IResourceBuilder<ProjectResource> workbenchUi = builder
            .AddProject<Projects.Workbench_Ui>(ResourceNames.WorkbenchUi)
            .WithUrlForEndpoint(
                Endpoints.HttpProtocol,
                static url => url.DisplayText = "Workbench UI")
            .WithHttpHealthCheck(
                path: Endpoints.HealthPath,
                endpointName: Endpoints.HttpProtocol)
            // Replace the configured gateway URL with its Aspire-managed endpoint.
            .WithEnvironment(
                ConfigurationKeys.GatewayBaseUrl,
                workbenchGateway.GetEndpoint(Endpoints.HttpProtocol))
            .WaitFor(workbenchGateway)
            .WithObservabilityConfiguration(builder.Configuration);

        return new WorkbenchServices(
            workbenchUi,
            workbenchGateway);
    }

    /// <summary>
    /// Publishes the application topology for Aspire grouping and Workbench
    /// observability.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="workbench">The registered Workbench resources.</param>
    /// <param name="microservices">
    /// The registered order-processing microservices.
    /// </param>
    /// <param name="virtualActors">
    /// The registered virtual-actor resources.
    /// </param>
    /// <param name="healthStatusEvaluator"></param>
    /// <remarks>
    /// The Workbench UI receives the serialized topology and service endpoint
    /// configuration.
    /// </remarks>
    private static void AddObservabilityTopology(
        IDistributedApplicationBuilder builder,
        WorkbenchServices workbench,
        Microservices microservices,
        VirtualActorServices virtualActors,
        IHealthStatusEvaluator healthStatusEvaluator) {
        builder.AddTopology(
            healthStatusEvaluator,
            workbench.Ui,
            topology => {
                AddTopologyNodes(
                    topology,
                    workbench,
                    microservices,
                    virtualActors);
                AddTopologyDependencies(
                    topology,
                    workbench,
                    microservices,
                    virtualActors);
                AddTopologyGroups(
                    topology,
                    workbench,
                    microservices,
                    virtualActors);
            });
    }

    /// <summary>
    /// Registers service and storage nodes in the neutral topology.
    /// </summary>
    /// <param name="topology">The topology builder that receives the nodes.</param>
    /// <param name="workbench">The registered Workbench resources.</param>
    /// <param name="microservices">
    /// The registered order-processing microservices.
    /// </param>
    /// <param name="virtualActors">
    /// The registered virtual-actor resources.
    /// </param>
    /// <remarks>
    /// All nodes are registered before dependencies and groups because those
    /// topology references are order-dependent.
    /// </remarks>
    private static void AddTopologyNodes(
        TopologyBuilder topology,
        WorkbenchServices workbench,
        Microservices microservices,
        VirtualActorServices virtualActors) {
        topology
            .AddService(
                workbench.Ui,
                "Workbench UI")
            .AddService(
                workbench.Gateway,
                "Workbench API")
            .AddService(
                microservices.OrdersApi,
                "Orders API")
            .AddService(
                microservices.InventoryApi,
                "Inventory API")
            .AddService(
                microservices.PaymentsApi,
                "Payments API")
            .AddService(
                virtualActors.OrderingApi,
                "Ordering API")
            .AddService(
                virtualActors.OrderingSilo,
                "Ordering Silo")
            .AddStorage(
                TopologyNodeIds.OrdersDatabase,
                "Orders Database",
                microservices.OrdersApi,
                TopologyNodeIds.OrdersDatabase)
            .AddStorage(
                TopologyNodeIds.InventoryDatabase,
                "Inventory Database",
                microservices.InventoryApi,
                TopologyNodeIds.InventoryDatabase)
            .AddStorage(
                TopologyNodeIds.PaymentsDatabase,
                "Payments Database",
                microservices.PaymentsApi,
                TopologyNodeIds.PaymentsDatabase)
            .AddStorage(
                TopologyNodeIds.OrderingDatabase,
                "Ordering Database",
                virtualActors.OrderingSilo,
                TopologyNodeIds.OrderingDatabase);
    }

    /// <summary>
    /// Registers directed service and storage dependencies in the neutral
    /// topology.
    /// </summary>
    /// <param name="topology">
    /// The topology builder that receives the dependency edges.
    /// </param>
    /// <param name="workbench">The registered Workbench resources.</param>
    /// <param name="microservices">
    /// The registered order-processing microservices.
    /// </param>
    /// <param name="virtualActors">
    /// The registered virtual-actor resources.
    /// </param>
    private static void AddTopologyDependencies(
        TopologyBuilder topology,
        WorkbenchServices workbench,
        Microservices microservices,
        VirtualActorServices virtualActors) {
        topology
            .AddDependency(
                workbench.Ui,
                workbench.Gateway)
            .AddDependency(
                workbench.Gateway,
                microservices.OrdersApi)
            .AddDependency(
                workbench.Gateway,
                virtualActors.OrderingApi)
            .AddDependency(
                microservices.OrdersApi,
                microservices.InventoryApi)
            .AddDependency(
                microservices.OrdersApi,
                microservices.PaymentsApi)
            .AddDependency(
                virtualActors.OrderingApi,
                virtualActors.OrderingSilo)
            .AddDependency(
                microservices.OrdersApi,
                TopologyNodeIds.OrdersDatabase,
                TopologyNodeIds.OrdersDatabase)
            .AddDependency(
                microservices.InventoryApi,
                TopologyNodeIds.InventoryDatabase,
                TopologyNodeIds.InventoryDatabase)
            .AddDependency(
                microservices.PaymentsApi,
                TopologyNodeIds.PaymentsDatabase,
                TopologyNodeIds.PaymentsDatabase)
            .AddDependency(
                virtualActors.OrderingSilo,
                TopologyNodeIds.OrderingDatabase,
                TopologyNodeIds.OrderingDatabase);
    }

    /// <summary>
    /// Registers visual groups in the neutral topology.
    /// </summary>
    /// <param name="topology">The topology builder that receives the groups.</param>
    /// <param name="workbench">The registered Workbench resources.</param>
    /// <param name="microservices">
    /// The registered order-processing microservices.
    /// </param>
    /// <param name="virtualActors">
    /// The registered virtual-actor resources.
    /// </param>
    private static void AddTopologyGroups(
        TopologyBuilder topology,
        WorkbenchServices workbench,
        Microservices microservices,
        VirtualActorServices virtualActors) {
        topology
            .AddGroup(
                TopologyGroupIds.Workbench,
                "Workbench",
                workbench.Ui,
                workbench.Gateway)
            .AddGroup(
                TopologyGroupIds.Microservices,
                "Microservices",
                new[] {
                    microservices.OrdersApi.Resource.Name,
                    microservices.InventoryApi.Resource.Name,
                    microservices.PaymentsApi.Resource.Name,
                    TopologyNodeIds.OrdersDatabase,
                    TopologyNodeIds.InventoryDatabase,
                    TopologyNodeIds.PaymentsDatabase,
                })
            .AddGroup(
                TopologyGroupIds.VirtualActors,
                "Virtual Actors",
                new[] {
                    virtualActors.OrderingApi.Resource.Name,
                    virtualActors.OrderingSilo.Resource.Name,
                    TopologyNodeIds.OrderingDatabase,
                });
    }

    /// <summary>
    /// Groups the project resources that implement order processing.
    /// </summary>
    /// <param name="OrdersApi">The order orchestration API.</param>
    /// <param name="InventoryApi">The inventory API.</param>
    /// <param name="PaymentsApi">The payments API.</param>
    private sealed record Microservices(
        IResourceBuilder<ProjectResource> OrdersApi,
        IResourceBuilder<ProjectResource> InventoryApi,
        IResourceBuilder<ProjectResource> PaymentsApi);

    /// <summary>
    /// Groups the project resources that implement the virtual-actor workflow.
    /// </summary>
    /// <param name="OrderingApi">The API that communicates with Orleans.</param>
    /// <param name="OrderingSilo">The Orleans silo.</param>
    private sealed record VirtualActorServices(
        IResourceBuilder<ProjectResource> OrderingApi,
        IResourceBuilder<ProjectResource> OrderingSilo);

    /// <summary>
    /// Groups the project resources that expose the Workbench experience.
    /// </summary>
    /// <param name="Ui">The Workbench user interface.</param>
    /// <param name="Gateway">The Workbench gateway.</param>
    private sealed record WorkbenchServices(
        IResourceBuilder<ProjectResource> Ui,
        IResourceBuilder<ProjectResource> Gateway);

    /// <summary>
    /// Contains stable names used to register Aspire project resources.
    /// </summary>
    private static class ResourceNames {
        /// <summary>Identifies the inventory API resource.</summary>
        public const string InventoryApi = "inventory-api";

        /// <summary>Identifies the payments API resource.</summary>
        public const string PaymentsApi = "payments-api";

        /// <summary>Identifies the orders API resource.</summary>
        public const string OrdersApi = "orders-api";

        /// <summary>Identifies the ordering silo resource.</summary>
        public const string OrderingSilo = "ordering-silo";

        /// <summary>Identifies the ordering API resource.</summary>
        public const string OrderingApi = "ordering-api";

        /// <summary>Identifies the Workbench gateway resource.</summary>
        public const string WorkbenchGateway = "workbench-gateway";

        /// <summary>Identifies the Workbench UI resource.</summary>
        public const string WorkbenchUi = "workbench-ui";
    }

    /// <summary>
    /// Contains endpoint names and paths shared by project registrations.
    /// </summary>
    private static class Endpoints {
        /// <summary>Identifies the HTTP endpoint exposed by each project.</summary>
        public const string HttpProtocol = "http";

        /// <summary>Identifies the readiness health-check path.</summary>
        public const string HealthPath = "/health";

        /// <summary>Identifies the Orleans dashboard path.</summary>
        public const string OrleansDashboardPath = "/dashboard";
    }

    /// <summary>
    /// Contains environment-variable names that override application
    /// configuration with Aspire-managed endpoint references.
    /// </summary>
    private static class ConfigurationKeys {
        /// <summary>Identifies the inventory API base-URL setting.</summary>
        public const string InventoryBaseUrl = "Services__InventoryBaseUrl";

        /// <summary>Identifies the payments API base-URL setting.</summary>
        public const string PaymentsBaseUrl = "Services__PaymentsBaseUrl";

        /// <summary>Identifies the microservices gateway base-URL setting.</summary>
        public const string MicroservicesBaseUrl = "ServiceEndpoints__MicroservicesBaseUrl";

        /// <summary>Identifies the virtual-actors gateway base-URL setting.</summary>
        public const string VirtualActorsBaseUrl = "ServiceEndpoints__VirtualActorsBaseUrl";

        /// <summary>Identifies the Workbench gateway base-URL setting.</summary>
        public const string GatewayBaseUrl = "Gateway__BaseUrl";
    }

    /// <summary>
    /// Contains stable identifiers for non-project topology nodes.
    /// </summary>
    private static class TopologyNodeIds {
        /// <summary>Identifies the orders database topology node.</summary>
        public const string OrdersDatabase = "orders-database";

        /// <summary>Identifies the inventory database topology node.</summary>
        public const string InventoryDatabase = "inventory-database";

        /// <summary>Identifies the payments database topology node.</summary>
        public const string PaymentsDatabase = "payments-database";

        /// <summary>Identifies the ordering database topology node.</summary>
        public const string OrderingDatabase = "ordering-database";
    }

    /// <summary>
    /// Contains stable identifiers for visual topology groups.
    /// </summary>
    internal static class TopologyGroupIds {
        /// <summary>
        /// Identifies the Workbench topology group.
        /// </summary>
        internal const string Workbench = "workbench";

        /// <summary>
        /// Identifies the microservices topology group.
        /// </summary>
        internal const string Microservices = "microservices";

        /// <summary>
        /// Identifies the virtual actors topology group.
        /// </summary>
        internal const string VirtualActors = "virtual-actors";
    }
}
