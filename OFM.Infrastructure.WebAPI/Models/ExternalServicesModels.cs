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

    public record feedbackBatch
    {
        [CustomFileField(StartsWith = "APBH", Start = 4, Length = 4)]
        public string Header { get; set; }

        [CustomFileField(StartsWith = "APBH", Start = 8, Length = 150)]
        public string BHError { get; set; }

        [CustomFileField(StartsWith = "APBT")]
        public string BatchTrailer { get; set; }
        public List<feedbackHeader>? feedbackHeader { get; set; }
    }

    public record feedbackHeader
    {
        [CustomFileField(StartsWith = "APIH", Start = 4, Length = 9)]
        public string IHSupplier { get; set; }

        [CustomFileField(StartsWith = "APIH", Start = 16, Length = 50)]
        public string IHInvoice { get; set; }

        [CustomFileField(StartsWith = "APIH", Start = 411, Length = 4)]
        public string IHCode { get; set; }

        [CustomFileField(StartsWith = "APIH", Start = 415, Length = 150)]
        public string IHError { get; set; }


        public List<feedbackLine>? feedbackLine { get; set; }

    }

    public record feedbackLine
    {

        [CustomFileField(StartsWith = "APIL", Start = 4, Length = 9)]
        public string ILSupplier { get; set; }

        [CustomFileField(StartsWith = "APIL", Start = 16, Length = 50)]
        public  string ILInvoice { get; set; }

        [CustomFileField(StartsWith = "APIL", Start = 77, Length = 15)]
        public  string ILAmount { get; set; }


        [CustomFileField(StartsWith = "APIL", Start = 139, Length = 55)]
        public string ILDescription { get; set; }

        [CustomFileField(StartsWith = "APIL", Start = 531, Length = 4)]
        public string ILCode { get; set; }

        [CustomFileField(StartsWith = "APIL", Start = 535, Length = 150)]
        public string ILError { get; set; }

    }
}
