using ECC.Core.DataContext;
using Microsoft.Identity.Client;
using Microsoft.VisualBasic;
using OFM.Infrastructure.WebAPI.Models;
using System.Diagnostics;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public abstract class ApplicationState
{
    public FundingApplication Application { get; protected set; } = null!;
    public string? FundingNumberBase { get; protected set; }

    public abstract void GenerateFileNumber();
    public abstract void CalculateFundingAmounts();
    public abstract void GenerateAgreement();
    public abstract void AddNewAgreementVersion();
    public abstract void SendProviderNotifications();
}

/// <summary>
/// CAN : GenerateFileNumber
/// </summary>
public class UnsubmittedApplication : ApplicationState
{
    public UnsubmittedApplication(FundingApplication application)
    {
        Application = application;
    }

    public override void GenerateFileNumber()
    {
        Debugger.Log(0, null, "GenerateFileNumber called.");
    }

    public override void CalculateFundingAmounts()
    {
        throw new NotImplementedException();
    }

    public override void GenerateAgreement()
    {
        throw new NotImplementedException();
    }

    public override void AddNewAgreementVersion()
    {
        throw new NotImplementedException();
    }

    public override void SendProviderNotifications()
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// CAN: CalculateFundingAmounts, GenerateAgreement, AddNewAgreementVersion
/// </summary>
/// <exception cref="NotImplementedException"></exception>
public class InReviewApplication : ApplicationState
{
    public InReviewApplication(FundingApplication application)
    {
        Application = application;
    }

    public override void AddNewAgreementVersion()
    {
        throw new NotImplementedException();
    }

    public override void CalculateFundingAmounts()
    {
        throw new NotImplementedException();
    }

    public override void GenerateAgreement()
    {
        throw new NotImplementedException();
    }

    public override void GenerateFileNumber()
    {
        FundingNumberBase = "NewID-2400011-00";

    }

    public override void SendProviderNotifications()
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// CAN: CalculateFundingAmounts, GenerateAgreement, AddNewAgreementVersion, SendProviderNotifications
/// </summary>
/// <exception cref="NotImplementedException"></exception>
public class ApprovedApplication : ApplicationState
{
    public ApprovedApplication(FundingApplication application)
    {
        Application = application;
    }

    public override void AddNewAgreementVersion()
    {
        throw new NotImplementedException();
    }

    public override void CalculateFundingAmounts()
    {
        throw new NotImplementedException();
    }

    public override void GenerateAgreement()
    {
        throw new NotImplementedException();
    }

    public override void GenerateFileNumber()
    {
        throw new NotImplementedException();
    }

    public override void SendProviderNotifications()
    {
        throw new NotImplementedException();
    }
}

//public class EAPendingSignatureApplication : ApplicationState
//{
//    public EAPendingSignatureApplication(ofm_application application)
//    {
//        Application = application;
//    }

//    public override void CalculateFundingAmounts()
//    {
//        throw new NotImplementedException();
//    }

//    public override void GenerateFileNumber()
//    {
//        throw new NotImplementedException();
//    }
//}

/// <summary>
/// Non methods are allowed
/// </summary>
public class FinalizedApplication : ApplicationState
{
    public FinalizedApplication(FundingApplication application)
    {
        Application = application;
    }

    public override void AddNewAgreementVersion()
    {
        throw new NotImplementedException();
    }

    public override void CalculateFundingAmounts()
    {
        throw new NotImplementedException();
    }

    public override void GenerateAgreement()
    {
        throw new NotImplementedException();
    }

    public override void GenerateFileNumber()
    {
        throw new NotImplementedException();
    }

    public override void SendProviderNotifications()
    {
        throw new NotImplementedException();
    }
}

public class FundingApplication : ofm_application
{
    public FundingApplication()
    {
        // default state
        ApplicationState = new UnsubmittedApplication(this);
    }

    public ApplicationState ApplicationState { get; set; }

    public string? FileNumber => ApplicationState.FundingNumberBase;

    public bool HasFileNumber => !string.IsNullOrEmpty(ApplicationState.FundingNumberBase);

    public void AddNewAgreementVersion()
    {
        ApplicationState.AddNewAgreementVersion();
    }

    public void CalculateFundingAmounts()
    {
        ApplicationState.CalculateFundingAmounts();
    }

    public void GenerateAgreement()
    {
        ApplicationState.GenerateAgreement();
    }

    public void GenerateFileNumber()
    {
        ApplicationState.GenerateFileNumber();

    }

    public void SendProviderNotifications()
    {
        ApplicationState.SendProviderNotifications();
    }

    public bool HasValidCoreServices()
    {
        throw new NotImplementedException();
    }
}