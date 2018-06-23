using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace ProfilePattern
{
    public static class ProfilePatternsRead
    {
        //[FunctionName("ProfilePatternRead")]
        //public static async Task<HttpResponseMessage> Run(
        //    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "FundingStreamCode/{id}")]HttpRequestMessage req
        //    , TraceWriter log
        //    , [DocumentDB("FundingPolicy" 
        //                ,"ProfilePattern"
        //                ,ConnectionStringSetting = "CosmosDB"
        //                ,Id ="{id}"
        //                )
        //    ]
        //    IEnumerable<object> documents)
        //{
        //    //,SqlQuery = "SELECT * FROM c where c.FundingStream = {FundingStreamCode} and FundingPeriod={period}" // will require PatternQuery
        //    //Route = "FundingStreamCode/{id}"

        //    log.Info("C# HTTP trigger function processed a request.");

        //    int totalDocuments = documents.Count();
        //    log.Info($"Found {totalDocuments} documents");
        //    if (totalDocuments == 0)
        //    {
        //        return req.CreateResponse(HttpStatusCode.NotFound);
        //    }
        //    return req.CreateResponse(HttpStatusCode.OK, documents);
        //}
        [FunctionName("ProfilePatternRead")]
        public static async Task<HttpResponseMessage> Run(PatternQuery query, HttpRequestMessage req, IEnumerable<dynamic> documents, TraceWriter log)
        {
            int totalDocuments = documents.Count();
            log.Info($"Found {totalDocuments} documents");
            if (totalDocuments == 0)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.OK, documents);
        }

    }


    public class PatternQuery
    {
        public string FundingStreamCode { get; set; }
    }
}
