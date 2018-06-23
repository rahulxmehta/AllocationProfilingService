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
    public static class FundinProfilePatternBindingsGetAll
    {
        [FunctionName("FundinProfilePatternBindingsGetAll")]
        public static async Task<HttpResponseMessage> GetAllProfilePatterns(
             [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "FundingProfilePatterns/FundingStreamPeriod")]HttpRequestMessage req
           , [DocumentDB("FundingPolicy", "FundingStreamPeriodProfilePattern", ConnectionStringSetting = "CosmosDB")] IEnumerable<dynamic> documents
           , TraceWriter log)
        {
            // Set Route - /FundingStream/{FundingStreamCode} and the SQL below
            //Select * from c where c.FundingStreamCode ={FundingStreamCode} 

            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            int totalDocuments = documents.Count();
            log.Info($"Found {totalDocuments} documents");
            if (totalDocuments == 0)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.OK, documents);
        }
    }
}
