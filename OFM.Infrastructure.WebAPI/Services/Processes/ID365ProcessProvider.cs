﻿using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public interface ID365ScheduledProcessProvider
{
    Int16 ProcessId { get; }
    string ProcessName { get; }
    Task<ProcessData> GetData();
    Task<JsonObject> RunScheduledProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams);
}

public interface ID365OndemandProcessProvider
{
    Int16 ProcessId { get; }
    string ProcessName { get; }
    Task<ProcessData> GetData();
    Task<ProcessResult> RunOndemandProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams);
}