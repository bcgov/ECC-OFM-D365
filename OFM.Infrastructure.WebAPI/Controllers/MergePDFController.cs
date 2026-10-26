using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Text;

namespace OFM.Infrastructure.WebAPI.Controllers;
public class PdfMergeRequest
{
    [JsonProperty("file1Base64")]
    public string File1Base64 { get; set; } = string.Empty;
    [JsonProperty("file2Base64")]
    public string File2Base64 { get; set; } = string.Empty;
    [JsonProperty("insertPageNumbers")]
    public bool? InsertPageNumbers { get; set; } = false;
    [JsonProperty("headers")]
    public PdfHeader[] Headers { get; set; }
}
public class PdfHeader
{
    [JsonProperty("headerText")]
    public string HeaderText { get; set; } = string.Empty;
    [JsonProperty("alignment")]
    public string Alignment { get; set; } = string.Empty;
    [JsonProperty("isBold")]
    public bool? IsBold { get; set; } = false;

}
[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    [HttpPost("Merge")]
    [Consumes("application/json")]
    public IActionResult MergePdfFilesJson([FromBody] PdfMergeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.File1Base64))
            return BadRequest("File 1 Base64 PDF strings is required.");

        using var outputDocument = new PdfDocument();
        try
        {
            if (!IsValidPdfBase64(request.File1Base64)) throw new FormatException("File 1 is not a valid PDF");
            // Decode and merge first PDF
            var file1Bytes = Convert.FromBase64String(request.File1Base64);
            using var stream1 = new MemoryStream(file1Bytes);
            var inputDocument1 = PdfReader.Open(stream1, PdfDocumentOpenMode.Import);
            foreach (var page in inputDocument1.Pages)
                outputDocument.AddPage(page);

            if (!string.IsNullOrWhiteSpace(request.File2Base64))
            {
                // Decode and merge second PDF
                if (IsValidPdfBase64(request.File2Base64))
                {
                    var file2Bytes = Convert.FromBase64String(request.File2Base64);
                    using var stream2 = new MemoryStream(file2Bytes);
                    var inputDocument2 = PdfReader.Open(stream2, PdfDocumentOpenMode.Import);
                    foreach (var page in inputDocument2.Pages)
                        outputDocument.AddPage(page);
                }
            }

            // Add header and footer to each page
            for (int i = 0; i < outputDocument.Pages.Count; i++)
            {
                var page = outputDocument.Pages[i];
                var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                // Header
                if (request.Headers.Any())
                {
                    for (int j = 0; j < request.Headers.Length; j++)
                    {
                        var header = request.Headers[j];
                        XStringFormat alignment = header.Alignment.ToLower() switch
                        {
                            "topcenter" => XStringFormats.TopCenter,
                            "topright" => XStringFormats.TopRight,
                            "topleft" => XStringFormats.TopLeft,
                            "left" => XStringFormats.CenterLeft,
                            "bottomcenter" => XStringFormats.BottomCenter,
                            "bottomright" => XStringFormats.BottomRight,
                            "bottomleft" => XStringFormats.BottomLeft,
                            "right" => XStringFormats.CenterRight,
                            _ => XStringFormats.Center
                        };

                        gfx.DrawString(header.HeaderText,
                            new XFont("Calibri", 10, header.IsBold == true ? XFontStyleEx.Bold : XFontStyleEx.Regular),
                            XBrushes.Black,
                            new XRect(20, 20, page.Width - 40, 40),
                            alignment);
                    }

                }
                // Footer
                if (request.InsertPageNumbers == true)
                    gfx.DrawString($"Page {i + 1} of {outputDocument.PageCount}",
                        new XFont("Calibri", 10, XFontStyleEx.Regular),
                        XBrushes.Black,
                        new XRect(20, page.Height - 40, page.Width - 40, 20),
                        XStringFormats.BottomRight);
            }
            using var outputStream = new MemoryStream();
            outputDocument.Save(outputStream);
            outputStream.Position = 0;
            var mergedBase64 = Convert.ToBase64String(outputStream.ToArray());
            return Ok(new
            {
                fileName = "merged.pdf",
                base64Content = mergedBase64
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"PDF merge failed: {ex.Message}");
        }
    }
    private static bool IsValidPdfBase64(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        try
        {
            // Decode base64
            var bytes = Convert.FromBase64String(base64);

            // Check for PDF header: "%PDF"
            var header = Encoding.ASCII.GetString(bytes.Take(4).ToArray());
            return header == "%PDF";
        }
        catch
        {
            return false;
        }
    }

}