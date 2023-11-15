using OFM.Infrastructure.WebAPI.Handlers;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void RegisterEnvironmentEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var endpoints = endpointRouteBuilder.MapGroup("/api/environment");

        endpoints.MapGet("", EnvironmentHandlers.Get).WithTags("Environment").Produces(200).ProducesProblem(404);
    }

    public static void RegisterSearchesEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var searchesEndpoints = endpointRouteBuilder.MapGroup("/api/searches");

        searchesEndpoints.MapPost("", SearchesHandlers.DataverseSearchAsync).WithTags("Searches").Produces(200).ProducesProblem(404);
    }

    public static void RegisterProviderProfileEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var searchesEndpoints = endpointRouteBuilder.MapGroup("/api/providerprofile");

        searchesEndpoints.MapGet("", ProviderProfilesHandlers.GetProfileAsync).WithTags("Providers").Produces(200).ProducesProblem(404);
    }

    public static void RegisterOperationsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var operationsEndpoints = endpointRouteBuilder.MapGroup("/api/operations");

        operationsEndpoints.MapGet("", OperationsHandlers.GetAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        operationsEndpoints.MapPost("", OperationsHandlers.PostAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        operationsEndpoints.MapPatch("", OperationsHandlers.PatchAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        operationsEndpoints.MapDelete("", OperationsHandlers.DeleteAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
    }

    public static void RegisterDocumentsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var documentsEndpoints = endpointRouteBuilder.MapGroup("/api/documents");

        documentsEndpoints.MapGet("", DocumentsHandlers.GetAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
        documentsEndpoints.MapPost("", DocumentsHandlers.PostAsync).WithTags("Documents").Produces(200).ProducesProblem(404).DisableAntiforgery();
        documentsEndpoints.MapPatch("", DocumentsHandlers.PatchAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
        documentsEndpoints.MapDelete("", DocumentsHandlers.DeleteAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
    }

    public static void RegisterBatchOperationsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var searchesEndpoints = endpointRouteBuilder.MapGroup("/api/batches");

        searchesEndpoints.MapPost("", BatchOperationsHandlers.BatchOperationAsync).WithTags("Batches").Produces(200).ProducesProblem(404);
    }
}