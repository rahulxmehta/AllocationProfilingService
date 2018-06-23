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
    public static class FundingProfilePatternBindingsRead
    {
        [FunctionName("FundingProfilePatternBindingsRead")]
        public static async Task<HttpResponseMessage> Run(
              [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "FundingProfilePatterns/FundingStreamPeriod/{id}")]HttpRequestMessage req
            , [DocumentDB("FundingPolicy", "FundingStreamPeriodProfilePattern", ConnectionStringSetting = "CosmosDB", Id ="{id}")] object document
            , TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            if (document ==null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            return req.CreateResponse(HttpStatusCode.OK, document);
        }

        
    }
}
