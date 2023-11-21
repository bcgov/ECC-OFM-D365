using System.Net;

namespace OFM.Infrastructure.WebAPI.Extensions;
public class D365ServiceException : Exception
{
    public HttpStatusCode HttpStatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public ODataError? ODataError { get; set; }

    public string? Content { get; set; }

    public string? RequestId { get; set; }

    public D365ServiceException()
    {
    }

    public D365ServiceException(string message)
        : base(message)
    {
    }

    public D365ServiceException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public class ODataException
{
    public string? Message { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? ErrorCode { get; set; }
}

public class ODataError
{
    public Error? Error { get; set; }
}

public class Error
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
