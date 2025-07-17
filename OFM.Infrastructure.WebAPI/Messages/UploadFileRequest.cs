using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Messages;

/// <summary>
/// Contains the data to update file column
/// </summary>
public sealed class UploadFileRequest : HttpRequestMessage
    {
        public UploadFileRequest(
            D365EntityReference entityReference,
            string columnName,
            Byte[] fileContent,
            string fileName,
            int? fileColumnMaxSizeInKb = null)
        {
            if (fileColumnMaxSizeInKb.HasValue && (fileContent.Length / 1024) > fileColumnMaxSizeInKb.Value)
            {
                throw new Exception($"The file is too large to be uploaded to this column.");
            }
            else
            {
                var fileNameWoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                fileNameWoExt = Regex.Replace(fileNameWoExt, @"[^\w]", "");
                fileName = fileNameWoExt + ext;
            }

           

            Method = HttpMethod.Patch;
            RequestUri = new Uri(
                uriString: $"{entityReference.Path}/{columnName}?x-ms-file-name={fileName}",
                uriKind: UriKind.Relative);
            Content = new ByteArrayContent(fileContent);
            Content.Headers.Add("Content-Type", "application/octet-stream");
        }
    }