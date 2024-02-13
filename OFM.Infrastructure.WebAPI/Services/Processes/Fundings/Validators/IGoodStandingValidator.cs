using System;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;

public interface ILicenseData
{
    string LicenseKey { get; }
}

public interface IServiceInformation
{
    ILicenseData License { get; }
}

public interface IGoodStandingValidator
{
    bool IsValid(string facilityNumber);
    void IsValid(string facilityNumber, out bool isValid);
    //string LicenseKey { get; }
    IServiceInformation ServiceInformation { get; }

    ValidationMode ValidationMode { get; set; }

    event EventHandler ValidatorLookupPerformed;
}