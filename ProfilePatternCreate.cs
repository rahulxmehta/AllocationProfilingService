using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace ProfilePattern
{
    public static class ProfilePatternCreate
    {
        [FunctionName("ProfilePatternCreate")]

        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req,
  TraceWriter log, IAsyncCollector<object> outputDocument)
        {
            FundingProfilePattern fundingStreamPatternRequest = await req.Content.ReadAsAsync<FundingProfilePattern>();
            log.Verbose("Incoming funding stream code:" + fundingStreamPatternRequest.FundingStreamCode);
            var doc = new FundingProfilePatternDocument(fundingStreamPatternRequest);
            log.Verbose("Outgoing funding stream code::" + doc.FundingStreamCode);
            await outputDocument.AddAsync(doc);
            if (doc.FundingStreamCode != " ")
            {
                return req.CreateResponse(HttpStatusCode.OK, $"{doc.FundingStreamCode} was created");
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest,
                 $"The request was incorrectly formatted.");
            }
        }

        public class BingeRequest
        {
            public string userId { get; set; }
            public string userName { get; set; }
            public string deviceName { get; set; }
            public DateTime dateTime { get; set; }
            public int score { get; set; }
            public bool worthit { get; set; }
        }
        public class BingeDocument : BingeRequest
        {
            public BingeDocument(BingeRequest binge)
            {
                logged = System.DateTime.Now;
                userId = binge.userId;
                userName = binge.userName;
                deviceName = binge.deviceName;
                dateTime = binge.dateTime;
                score = binge.score;
            }
            public DateTime logged { get; set; }
        }

        public class FundingProfilePatternDocument : FundingProfilePattern
        {
            public FundingProfilePatternDocument(FundingProfilePattern pattern)
            {
                logged = System.DateTime.Now;
                id = pattern.id;
                FundingStreamCode = pattern.FundingStreamCode;
                ProfilePatterns = pattern.ProfilePatterns;
            }
            public DateTime logged { get; set; }
        }

        public class FundingProfilePattern
        {
            public string id { get; set; }
            public string FundingStreamCode { get; set; }
            public List<ProfilePattern> ProfilePatterns { get; set; }
        }
        public class ProfilePattern
        {
            public string PeriodType;
            public string Period;
            public int Occurence;
            public string PeriodFY;
            public decimal PeriodPatternPercentage;
        }



        /*
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequestMessage req
            , [Table("person", Connection = "")]ICollector<Person> outTable
            , TraceWriter log)

        {
            dynamic data = await req.Content.ReadAsAsync<object>();
            string name = data?.name;

            if (name == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name in the request body");
            }

            outTable.Add(new Person()
            {
                PartitionKey = "Functions",
                RowKey = Guid.NewGuid().ToString(),
                Name = name
            });
            return req.CreateResponse(HttpStatusCode.Created);
        }

        public class Person : TableEntity
        {
            public string Name { get; set; }
        }
        */
    }
}
