using ECC.Core.DataContext;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;

public class DuplicateCareTypeConditionLookup
{
    public bool HasDuplicateCareTypeCondition(ofm_application application)
    {
        return CheckApplication(application);
    }

    protected virtual bool CheckApplication(ofm_application application)
    {
        if (application.ofm_application_type == ecc_application_type.Renewal)
        {
            return true;
        }

        return false;
    }
}

public class RoomSplitConditionLookup
{
    public bool HasSplitRoomCondition(ofm_application application)
    {
        return CheckApplication(application);
    }

    protected virtual bool CheckApplication(ofm_application application)
    {
        if (application.ofm_application_type == ecc_application_type.Renewal)
        {
            return true;
        }

        return false;
    }
}

public class MonthlyReportingConditionLookup
{
    public bool HasMissingReports(ofm_application application)
    {
        return CheckApplication(application);
    }

    protected virtual bool CheckApplication(ofm_application application)
    {
        if (application.ofm_application_type == ecc_application_type.Renewal)
        {
            return true;
        }

        return false;
    }
}