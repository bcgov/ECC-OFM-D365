using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OFM.Infrastructure.Plugins.AssistanceRequest
{
    public class StripInlineDocumentLinks : PluginBase
    {
        public StripInlineDocumentLinks(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(StripInlineDocumentLinks))
        {

        }
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            // Obtain the execution context
            IPluginExecutionContext context = localPluginContext.PluginExecutionContext;
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)localPluginContext.ServiceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)localPluginContext.ServiceProvider.GetService(typeof(ITracingService));

            try
            {
                tracingService.Trace("Check if the target entity is available");
                // Check if the target entity is available
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {
                    tracingService.Trace("Target entity is available");

                    Entity entity = (Entity)context.InputParameters["Target"];
                    string entityName = entity.LogicalName;
                    Guid requestId = Guid.Empty;

                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Serialize(entity);

                    tracingService.Trace($"Target entity is available {serializedResult}");

                    if (context.MessageName == "Create")
                        requestId = entity.GetAttributeValue<EntityReference>("ofm_request").Id;
                    if (context.MessageName == "Update")
                        requestId = (service.Retrieve("ofm_conversation", entity.Id, new ColumnSet("ofm_request"))).GetAttributeValue<EntityReference>("ofm_request").Id;

                    tracingService.Trace($"Message belongs to request with ID = {requestId}");

                    // Process rich text field for msdyn_richtextfile link extraction and removal
                    string richTextField = "ofm_message";
                    if (entity.Contains(richTextField) && !string.IsNullOrEmpty(entity[richTextField]?.ToString()))
                    {
                        string richText = entity.GetAttributeValue<string>(richTextField);
                        tracingService.Trace($"Message Body {richText}");
                        List<string> documentLinks;
                        tracingService.Trace($"RemoveRichTextFileLinks from message Body");
                        string cleanedText = RemoveRichTextFileLinks(richText, out documentLinks, requestId, service, tracingService);
                        entity[richTextField] = cleanedText;
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"StripInlineDocumentLinks Plugin execution failed: {ex.Message}");
                throw new InvalidPluginExecutionException($"StripInlineDocumentLinks Plugin execution failed: {ex}");
            }
        }

        private string RemoveRichTextFileLinks(string richText, out List<string> documentLinks, Guid requestId, IOrganizationService service, ITracingService tracingService)
        {
            documentLinks = new List<string>();
            if (string.IsNullOrEmpty(richText))
                return richText;

            // Regex to match <a> tags with msdyn_richtextfile URLs
            string pattern = @"<a\s+[^>]*href=""([^""]*msdyn_richtextfiles\([^)]+\)[^""]*)""[^>]*>(.*?)</a>";
            try
            {
                MatchCollection matches = Regex.Matches(richText, pattern, RegexOptions.IgnoreCase);
                string result = richText;

                foreach (Match match in matches)
                {
                    string href = match.Groups[1].Value;
                    string innerText = match.Groups[2].Value;
                    // Add href to documentLinks
                    documentLinks.Add(href);
                    // Download and store the file in new_document
                    byte[] fileContent = DownloadRichTextFile(href, service, tracingService, out string fileName, out string fileExtension, out long fileSize);
                    Entity newDocument = new Entity("ofm_document");
                    newDocument["ofm_subject"] = Uri.UnescapeDataString(fileName ?? innerText); // Use filename or inner text
                    newDocument["ofm_file_size"] = Convert.ToDecimal(fileSize); // Store size in bytes
                    newDocument["ofm_extension"] = fileExtension; // Store extension
                    newDocument["ofm_category"] = "Assistance Request"; // Store category
                    newDocument["ofm_regardingid"] = new EntityReference("ofm_assistance_request", requestId); // Link to parent
                    var docId = service.Create(newDocument);
                    tracingService.Trace($"Created new_document for file: {href}");
                    if (fileContent != null)
                        UploadFile(service, docId, fileName, fileContent);
                    // Replace the <a> tag with empty string
                    result = result.Replace(match.Value, string.Empty);
                }

                tracingService.Trace($"Processed rich text. Original length: {richText.Length}, Cleaned length: {result.Length}, Links extracted: {documentLinks.Count}");
                return result;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error occurred while processing documents: {ex}");
                throw;
            }
        }

        private byte[] DownloadRichTextFile(string url, IOrganizationService service, ITracingService tracingService, out string fileName, out string fileExtension, out long fileSize)
        {
            fileName = null;
            fileExtension = null;
            fileSize = 0;

            try
            {
                // Extract msdyn_richtextfile GUID from URL
                string pattern = @"msdyn_richtextfiles\(([^)]+)\)";
                Match match = Regex.Match(url, pattern);
                if (!match.Success)
                {
                    tracingService.Trace($"Invalid msdyn_richtextfile URL: {url}");
                    return null;
                }

                string richTextFileId = match.Groups[1].Value;
                Entity richTextFile = service.Retrieve("msdyn_richtextfile", new Guid(richTextFileId), new ColumnSet("msdyn_fileblob", "msdyn_name"));
                tracingService.Trace($" msdyn_fileblob found for msdyn_richtextfile {richTextFileId}");
                InitializeFileBlocksDownloadRequest ifbdRequest = new InitializeFileBlocksDownloadRequest
                {
                    Target = new EntityReference("msdyn_richtextfile", richTextFile.Id),
                    FileAttributeName = "msdyn_fileblob"
                };

                InitializeFileBlocksDownloadResponse ifbdResponse = (InitializeFileBlocksDownloadResponse)service.Execute(ifbdRequest);

                DownloadBlockRequest dbRequest = new DownloadBlockRequest
                {
                    FileContinuationToken = ifbdResponse.FileContinuationToken
                };

                var resdbResponsep = (DownloadBlockResponse)service.Execute(dbRequest);
                if (richTextFile.Contains("msdyn_fileblob"))
                {
                    string fileContent = Convert.ToBase64String(resdbResponsep.Data);
                    tracingService.Trace($" file content of msdyn_fileblob found for msdyn_richtextfile {fileContent}");
                    fileName = ifbdResponse.FileName;
                    tracingService.Trace($" file name of msdyn_fileblob found for msdyn_richtextfile {fileName}");
                    // Calculate file size
                    fileSize = ifbdResponse.FileSizeInBytes;//CalculateBase64FileSize(fileContent, tracingService);

                    // Extract file extension
                    fileExtension = string.IsNullOrEmpty(fileName) ? "" : GetFileExtension(fileName);

                    tracingService.Trace($"Downloaded file from msdyn_richtextfile {richTextFileId}, size: {fileSize} bytes, extension: {fileExtension}");
                    return resdbResponsep.Data;
                }

                tracingService.Trace($"No msdyn_fileblob found for msdyn_richtextfile {richTextFileId}");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error downloading file from {url}: {ex.Message}");
                throw;
            }
            return null;
        }
        private void UploadFile(IOrganizationService service, Guid DocumentId, string fileName, byte[] fileContent)
        {

            var limit = 4194304;
            var blockIds = new List<string>();
            var initializeFileUploadRequest = new InitializeFileBlocksUploadRequest
            {
                FileAttributeName = "ofm_file",
                Target = new EntityReference("ofm_document", DocumentId),
                FileName = Uri.UnescapeDataString(fileName)
            };
            var fileUploadResponse = (InitializeFileBlocksUploadResponse)service.Execute(initializeFileUploadRequest);
            for (int i = 0; i < Math.Ceiling(fileContent.Length / Convert.ToDecimal(limit)); i++)
            {
                var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
                blockIds.Add(blockId);
                var blockData = fileContent.Skip(i * limit).Take(limit).ToArray();
                var blockRequest = new UploadBlockRequest()
                {
                    FileContinuationToken = fileUploadResponse.FileContinuationToken,
                    BlockId = blockId,
                    BlockData = blockData
                };
                var blockResponse = (UploadBlockResponse)service.Execute(blockRequest);
            }
            var commitRequest = new CommitFileBlocksUploadRequest()
            {
                BlockList = blockIds.ToArray(),
                FileContinuationToken = fileUploadResponse.FileContinuationToken,
                FileName = Uri.UnescapeDataString(fileName),
                MimeType = GetMimeType(fileName),
            };
            service.Execute(commitRequest);





        }
        private string GetMimeType(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            var extension = System.Web.MimeMapping.GetMimeMapping(filename);
            return extension;
        }
        private string GetFileExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
                return "";

            return string.Format("{0}", fileName?.Substring(fileName.LastIndexOf("."))?.ToLower());
        }
    }
}
