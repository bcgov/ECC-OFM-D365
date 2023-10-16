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
        var myEndpoints = endpointRouteBuilder.MapGroup("/api/operations");

        myEndpoints.MapGet("", OperationsHandlers.GetAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        myEndpoints.MapPost("", OperationsHandlers.PostAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        myEndpoints.MapPatch("", OperationsHandlers.PatchAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        myEndpoints.MapPut("", OperationsHandlers.PutAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
        myEndpoints.MapDelete("", OperationsHandlers.DeleteAsync).WithTags("Operations").Produces(200).ProducesProblem(404);
    }

    public static void RegisterDocumentsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var myEndpoints = endpointRouteBuilder.MapGroup("/api/documents");

        myEndpoints.MapGet("", DocumentsHandlers.GetAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
        myEndpoints.MapPost("", DocumentsHandlers.PostAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
        myEndpoints.MapDelete("", DocumentsHandlers.DeleteAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
    }

    public static void RegisterBatchOperationsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var searchesEndpoints = endpointRouteBuilder.MapGroup("/api/batches");

        searchesEndpoints.MapPost("", BatchOperationsHandlers.BatchOperationAsync).WithTags("Batches").Produces(200).ProducesProblem(404);
    }
}