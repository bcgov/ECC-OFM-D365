using OFM.Infrastructure.WebAPI.Models.Fundings;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

/// <summary>
///  This calculator version calculates funding amounts using the default allocation. 
///  It is intended to generate the initial calculation breakdowns for the CRC to compare with the modifed funding amounts in a complex scenario such as room split.
/// </summary>
/// <param name="fundingRepository"></param>
/// <param name="funding"></param>
/// <param name="rateSchedules"></param>
/// <param name="logger"></param>
sealed class DefaultCalculator(IFundingRepository fundingRepository, Funding funding, IEnumerable<RateSchedule> rateSchedules, ILogger logger) : FundingCalculator(fundingRepository, funding, rateSchedules, logger)
{
    public override IEnumerable<LicenceDetail> LicenceDetails
    {
        get
        {
            IEnumerable<LicenceDetail>? licenceDetails = base.LicenceDetails;

            foreach (var ld in licenceDetails)
            {
                // Reset to default allocation: i.e. Override Room Split Condition, or any specific calconditions here.
                ld.ApplyRoomSplitCondition = false;
            }

            return licenceDetails;
        }
    }

    /// <summary>
    /// Only used for split room scenario. It will calculate and set the default allocation values for each applicable space allocation record.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CalculateDefaultSpacesAllocationAsync()
    {
        foreach (LicenceDetail licenceDetail in LicenceDetails)
        {
            //Note: For each licence detail, calculate the default group sizes based on the cclr ratios table and grouped by the group size
            var groupedByGSize = licenceDetail.DefaultGroupSizes!.GroupBy(grp1 => grp1, grp2 => grp2, (g1, g2) => new { GroupSize = g1, Count = g2.Count() });

            foreach (var space in licenceDetail.NewSpacesAllocationByLicenceType)
            {
                space.ofm_default_allocation = groupedByGSize.FirstOrDefault(grp => grp.GroupSize == space.ofm_cclr_ratio?.ofm_group_size)?.Count ?? 0;
            }
        }

        var newSpaces = LicenceDetails.SelectMany(s => s.NewSpacesAllocationByLicenceType)
                                        .Where(s => s.ofm_default_allocation.Value > 0);

        if (!newSpaces.Any())
        {
            _logger.LogWarning("No new spaces allocation records to update.");
            return await Task.FromResult(false);
        }

        var result = await _fundingRepository.SaveDefaultSpacesAllocationAsync(newSpaces);

        return await Task.FromResult(result);
    }
}
  