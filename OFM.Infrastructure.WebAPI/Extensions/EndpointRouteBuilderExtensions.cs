﻿using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Handlers.D365;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class EndpointRouteBuilderExtensions
{
    #region Portal

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
        documentsEndpoints.MapDelete("", DocumentsHandlers.DeleteAsync).WithTags("Documents").Produces(200).ProducesProblem(404);
    }

    public static void RegisterBatchOperationsEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var batchEndpoints = endpointRouteBuilder.MapGroup("/api/batches");

        batchEndpoints.MapPost("", BatchOperationsHandlers.BatchOperationsAsync).WithTags("Batches").Produces(200).ProducesProblem(404);
    }

    #endregion

    #region D365

    public static void RegisterBatchProcessesEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var requestsEndpoints = endpointRouteBuilder.MapGroup("/api/processes");

        requestsEndpoints.MapPost("/{processId}", ProcessesHandlers.RunProcessById).WithTags("D365 Processes");

    }

    #endregion
}