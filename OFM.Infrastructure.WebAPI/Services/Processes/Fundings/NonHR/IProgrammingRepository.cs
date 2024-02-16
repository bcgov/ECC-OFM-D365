using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.NonHR;

public interface IProgrammingRepository
{
    Task<decimal> EvaluateAsync(ofm_licence_detail coreService);
}