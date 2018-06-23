using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace FundingProfilePatternBindings
{
    public static class FundingProfilePatternBindingsPost
    {
        [FunctionName("FundingProfilePatternBindingsPost")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "FundingProfilePatterns/FundingStreamPeriod")]FundingStreamPeriodProfilePattern fundingStreamPeriodPatternRequest
            , HttpRequestMessage req
            ,[DocumentDB("FundingPolicy","FundingStreamPeriodProfilePattern",ConnectionStringSetting ="CosmosDB")] IAsyncCollector<object> outputDocument
            , TraceWriter log)
        {
            log.Info($"C# HTTP trigger function processed a request to create funding profile pattern for {fundingStreamPeriodPatternRequest.FundingStreamPeriodCode}");
            var doc = new FundingStreamPeriodProfilePatternDocument(fundingStreamPeriodPatternRequest);
            log.Verbose("Outgoing funding stream code::" + doc.FundingStreamPeriodCode);
            await outputDocument.AddAsync(doc);
            if (doc.FundingStreamPeriodCode != " ")
            {
                return req.CreateResponse(HttpStatusCode.OK, $"{doc.FundingStreamPeriodCode} was created");
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest,
                 $"The request was incorrectly formatted.");
            }
        }
    }


   

    public class FundingStreamPeriodProfilePatternDocument: FundingStreamPeriodProfilePattern
    {
        public FundingStreamPeriodProfilePatternDocument(FundingStreamPeriodProfilePattern pattern)
        {
            logged = System.DateTime.Now;
            id = pattern.FundingStreamPeriodCode;
            FundingStreamPeriodCode= pattern.FundingStreamPeriodCode;
            FundingStreamPeriodStartDate = pattern.FundingStreamPeriodStartDate;
            FundingStreamPeriodEndDate = pattern.FundingStreamPeriodEndDate;
            ReprofilePastPeriods = pattern.ReprofilePastPeriods;
            ProfilePattern = pattern.ProfilePattern;
        }
        public System.DateTime logged { get; set; }
        public string id { get; set; }
    }
    public class ProfilePeriodPattern
    {
        public string PeriodType { get; set; }
        public string Period { get; set; }
        public DateTime PeriodStartDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public DateTime PeriodCutOffDate { get; set; }
        public int Occurrence { get; set; }
        public string DistributionPeriod { get; set; }
        public decimal PeriodPatternPercentage { get; set; }
    }


    public class FundingStreamPeriodProfilePattern
    {
        public string FundingStreamPeriodCode { get; set; }
        public DateTime FundingStreamPeriodStartDate { get; set; }
        public DateTime FundingStreamPeriodEndDate { get; set; }
        public bool ReprofilePastPeriods { get; set; }
        public List<ProfilePeriodPattern> ProfilePattern { get; set; }
    }

    //public class FundingProfilePattern
    //{
    //    public string id { get; set; }
    //    public string FundingStreamCode { get; set; }
    //    public List<ProfilePattern> ProfilePatterns { get; set; }
    //}
    //public class ProfilePattern
    //{
    //    public string PeriodType;
    //    public string Period;
    //    public int Occurence;
    //    public string PeriodFY;
    //    public decimal PeriodPatternPercentage;
    //}
    //public class FundingProfilePatternDocument : FundingProfilePattern
    //{
    //    public FundingProfilePatternDocument(FundingProfilePattern pattern)
    //    {
    //        logged = System.DateTime.Now;
    //        id = pattern.id;
    //        FundingStreamCode = pattern.FundingStreamCode;
    //        ProfilePatterns = pattern.ProfilePatterns;
    //    }
    //    public System.DateTime logged { get; set; }
    //}



}
