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
