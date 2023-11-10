using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class D365DocumentProvider : ID365DocumentProvider
{ 
    public string EntityNameSet { get => "annotations"; }

    public Task<string> PrepareDocumentBodyAsync(JsonObject jsonData, ID365AppUserService appUserService,ID365WebApiService d365WebApiService)
    {
        // Clean up the json body to only include the required attributes for the OOTB Note Attachment
        string jsonBody = $$"""
                            {
                                "filename": {{jsonData["filename"]}},
                                "subject": {{jsonData["subject"]}},
                                "notetext": {{jsonData["notetext"]}},                                                  
                                "documentbody":{{jsonData["documentbody"]}}
                            }
                            """;

        return Task.FromResult(jsonBody);
    }
}