using System.ComponentModel.DataAnnotations;
using FixedWidthParserWriter;

namespace OFM.Infrastructure.WebAPI.Models;

public record BCRegistrySearchResult
{
    public Facets? facets { get; set; }
    public Searchresults? searchResults { get; set; }

    public record Facets
    {
        public Fields? fields { get; set; }
    }

    public record Fields
    {
        public Legaltype[]? legalType { get; set; }
        public Status[]? status { get; set; }
    }

    public record Legaltype
    {
        public int count { get; set; }
        public string? value { get; set; }
    }

    public record Status
    {
        public int count { get; set; }
        public string? value { get; set; }
    }

    public record Searchresults
    {
        public Queryinfo? queryInfo { get; set; }
        public Result[]? results { get; set; }
        public int totalResults { get; set; }
    }

    public record Queryinfo
    {
        public Categories? categories { get; set; }
        public Query? query { get; set; }
        public int rows { get; set; }
        public int start { get; set; }
    }

    public record Categories
    {
        public object? legalType { get; set; }
        public object? status { get; set; }
    }

    public record Query
    {
        public string? bn { get; set; }
        public string? identifier { get; set; }
        public string? name { get; set; }
        public string? value { get; set; }
    }

    public record Result
    {
        public string? bn { get; set; }
        public bool goodStanding { get; set; }
        public string? identifier { get; set; }
        public string? legalType { get; set; }
        public string? name { get; set; }
        public float score { get; set; }
        public string? status { get; set; }
    }
}


public class BCRegistryBusinessResult
{
    public Business business { get; set; }
}

public class Business
{
    public bool adminFreeze { get; set; }
    public Allowedactions allowedActions { get; set; }
    public Alternatename[] alternateNames { get; set; }
    public string arMaxDate { get; set; }
    public string arMinDate { get; set; }
    public object associationType { get; set; }
    public object[] complianceWarnings { get; set; }
    public string fiscalYearEndDate { get; set; }
    public DateTime foundingDate { get; set; }
    public bool goodStanding { get; set; }
    public bool hasCorrections { get; set; }
    public bool hasCourtOrders { get; set; }
    public bool hasRestrictions { get; set; }
    public string identifier { get; set; }
    public bool inDissolution { get; set; }
    public string lastAddressChangeDate { get; set; }
    public string lastAnnualGeneralMeetingDate { get; set; }
    public string lastAnnualReportDate { get; set; }
    public string lastDirectorChangeDate { get; set; }
    public DateTime lastLedgerTimestamp { get; set; }
    public DateTime lastModified { get; set; }
    public string legalName { get; set; }
    public string legalType { get; set; }
    public string naicsCode { get; set; }
    public string naicsDescription { get; set; }
    public string naicsKey { get; set; }
    public DateTime nextAnnualReport { get; set; }
    public bool noDissolution { get; set; }
    public string startDate { get; set; }
    public string state { get; set; }
    public string submitter { get; set; }
    public string taxId { get; set; }
    public object[] warnings { get; set; }
}

public class Allowedactions
{
    public bool digitalBusinessCard { get; set; }
    public Filing filing { get; set; }
    public bool viewAll { get; set; }
}

public class Filing
{
    public string filingSubmissionLink { get; set; }
    public Filingtype[] filingTypes { get; set; }
}

public class Filingtype
{
    public string displayName { get; set; }
    public string feeCode { get; set; }
    public string name { get; set; }
    public string type { get; set; }
}

public class Alternatename
{
    public string entityType { get; set; }
    public string identifier { get; set; }
    public string name { get; set; }
    public DateTime registeredDate { get; set; }
    public string startDate { get; set; }
    public string type { get; set; }
}


public record InvoiceHeader
{
    public required string feederNumber { get; set; }
    public required string headertransactionType { get; set; }
    public required string batchType { get; set; }
    public required string delimiter { get; set; }
    [StringLength(9)]
    public required string supplierNumber { get; set; }
    [StringLength(3)]
    public required string supplierSiteNumber { get; set; }
    [StringLength(50)]
    public required string invoiceNumber { get; set; }
    [StringLength(2)]
    public required string invoiceType { get; set; }
    [StringLength(8)]
    public required string invoiceDate { get; set; }
    [StringLength(8)]
    public required string invoiceRecDate { get; set; }
    [StringLength(8)]
    public required string goodsDate { get; set; }
    [StringLength(20)]
    public required string PONumber { get; set; }
    [StringLength(9)]
    public required string payGroupLookup { get; set; }
    [StringLength(4)]
    public required string remittanceCode { get; set; }
    [StringLength(15)]
    public required string grossInvoiceAmount { get; set; }
    [StringLength(3)]
    public required string CAD { get; set; }
    [StringLength(50)]
    public required string termsName { get; set; }
    [StringLength(60)]
    public required string description { get; set; }
    [StringLength(30)]
    public required string oracleBatchName { get; set; }
    public required string payflag { get; set; }
    [StringLength(110)]
    public required string flow { get; set; }
    [StringLength(9)]
    public required string SIN { get; set; }
    public List<InvoiceLines>? invoiceLines { get; set; }
}

public record InvoiceLines
{
    public required string feederNumber { get; set; }
    public required string batchType { get; set; }
    public required string delimiter { get; set; }
    public required string linetransactionType { get; set; }
    [StringLength(50)]
    public required string invoiceNumber { get; set; }
    [StringLength(4)]
    public required string invoiceLineNumber { get; set; }
    [StringLength(9)]
    public required string supplierNumber { get; set; }
    [StringLength(3)]
    public required string supplierSiteNumber { get; set; }
    [StringLength(4)]
    public required string committmentLine { get; set; }
    [StringLength(15)]
    public required string lineAmount { get; set; }
    [StringLength(1)]
    public required string lineCode { get; set; }
    [StringLength(50)]
    public required string distributionACK { get; set; }
    [StringLength(55)]
    public required string lineDescription { get; set; }
    [StringLength(8)]
    public required string effectiveDate { get; set; }
    [StringLength(10)]
    public required string quantity { get; set; }
    [StringLength(15)]
    public required string unitPrice { get; set; }
    [StringLength(163)]
    public required string optionalData { get; set; }
    [StringLength(30)]
    public required string distributionSupplierNumber { get; set; }
    [StringLength(110)]
    public required string flow { get; set; }

}

public record FeedbackBatch
{
    [CustomFileField(StartsWith = "APBH", Start = 4, Length = 4)]
    public string Header { get; set; }

    [CustomFileField(StartsWith = "APBH", Start = 8, Length = 150)]
    public string BHError { get; set; }

    [CustomFileField(StartsWith = "APBT")]
    public string BatchTrailer { get; set; }
    public List<FeedbackHeader>? feedbackHeader { get; set; }
}

public record FeedbackHeader
{
    [CustomFileField(StartsWith = "APIH", Start = 4, Length = 9)]
    public string IHSupplier { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 16, Length = 50)]
    public string IHInvoice { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 411, Length = 4)]
    public string IHCode { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 415, Length = 150)]
    public string IHError { get; set; }


    public List<FeedbackLine>? feedbackLine { get; set; }

}

public record FeedbackLine
{
    [CustomFileField(StartsWith = "APIL", Start = 4, Length = 9)]
    public string ILSupplier { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 16, Length = 50)]
    public string ILInvoice { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 77, Length = 15)]
    public string ILAmount { get; set; }


    [CustomFileField(StartsWith = "APIL", Start = 139, Length = 55)]
    public string ILDescription { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 531, Length = 4)]
    public string ILCode { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 535, Length = 150)]
    public string ILError { get; set; }

}

//ECER API Model
// Model for the token response from the POST endpoint
public class TokenResponse
{
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public string access_token { get; set; }
}

// Model for each file returned from the ECER files endpoint
public class CertificationFile
{
    public string id { get; set; }
    public string fileName { get; set; }
    public string fileId { get; set; }
    public string createdOn { get; set; }
}

// Model for the certification details downloaded for a file
public class CertificationDetail
{
    public string? registrationnumber { get; set; }
    public string? certificatelevel { get; set; }
    public DateTime? effectivedate { get; set; }
    public DateTime? expirydate { get; set; }
    public string? firstname { get; set; }
    public string? lastname { get; set; }
}
