using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace FundingProfilePatternBindings
{
    public static class OrganisationFSPPatternRead
    {
        [FunctionName("OrganisationFSPPatternRead")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "FundingProfilePatterns/OrgFSP")]OrgFSPRequest orgFSPRequest
            ,HttpRequestMessage req
            , [DocumentDB("FundingPolicy", "OrganisationFSPProfilePattern", ConnectionStringSetting="CosmosDB",
                            SqlQuery ="SELECT * FROM c where c.AllocationOrganisation.AlternateOrganisation.Identifier = {OrganisationUKPRN} and c.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode = {FundingStreamPeriod}")] IEnumerable<dynamic> documents
            , TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
               
            int totalDocuments = documents.Count();
            log.Info($"Found {totalDocuments} documents");
            if (totalDocuments == 0)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.OK, documents);
        }


    }
    public class OrgFSPRequest
    {
        public string OrganisationUKPRN { get; set; }
        public string FundingStreamPeriod { get; set; }
    }
}
