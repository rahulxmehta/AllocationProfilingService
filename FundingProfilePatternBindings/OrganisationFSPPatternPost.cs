using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace FundingProfilePatternBindings
{
    public static class OrganisationFSPPatternPost
    {
        [FunctionName("OrganisationFSPPatternPost")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "FundingProfilePatterns/OrgFSP")]HttpRequestMessage req
            , [DocumentDB("FundingPolicy", "OrganisationFSPProfilePattern", ConnectionStringSetting = "CosmosDB")] IAsyncCollector<object> outputDocument
            , TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request to create funding profile pattern.");

            OrganisationFundingProfilePattern OrgfundingStreamPeriodPatternRequest = await req.Content.ReadAsAsync<OrganisationFundingProfilePattern>();
            log.Verbose("Incoming OrgID and funding stream period code:" + OrgfundingStreamPeriodPatternRequest.AllocationOrganisation.OrganisationID + "" + OrgfundingStreamPeriodPatternRequest.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode);
            var doc = new OrganisationFundingProfilePatternDocument(OrgfundingStreamPeriodPatternRequest);
            log.Verbose("Outgoing funding stream period code and UKPRN: " + doc.AllocationOrganisation.AlternateOrganisation.Identifier + doc.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode);
            await outputDocument.AddAsync(doc);
            if (doc.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode != " ")
            {
                return req.CreateResponse(HttpStatusCode.OK, $"{doc.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode} for Organisation {doc.AllocationOrganisation.AlternateOrganisation.Identifier} was created");
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest,
                 $"The request was incorrectly formatted.");
            }
        }
    }
    public class Organisation
    {
        public string OrganisationID { get; set; }
        public AlternateOrganisationIdentifier AlternateOrganisation { get; set; }

    }

    public class AlternateOrganisationIdentifier
    {
        public string IdentifierName { get; set; }
        public string Identifier { get; set; }
    }


    public class OrganisationFundingProfilePattern
    {
        public Organisation AllocationOrganisation { get; set; }
        public FundingStreamPeriodProfilePattern FundingStreamPeriodProfilePattern { get; set; }
    }

    public class OrganisationFundingProfilePatternDocument: OrganisationFundingProfilePattern
    {
        public OrganisationFundingProfilePatternDocument(OrganisationFundingProfilePattern OrgPattern)
        {
            loggedDateTime = System.DateTime.Now;
            AllocationOrganisation = OrgPattern.AllocationOrganisation;
            FundingStreamPeriodProfilePattern = OrgPattern.FundingStreamPeriodProfilePattern;
        }

        public DateTime loggedDateTime { get; set; }
    }

}
