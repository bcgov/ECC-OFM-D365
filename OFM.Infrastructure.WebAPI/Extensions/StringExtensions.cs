﻿using System.Text.Json;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Extensions;
public static class StringExtensions
{
    private static readonly string CRLF = "\r\n";

    public static string CleanLog(this string text)
    {
        var options = new JsonSerializerOptions();
        options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        _ = text.Replace("\u0022", "");

        var returned = System.Text.RegularExpressions.Regex.Unescape(text);

        return returned;
    }
    public static string CleanCRLF(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
  
        return Regex.Replace(text, CRLF, "");
    }
}
public record TempApplication { };

public interface IHaveItems<T>
{
    List<T> Items { get; set; }
}

public record ApplicationResponse : IHaveItems<TempApplication>
{
    public List<TempApplication> Items { get; set; } = new();
}
public record LicenceResponse : IHaveItems<TempLicence>
{
    public List<TempLicence> Items { get; set; } = new();
}

public class TempLicence
{
}

//public record LicenceDetailResponse : IHaveItems<LicenceDetail>
//{
//    public List<LicenceDetail> Items { get; set; } = new();
//}

//public class LicenceDetail
//{
//}

