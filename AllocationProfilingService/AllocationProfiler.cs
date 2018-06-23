using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Configuration;
using Microsoft.Azure.Documents.Linq;
using Autofac.Features;

namespace AllocatinProfilingService
{
    public static class AllocationProfiler
    {

        public static DocumentClient _client { get; set; }

        [FunctionName("AllocationProfiler")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req,
            [DocumentDB("FundingPolicy", "FundingStreamPeriodProfilePattern", ConnectionStringSetting = "tutorialcosmos_DOCUMENTDB")] DocumentClient client,
            TraceWriter log)
        {
            try
            {

                log.Info("C# HTTP trigger function processed a request.");

                bool allInputAvailable = false;
                string jsonToReturn = string.Empty;

                _client = client;

                Request request = CreateRequestFromQueryString(req);

                if (request.FundingStream == null)
                {
                    // Get request body
                    dynamic data = await req.Content.ReadAsStringAsync();
                    request = JsonConvert.DeserializeObject<Request>(data as string);

                    log.Info("Body " + request.FundingStream + " " + request.AllocationValuesByDistributionPeriod[0].Period);

                }

                if (request.AllocationValuesByDistributionPeriod != null &&
                    request.AllocationValuesByDistributionPeriod.Count != 0 &&
                    request.AllocationStartDate != null &
                    request.AllocationEndDate != null &&
                    request.FundingStream != null &&
                    request.FundingPeriod != null
                    )
                {
                    allInputAvailable = true;
                    jsonToReturn = GetGenericResponse(request, log);
                }

                return allInputAvailable
                     ? new HttpResponseMessage(HttpStatusCode.OK)
                     {
                         Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                     }
                    : req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a funding stream, Fundingperiod , FundingPeriodType,AllocationStartDate, AllocationEndDate and Allocation FY1 Value, FY2 Value or AY Value on the query string or in the request body");

            }
            catch (Exception ex)
            {
                log.Error("Error in getting provider fsp pattern", ex);
                return null;
            }
        }

        public static string GetGenericResponse(Request request,TraceWriter log)
        {
            if (request.LastApprovedProfilePeriods != null)
            {
                log.Info("Last approved profile periods exists- calling re-profiling");
                return GetReProfileResponse(request, log);
            }

            log.Info("Last approved profile periods doesn't exists");
            return GetReponseForAnyDistributionPattern(request,log);

        }
        private static Request CreateRequestFromQueryString(HttpRequestMessage req)
        {
            Request request = new Request
            {
                FundingStream = GetQueryParameterValue(req, "FundingStream"),
                FundingPeriod = GetQueryParameterValue(req, "FundingPeriod"),
                AllocationStartDate = GetQueryParameterValue(req, "AllocationStartDate"),
                AllocationEndDate = GetQueryParameterValue(req, "AllocationEndDate")
            };
            List<RequestPeriodValue> requestPeriodValues = new List<RequestPeriodValue>();
            request.AllocationValuesByDistributionPeriod = requestPeriodValues;
            return request;
        }

        private static string GetQueryParameterValue(HttpRequestMessage req, string parameterName)
        {
            return req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, parameterName, true) == 0)
                .Value;
        }


        private static string GetReponseForAnyDistributionPattern(Request req, TraceWriter log)
        {
            List<ProfilePeriodPattern> patterns = LoadProfilePattern(req.AllocationOrganisation.AlternateOrganisation.Identifier, req.FundingStream, req.FundingPeriod,log);

            string serialised = JsonConvert.SerializeObject(patterns);

            var myObj = new
            {
                AllocationOrganisation = req.AllocationOrganisation,
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods = GetProfiledPeriods(req.AllocationStartDate, req.AllocationEndDate, req.AllocationValuesByDistributionPeriod, patterns, log)
            };

            return JsonConvert.SerializeObject(myObj);
        }

        public static string GetReProfileResponse(Request req, TraceWriter log)
        {
            List<ProfilePeriodPattern> patterns = LoadProfilePattern(req.AllocationOrganisation.AlternateOrganisation.Identifier, req.FundingStream, req.FundingPeriod, log);
            
            DateTime calculationDate = DateTime.ParseExact(req.CalculationDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            //Assumption here that this is not a multiple distribution period
            decimal originalAllocationValue = Convert.ToDecimal(req.AllocationValuesByDistributionPeriod.First().AllocationValue);
            string allocationDistributionPeriod = req.AllocationValuesByDistributionPeriod.FirstOrDefault().Period;
            var revisedAllocationValuesByDistributionPeriod = GetRevisedAllocationValueRequest(calculationDate, originalAllocationValue, allocationDistributionPeriod, req.LastApprovedProfilePeriods, patterns);
            List <RequestPeriodValue> revisedAllocValsByDistPeriod= new List<RequestPeriodValue>() { revisedAllocationValuesByDistributionPeriod };

            var myObj = new
            {
                AllocationOrganisation = req.AllocationOrganisation,
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods= GetReProfiledPeriods(calculationDate, revisedAllocValsByDistPeriod, req.LastApprovedProfilePeriods, patterns,log)
            };

            return JsonConvert.SerializeObject(myObj);
        }

        private static List<AllocationProfilePeriod> GetProfiledPeriods(string reqAllocationStartDate, string reqAllocationEndDate, List<RequestPeriodValue> allocationValuesByPeriod, List<ProfilePeriodPattern> patterns, TraceWriter log)
        {
            DateTime allocationStartDate = DateTime.ParseExact(reqAllocationStartDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            DateTime allocationEndDate = DateTime.ParseExact(reqAllocationEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            List<AllocationProfilePeriod> allocationProfilePeriods = GetProfilePeriodsForAllocation(allocationStartDate, allocationEndDate, patterns, log);
            return ApplyProfilePattern(allocationValuesByPeriod, patterns, allocationProfilePeriods, log);
        }


        private static List<AllocationProfilePeriod> GetReProfiledPeriods(DateTime calculationDate, List<RequestPeriodValue> revisedAllocationPeriodValue,List<AllocationProfilePeriod> pastAllocationProfilePeriods, List<ProfilePeriodPattern> patterns, TraceWriter log)
        {

            List<AllocationProfilePeriod> pastProfilePeriods = GetPastPeriodsFromPreviousAllocationProfile(calculationDate, pastAllocationProfilePeriods, patterns);
            List<AllocationProfilePeriod> periodsToProfile = GetAllocationPeriodsToBeProfiled(calculationDate, patterns, log);
            var reProfiledPeriods = ApplyProfilePattern(revisedAllocationPeriodValue, patterns, periodsToProfile, log);
            pastProfilePeriods.AddRange(reProfiledPeriods);
            return pastProfilePeriods;
        }
        
        private static RequestPeriodValue GetRevisedAllocationValueRequest(DateTime calculationDate, decimal originalAllocationValue, string allocationDistributionPeriod, List<AllocationProfilePeriod> pastProfilePeriods, List<ProfilePeriodPattern> profilePatterns)
        {
            return new RequestPeriodValue()
            {
                Period = allocationDistributionPeriod,
                AllocationValue = GetRevisedAllocationValueToProfile(calculationDate, pastProfilePeriods, profilePatterns, originalAllocationValue).ToString()
            
            };
        }

        private static List<AllocationProfilePeriod> GetProfilePeriodsForAllocation(DateTime allocationStartDate,
                                                                          DateTime allocationEndDate,
                                                                          List<ProfilePeriodPattern> profilePattern,
                                                                          TraceWriter log
                                                                         )
        {
         
            List<AllocationProfilePeriod> profilePeriods = new List<AllocationProfilePeriod>();
            
            foreach (ProfilePeriodPattern period in GetPatternPeriodsWithinAllocation(allocationStartDate, allocationEndDate, profilePattern))
            {

                profilePeriods.Add(new AllocationProfilePeriod
                {
                    Period = period.Period,
                    Occurence = period.Occurrence,
                    PeriodYear = period.PeriodEndDate.Year,
                    DistributionPeriod = period.DistributionPeriod,
                    PeriodType = period.PeriodType,
                });
            }


            return profilePeriods;
        }

        private static List<AllocationProfilePeriod> GetAllocationPeriodsToBeProfiled(DateTime currentDateTime,
                                                                         List<ProfilePeriodPattern> profilePatterns,
                                                                         TraceWriter log
                                                                        )
        {
            List<AllocationProfilePeriod> profilePeriods = new List<AllocationProfilePeriod>();

            foreach (ProfilePeriodPattern period in GetFutureProfilePatternsForAllocation(currentDateTime, profilePatterns))
            {

                profilePeriods.Add(new AllocationProfilePeriod
                {
                    Period = period.Period,
                    Occurence = period.Occurrence,
                    PeriodYear = period.PeriodEndDate.Year,
                    DistributionPeriod = period.DistributionPeriod,
                    PeriodType = period.PeriodType,
                });
            }


            return profilePeriods;
        }

        private static List<ProfilePeriodPattern> GetPatternPeriodsWithinAllocation(DateTime allocationStartDate,
                                                                         DateTime allocationEndDate,
                                                                         List<ProfilePeriodPattern> profilePattern)
        {
            // find the pattern periods between allocation start date and allocation end date
            return profilePattern.Where(p => p.PeriodEndDate >= allocationStartDate && p.PeriodStartDate <= allocationEndDate).ToList();
        }

        private static decimal GetRevisedAllocationValueToProfile(DateTime currentDateTime,List<AllocationProfilePeriod> previousAllocationProfilePeriods, 
                                                                    List<ProfilePeriodPattern> profilePatterns, decimal revisedAllocationValue)
        {
            decimal pastPeriodTotal = GetPastPeriodsFromPreviousAllocationProfile(currentDateTime, previousAllocationProfilePeriods, profilePatterns).Sum(p => p.ProfileValue);
            return revisedAllocationValue - pastPeriodTotal;
        }

        private static List<ProfilePeriodPattern> GetFutureProfilePatternsForAllocation(DateTime currentDateTime, List<ProfilePeriodPattern> profilePatterns)
        {
            // find the pattern periods where cut-off date is less than the current date
            return profilePatterns.Where(p => p.PeriodCutOffDate > currentDateTime).ToList();
        }

        private static List<AllocationProfilePeriod> GetPastPeriodsFromPreviousAllocationProfile(DateTime currentDateTime,
                                                                         List<AllocationProfilePeriod> allocationProfilePeriods, List<ProfilePeriodPattern> profilePatterns)
        {
            // find the pattern periods where cut-off date is less than the current date
            var pastPeriodPatterns= profilePatterns.Where(p => p.PeriodCutOffDate<=currentDateTime).ToList();
            return GetProfilePeriodsMatchingPatternPeriods(allocationProfilePeriods, pastPeriodPatterns);
        }

        private static List<AllocationProfilePeriod> GetProfilePeriodsMatchingPatternPeriods(List<AllocationProfilePeriod> allocationProfilePeriods, List<ProfilePeriodPattern> profilePatterns)
        {
            List<AllocationProfilePeriod> matchedProfilePeriods = new List<AllocationProfilePeriod>();
            foreach(AllocationProfilePeriod period in allocationProfilePeriods)
            {
                foreach(ProfilePeriodPattern patternPeriod in profilePatterns)
                {
                    if (period.Period == patternPeriod.Period && period.PeriodType==patternPeriod.PeriodType && period.Occurence==patternPeriod.Occurrence && period.PeriodYear == patternPeriod.PeriodEndDate.Year)
                    {
                        matchedProfilePeriods.Add(period);
                        break;
                    }
                }
            }
            return matchedProfilePeriods;
        }

        private static List<AllocationProfilePeriod> ApplyProfilePattern( List<RequestPeriodValue> allocationValuesByPeriod,
                                                                List<ProfilePeriodPattern> profilePatterns,
                                                                List<AllocationProfilePeriod> profilePeriods,
                                                                TraceWriter log
                                                              )
        {
            List<PercentageByDistibutionPeriod> percentagebyPeriods = GetTotalPercentageByDistibutionPeriods(profilePatterns,profilePeriods);

            foreach (RequestPeriodValue requestPeriod in allocationValuesByPeriod)
            {
                decimal? totalPercentage = percentagebyPeriods.FirstOrDefault(q => q.Period == requestPeriod.Period)?.TotalProfilePercentage;
                
                decimal allocationValueToBeProfiled = Convert.ToDecimal(requestPeriod.AllocationValue);
                
                var profilePeriodWithMatchingDp = profilePeriods.FindAll(pp => pp.DistributionPeriod == requestPeriod.Period);

                for (int i = 0; i < profilePeriodWithMatchingDp.Count; i++)
                {
                    AllocationProfilePeriod profilePeriod = profilePeriodWithMatchingDp[i];
                    var profilePercentage = profilePatterns.FirstOrDefault(
                                                                            q => q.Period == profilePeriod.Period &&
                                                                            q.DistributionPeriod == profilePeriod.DistributionPeriod &&
                                                                            q.Occurrence == profilePeriod.Occurence
                                                                            )
                                                                            .PeriodPatternPercentage;

                    profilePeriod.ProfileValue = (profilePercentage / totalPercentage.GetValueOrDefault()) * allocationValueToBeProfiled;
                    profilePeriod.ProfileValue = decimal.Round(profilePeriod.ProfileValue, 2, MidpointRounding.AwayFromZero);
                }
                if (profilePeriodWithMatchingDp.Count > 0)
                {
                    decimal roundingValue = allocationValueToBeProfiled - profilePeriodWithMatchingDp.Sum(p => p.ProfileValue);
                    if (roundingValue > 0 || roundingValue <0 )
                        profilePeriodWithMatchingDp.FirstOrDefault(p => p.ProfileValue !=0).ProfileValue += roundingValue;
                    
                }
            }
            return profilePeriods;
        }

        
        private static List<PercentageByDistibutionPeriod> GetTotalPercentageByDistibutionPeriods(List<ProfilePeriodPattern> profilePatterns, List<AllocationProfilePeriod> profilePeriods)
        {
            List<PercentageByDistibutionPeriod> percentageByDistPeriods = new List<PercentageByDistibutionPeriod>();

            var uniqueDistributionPeriods = profilePatterns.Select(pattern => pattern.DistributionPeriod).Distinct().ToList();

            foreach(var distributionperiod in uniqueDistributionPeriods)
            {
                percentageByDistPeriods.Add(GetTotalPercentageForDistributionPeriod(profilePatterns, profilePeriods, distributionperiod));
            }
            
            return percentageByDistPeriods;
        }


        private static PercentageByDistibutionPeriod GetTotalPercentageForDistributionPeriod(List<ProfilePeriodPattern> profilePatterns, List<AllocationProfilePeriod> profilePeriods, string distributionPeriod)
        {
            List<ProfilePeriodPattern> profilePatternsForDp = profilePatterns.Where(q => q.DistributionPeriod == distributionPeriod).ToList();
            List<ProfilePeriodPattern> matchedPatterns = GetMatchingProfilePatternsWithProfilePeriods(profilePeriods, profilePatternsForDp);

            return new PercentageByDistibutionPeriod { Period = distributionPeriod, TotalProfilePercentage = matchedPatterns.Sum(p => p.PeriodPatternPercentage) };
        }

       
        private static List<ProfilePeriodPattern> GetMatchingProfilePatternsWithProfilePeriods(List<AllocationProfilePeriod> periods, List<ProfilePeriodPattern> patterns)
        {
            List<ProfilePeriodPattern> matchedPatterns = new List<ProfilePeriodPattern>();
            foreach (ProfilePeriodPattern pattern in patterns)
            {
                foreach (AllocationProfilePeriod period in periods)
                {
                    if (pattern.Period == period.Period && pattern.DistributionPeriod == period.DistributionPeriod) //TODO: Perhaps more checks needed- year? Occurence?
                    {
                        matchedPatterns.Add(pattern);
                        break;
                    }
                }
            }
            return matchedPatterns;
        }

        private static string GetFundingStreamPeriod(string fundingStream, string fundingPeriod)
        {
            return fundingStream.ToUpper() + fundingPeriod.Replace("20", "").Replace("-", "");
        }

        private static List<ProfilePeriodPattern> LoadProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod,TraceWriter log)
        {
            // Get Provider Profile Pattern for an FSP, if not get for FS for the provider
            // if not available then get one for FSP
            // if not available then get for FS ToDo

            List<ProfilePeriodPattern> pattern = FromCofigGetProviderFspProfilePattern(providerIdentifier, fundingStream, fundingPeriod,log);
            return pattern ?? FromCofigGetFspProfilePattern(providerIdentifier, fundingStream, fundingPeriod,log);

        }

        private static List<ProfilePeriodPattern> FromCofigGetFspProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod, TraceWriter log)
        {
            log.Info("Getting profile periods for allocation at FSP Level ...");

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("FundingPolicy", "FundingStreamPeriodProfilePattern");

            IDocumentQuery<FundingStreamPeriodProfilePattern> query = _client.CreateDocumentQuery<FundingStreamPeriodProfilePattern>(collectionUri)
                .Where(p => p.FundingStreamPeriodCode == GetFundingStreamPeriod(fundingStream, fundingPeriod))
                .AsDocumentQuery();

            while (query.HasMoreResults)
            {
                foreach (FundingStreamPeriodProfilePattern result in query.ExecuteNextAsync().Result)
                {
                    log.Info("returning profile pattern at FSP Level");
                    return result.ProfilePattern;

                }
            }
            return null;
            //return FromConfigGetNonLevy1618AppsProfilePattern();
        }

        private static List<ProfilePeriodPattern> FromCofigGetProviderFspProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod, TraceWriter log)
        {
            
            log.Info($"Getting profile periods for allocation at Provider FSP Level ...{fundingStream}, {fundingPeriod}, {providerIdentifier}");

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("FundingPolicy", "OrganisationFSPProfilePattern");

               
            IDocumentQuery<OrganisationFundingProfilePattern> query = _client.CreateDocumentQuery<OrganisationFundingProfilePattern>(collectionUri)
                .Where(p => p.FundingStreamPeriodProfilePattern.FundingStreamPeriodCode == GetFundingStreamPeriod(fundingStream, fundingPeriod) && p.AllocationOrganisation.AlternateOrganisation.Identifier == providerIdentifier)
                .AsDocumentQuery();

            
            while (query.HasMoreResults)
            {
            
                foreach (OrganisationFundingProfilePattern result in query.ExecuteNextAsync().Result)
                {
                    return result.FundingStreamPeriodProfilePattern.ProfilePattern;

                }
            }
            return null;
            
            
            //return FromConfigGetNonLevy1618AppsProfilePattern();
        }

        private static List<ProfilePeriodPattern> FromConfigGetNonLevy1618AppsProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2018,1,1), PeriodEndDate=new DateTime(2018,1,31), Occurrence = 1, DistributionPeriod="2018-19", PeriodPatternPercentage = 0.44M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2018,2,1), PeriodEndDate=new DateTime(2018,2,28), Occurrence = 1, DistributionPeriod="2018-19", PeriodPatternPercentage = 0.80M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2018,3,1), PeriodEndDate=new DateTime(2018,3,31), Occurrence = 1, DistributionPeriod="2018-19", PeriodPatternPercentage = 1.12M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 4.61M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2018,5,1), PeriodEndDate=new DateTime(2018,5,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 4.57M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2018,6,1), PeriodEndDate=new DateTime(2018,6,30), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 4.67M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2018,7,1), PeriodEndDate=new DateTime(2018,7,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 4.76M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="2019-20", PeriodPatternPercentage = 5.21M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 7.91M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 9.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 8.69M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 13.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 12.24M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 11.05M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="2019-20", PeriodPatternPercentage = 10.03M }
            };

            return profilePatterns;
        }
        
    }


    public class Request
    {
        public Organisation AllocationOrganisation { get; set; }
        public string FundingStream { get; set; }
        public string FundingPeriod { get; set; }
        public string AllocationStartDate { get; set; }
        public string AllocationEndDate { get; set; }

        public string CalculationDate { get; set; }
        public List<RequestPeriodValue> AllocationValuesByDistributionPeriod { get; set; }
        public List<AllocationProfilePeriod> LastApprovedProfilePeriods { get; set; }
    }

    public class Response
    {
        public string FundingStream { get; set; }
        public string FundingPeriod { get; set; }
        public string AllocationStartDate { get; set; }
        public string AllocationEndDate { get; set; }
        public List<RequestPeriodValue> AllocationValuesByDistributionPeriod { get; set; }
        public List<AllocationProfilePeriod> ProfilePeriods { get; set; }
    }

    public class RequestPeriodValue
    {
        public string Period { get; set; }
        public string AllocationValue { get; set; }
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

    public class AllocationProfilePeriod
    {
        public string Period { get; set; }
        public int Occurence { get; set; }
        public int PeriodYear { get; set; }
        public string PeriodType { get; set; }
        public decimal ProfileValue { get; set; }
        public string DistributionPeriod { get; set; }
    }

    public class Organisation
    {
        public string OrganisationId { get; set; }
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

    public class FundingStreamPeriodProfilePattern
    {
        public string FundingStreamPeriodCode { get; set; }
        public DateTime FundingStreamPeriodStartDate { get; set; }
        public DateTime FundingStreamPeriodEndDate { get; set; }
        public List<ProfilePeriodPattern> ProfilePattern { get; set; }
    }

    public class FundingStreamProfilePattern
    {
        public string FundingStream { get; set; }
        public string DistributionPeriodType { get; set; }
        public List<ProfilePeriodPattern> ProfilePattern { get; set; }
    }



    public class PercentageByDistibutionPeriod
    {
        public string Period { get; set; }
        public decimal TotalProfilePercentage { get; set; }
    }

    #region commented code
    /*
     *  private static string GetDistributionPeriodForSinglePeriod(DateTime startDate, DateTime endDate)
        {
            return startDate.Year.ToString() + "-" + endDate.Year.ToString();
        }

        private static List<ProfilePeriodPattern> TransformProfilePatternDistributionPeriod(List<ProfilePeriodPattern> profilePatterns, 
                                                                                    DateTime fundingStreamPeriodStartDate, 
                                                                                    DateTime fundingStreamPeriodEndDate)
        {
            int countDisributionPeriods = profilePatterns.Select(pattern => pattern.DistributionPeriod).Distinct().Count();
            if (countDisributionPeriods == 1)
                return TransformProfilePatternDistributionPeriodForSinglePeriod(profilePatterns,
                                                                                   fundingStreamPeriodStartDate,
                                                                                   fundingStreamPeriodEndDate);

            return TransformProfilePatternFY(profilePatterns, fundingStreamPeriodStartDate, fundingStreamPeriodEndDate);
           
        }

        private static List<ProfilePeriodPattern> TransformProfilePatternDistributionPeriodForSinglePeriod(List<ProfilePeriodPattern> profilePatterns, DateTime fundingStreamPeriodStartDate, DateTime fundingStreamPeriodEndDate)
        {
            profilePatterns.Where(q => q.DistributionPeriod == "DP1")
                           .ToList()
                           .ForEach(s => s.DistributionPeriod = GetDistributionPeriodForSinglePeriod(fundingStreamPeriodStartDate,fundingStreamPeriodEndDate));
            return profilePatterns;
        }


        private static List<ProfilePeriodPattern> TransformProfilePatternFY(List<ProfilePeriodPattern> profilePatterns, DateTime fundingStreamPeriodStartDate, DateTime fundingStreamPeriodEndDate)
        {
            string fy1 = GetFY(fundingStreamPeriodStartDate);
            string fy2 = GetFY(fundingStreamPeriodEndDate);

            profilePatterns.Where(q => q.DistributionPeriod == "DP1").ToList().ForEach(s => s.DistributionPeriod = fy1);
            profilePatterns.Where(q => q.DistributionPeriod == "DP2").ToList().ForEach(s => s.DistributionPeriod = fy2);

            return profilePatterns;
        }

        private static int GetMonthDifference(DateTime startDate, DateTime endDate)
        {
            int monthsApart = 12 * (startDate.Year - endDate.Year) + startDate.Month - endDate.Month - 1;
            return Math.Abs(monthsApart);
        }


        private static string GetFY(DateTime dt)
        {
            if (dt.Month > 3)
                return (dt.Year + "-" + (dt.Year + 1));
            else
                return ((dt.Year - 1) + "-" + dt.Year);
        }

      private static List<AllocationProfilePeriod> GetProfilePeriods(string[] periods, int[] years,List<ProfilePeriodPattern> profilePattern, string profilePeriodType)
        {
            List<AllocationProfilePeriod> profilePeriods = new List<AllocationProfilePeriod>();
            for (int i = 0; i < periods.Length; i++)
            {
                int occurence = profilePattern.SingleOrDefault(q => q.Period == periods[i]).Occurrence;

                for (int x = 1; x <= occurence; x++)
                {
                    string disributionPeriod = profilePattern.SingleOrDefault(q => q.Period == periods[i] && q.Occurrence == x).DistributionPeriod;
                    profilePeriods.Add(new AllocationProfilePeriod
                                                        {
                                                            Period = periods[i],
                                                            DistributionPeriod = disributionPeriod,
                                                            Occurence = x,
                                                            PeriodYear = years[i],
                                                            PeriodType = profilePeriodType
                                                        }
                                        );
                }
            }

            return profilePeriods;
        }

     private static List<AllocationProfilePeriod> GetProfilePeriodsForAllocation(string allocationStartDate,
                                                                          string allocationEndDate,
                                                                          List<ProfilePeriodPattern> profilePattern
                                                                         )
        {
            DateTime dtStart = DateTime.ParseExact(allocationStartDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(allocationEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
           
            //string profilePeriodType = profilePattern.Select(q => q.PeriodType).Distinct().FirstOrDefault();

            List<AllocationProfilePeriod> profilePeriods = new List<AllocationProfilePeriod>();

            if (profilePeriodType == "CalendarMonth" || profilePeriodType == "Quarterly") 
            {
                 string[] periodMonths = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                int startYear = dtStart.Year;
                int index = dtStart.Month - 1;

                for (int i = 0; i < GetMonthDifference(dtStart, dtEnd); i++)
                {
                    if (index == periodMonths.Length)
                    {
                        index = 0;
                        startYear += 1;
                    }
                    DateTime dtTempDate = DateTime.ParseExact("01/" + (index + 1) + "/" + startYear, "dd/M/yyyy", CultureInfo.InvariantCulture);

                    int occurence = profilePattern.Where(q => q.Period == periodMonths[index] && q.DistributionPeriod==GetFY(dtTempDate))
                                                  .Max(q=>q.Occurrence);
                    
                    for (int x = 1; x <= occurence; x++)
                    {
                        profilePeriods.Add(new AllocationProfilePeriod {
                                                    Period = periodMonths[index],
                                                    Occurence = x,
                                                    PeriodYear = startYear,
                                                    DistributionPeriod = GetFY(dtTempDate),
                                                    PeriodType = profilePeriodType
                                                    });
                    }
                    index++;
                }
                
            }
            else if (profilePeriodType=="Quarterly") 
            {
                return GetProfilePeriods(new string[] { "Q1", "Q2", "Q3", "Q4" }, 
                                        new int[] { dtStart.Year, dtStart.Year, dtStart.Year, dtEnd.Year }, 
                                        profilePattern, 
                                        profilePeriodType);
            }
            else if (profilePeriodType == "HalfYearly") //Assumption that distribution period is single and no short allocation
            {
                return GetProfilePeriods(new string[] { "H1", "H2" }, 
                                         new int[] { dtStart.Year, dtEnd.Year }, 
                                         profilePattern, 
                                         profilePeriodType);
            }

            return profilePeriods;
        }

    private static void GetMonthlyAllocationPeriods(string allocationStartDate,
                                                                          string allocationEndDate,
                                                                          List<ProfilePeriodPattern> profilePattern)
    {
               
    }
    private static DateTime FromConfigGetFSPStartDate(string fundingStream, string fundingPeriod)
        {
            string startDate = string.Empty;
            switch (fundingStream.ToUpper())
            {
                case "NONLEVY1618":
                case "NONLEVYADULT":
                    startDate = "01/01/2018";
                    break;
                case "DSG":
                case "PUPILPREMIUM":
                    startDate = "01/04/2018";
                    break;
                default:
                    startDate = "01/08/2018";
                    break;
            }
            DateTime dtTempDate = DateTime.ParseExact(startDate, "dd/M/yyyy", CultureInfo.InvariantCulture);

            return dtTempDate;
        }

        private static DateTime FromConfigGetFSPEndDate(string fundingStream, string fundingPeriod)
        {
            string tempDate = string.Empty;
            switch (fundingStream.ToUpper())
            {
                case "NONLEVY1618":
                case "NONLEVYADULT":
                    tempDate = "31/03/2019";
                    break;
                case "DSG":
                case "PUPILPREMIUM":
                    tempDate = "31/03/2019";
                    break;
                default:
                    tempDate = "31/07/2019";
                    break;
            }
            DateTime dtTempDate = DateTime.ParseExact(tempDate, "dd/M/yyyy", CultureInfo.InvariantCulture);

            return dtTempDate;
        }
       
        private static List<ProfilePeriodPattern> FromConfigGetNonLevyAdultAppsProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2018,1,1), PeriodEndDate=new DateTime(2018,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 1.07M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2018,2,1), PeriodEndDate=new DateTime(2018,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 2.03M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2018,3,1), PeriodEndDate=new DateTime(2018,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 3.06M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 4.34M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2018,5,1), PeriodEndDate=new DateTime(2018,5,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 5.05M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2018,6,1), PeriodEndDate=new DateTime(2018,6,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 5.85M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2018,7,1), PeriodEndDate=new DateTime(2018,7,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 6.59M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP2", PeriodPatternPercentage = 6.99M },
                new ProfilePeriodPattern {PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 7.76M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.37M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.76M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 9.06M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 9.99M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 10.36M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 10.72M }
            };

            return profilePatterns;
        }

        private static List<ProfilePeriodPattern> FromConfigGetPupilPremiumProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "Quarterly", Period = "Q1", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 25M },
                new ProfilePeriodPattern { PeriodType = "Quarterly", Period = "Q2", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 25M },
                new ProfilePeriodPattern { PeriodType = "Quarterly", Period = "Q3", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 35M },
                new ProfilePeriodPattern { PeriodType = "Quarterly", Period = "Q4", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 15M }
            };

            return profilePatterns;
        }

        private static List<ProfilePeriodPattern> FromConfigGetPESportPremiumPattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "HalfYearly", Period = "H1", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 58.33M },
                new ProfilePeriodPattern { PeriodType = "HalfYearly", Period = "H2", Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 41.67M }
            };


            return profilePatterns;
        }
        private static List<ProfilePeriodPattern> FromConfigGetNonLevy1618AppsProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2018,1,1), PeriodEndDate=new DateTime(2018,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 0.44M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2018,2,1), PeriodEndDate=new DateTime(2018,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 0.80M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2018,3,1), PeriodEndDate=new DateTime(2018,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 1.12M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 4.61M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2018,5,1), PeriodEndDate=new DateTime(2018,5,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 4.57M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2018,6,1), PeriodEndDate=new DateTime(2018,6,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 4.67M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2018,7,1), PeriodEndDate=new DateTime(2018,7,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 4.76M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP2", PeriodPatternPercentage = 5.21M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 7.91M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 9.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.69M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 13.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 12.24M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 11.05M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 10.03M }
            };

            return profilePatterns;
        }
        private static List<ProfilePeriodPattern> FromConfigGetSSFProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP1", PeriodPatternPercentage = 5.21M },
                new ProfilePeriodPattern {PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 7.91M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 9.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.69M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 13.95M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 0.44M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 0.80M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 1.12M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2019,4,1), PeriodEndDate=new DateTime(2019,4,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4.61M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2019,5,1), PeriodEndDate=new DateTime(2019,5,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4.57M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2019,6,1), PeriodEndDate=new DateTime(2019,6,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4.67M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2019,7,1), PeriodEndDate=new DateTime(2019,7,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4.76M }
            };

            return profilePatterns;

        }
        private static List<ProfilePeriodPattern> FromConfigGet1618AppsProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern {PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.3M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.35M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2019,4,1), PeriodEndDate=new DateTime(2019,4,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2019,5,1), PeriodEndDate=new DateTime(2019,5,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2019,6,1), PeriodEndDate=new DateTime(2019,6,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2019,7,1), PeriodEndDate=new DateTime(2019,7,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.35M }
            };

            return profilePatterns;
        }
        private static List<ProfilePeriodPattern> FromConfigGetAdultSkillsProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 14.44M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.58M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.67M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 7.08M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 5.69M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 7.44M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 5.39M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 5.36M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2019,4,1), PeriodEndDate=new DateTime(2019,4,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 12.69M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2019,5,1), PeriodEndDate=new DateTime(2019,5,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 10.21M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2019,6,1), PeriodEndDate=new DateTime(2019,6,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.70M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2019,7,1), PeriodEndDate=new DateTime(2019,7,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 5.75M }
            };

            return profilePatterns;
        }
        private static List<ProfilePeriodPattern> FromConfigGetCommunityLearningProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 8.36M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2019,4,1), PeriodEndDate=new DateTime(2019,4,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2019,5,1), PeriodEndDate=new DateTime(2019,5,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2019,6,1), PeriodEndDate=new DateTime(2019,6,30), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.33M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2019,7,1), PeriodEndDate=new DateTime(2019,7,31), Occurrence = 1, DistributionPeriod="DP2", PeriodPatternPercentage = 8.34M }
            };

            return profilePatterns;
        }

        private static List<ProfilePeriodPattern> FromConfigGetDSGProfilePattern()
        {
            List<ProfilePeriodPattern> profilePatterns = new List<ProfilePeriodPattern>
            {
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Apr", PeriodStartDate=new DateTime(2018,4,1), PeriodEndDate=new DateTime(2018,4,30), Occurrence = 3, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2018,5,1), PeriodEndDate=new DateTime(2018,5,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "May", PeriodStartDate=new DateTime(2018,5,1), PeriodEndDate=new DateTime(2018,5,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2018,6,1), PeriodEndDate=new DateTime(2018,6,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jun", PeriodStartDate=new DateTime(2018,6,1), PeriodEndDate=new DateTime(2018,6,30), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2018,7,1), PeriodEndDate=new DateTime(2018,7,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jul", PeriodStartDate=new DateTime(2018,7,1), PeriodEndDate=new DateTime(2018,7,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 1,DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Aug", PeriodStartDate=new DateTime(2018,8,1), PeriodEndDate=new DateTime(2018,8,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Sep", PeriodStartDate=new DateTime(2018,9,1), PeriodEndDate=new DateTime(2018,9,30), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Oct", PeriodStartDate=new DateTime(2018,10,1), PeriodEndDate=new DateTime(2018,10,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Nov", PeriodStartDate=new DateTime(2018,11,1), PeriodEndDate=new DateTime(2018,11,30), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Dec", PeriodStartDate=new DateTime(2018,12,1), PeriodEndDate=new DateTime(2018,12,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Jan", PeriodStartDate=new DateTime(2019,1,1), PeriodEndDate=new DateTime(2019,1,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Feb", PeriodStartDate=new DateTime(2019,2,1), PeriodEndDate=new DateTime(2019,2,28), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 1, DistributionPeriod="DP1", PeriodPatternPercentage = 4M },
                new ProfilePeriodPattern { PeriodType = "CalendarMonth", Period = "Mar", PeriodStartDate=new DateTime(2019,3,1), PeriodEndDate=new DateTime(2019,3,31), Occurrence = 2, DistributionPeriod="DP1", PeriodPatternPercentage = 4M }
            };


            return profilePatterns;
        }
        

    private static List<ProfilePeriodPattern> LoadProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod)
        {

            DateTime fspStartDate = FromConfigGetFSPStartDate(fundingStream, fundingPeriod);
            DateTime fspEndDate = FromConfigGetFSPEndDate(fundingStream, fundingPeriod);

           

            if (fundingStream.ToUpper() == "AEB")
                return TransformProfilePatternDistributionPeriod(FromConfigGetAdultSkillsProfilePattern(),
                                                fspStartDate,
                                                fspEndDate
                                                );
            else if (fundingStream.ToUpper() == "1618APPS")
                return TransformProfilePatternDistributionPeriod(FromConfigGet1618AppsProfilePattern(),
                                                    fspStartDate,
                                                    fspEndDate
                                                );
            else if (fundingStream.ToUpper() == "NONLEVY1618")
                return TransformProfilePatternDistributionPeriod(
                                                FromConfigGetNonLevy1618AppsProfilePattern(),
                                                fspStartDate,
                                                fspEndDate
                                                );
            else if (fundingStream.ToUpper() == "CL")
            return TransformProfilePatternDistributionPeriod(FromConfigGetCommunityLearningProfilePattern(),
                                                fspStartDate,
                                                fspEndDate
                                                );
            else if (fundingStream.ToUpper() == "DSG")
                    return TransformProfilePatternDistributionPeriod(FromConfigGetDSGProfilePattern(), fspStartDate, fspEndDate);
            
            else if (fundingStream.ToUpper() == "NONLEVYADULT")
                return TransformProfilePatternDistributionPeriod(FromConfigGetNonLevyAdultAppsProfilePattern(), fspStartDate, fspEndDate);

            else if (fundingStream.ToUpper() == "PUPILPREMIUM")
                return TransformProfilePatternDistributionPeriod(FromConfigGetPupilPremiumProfilePattern(),
                                                    fspStartDate,
                                                fspEndDate
                                                );

            else if (fundingStream.ToUpper() == "PESPORTPREMIUM")
                return TransformProfilePatternDistributionPeriod(FromConfigGetPESportPremiumPattern(),
                                                    fspStartDate,
                                                fspEndDate
                                                );
            else if (fundingStream.ToUpper() == "SSF")
                return TransformProfilePatternDistributionPeriod(FromConfigGetSSFProfilePattern(),
                                                fspStartDate,
                                                fspEndDate
                                                );

            else
                return null;
        }

        public class ProfilePeriodBreakdown
        {
            public string PeriodType { get; set; }
            public string Period { get; set; }
            public int Occurrence { get; set; }
        }
        private static string GetReponseForSingleEnvelopeAllocation(Request req)
        {
            var myObj = new
            {
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                FundingPeriodType = req.FundingPeriodType,
                FundingDistributionPeriodType = req.FundingDistributionPeriodType,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods = ApplySingleEnvelopeProfilePattern(req.FundingStream,
                                                        req.FundingPeriod,
                                                        req.AllocationValuesByDistributionPeriod,
                                                        GetProfilePeriodsForAllocation(
                                                                                    req.AllocationStartDate,
                                                                                    req.AllocationEndDate,
                                                                                    req.FundingStream,
                                                                                    req.FundingPeriod
                                                    ))
            };

            return JsonConvert.SerializeObject(myObj);
        }

        private static string GetReponseForMultipleEnvelopeAllocation(Request req)
        {
            var myObj = new
            {
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                FundingPeriodType = req.FundingPeriodType,
                FundingDistributionPeriodType = req.FundingDistributionPeriodType,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods = ApplyMultipleEnvelopeProfilePattern(req.FundingStream,
                                                        req.FundingPeriod,
                                                        req.AllocationValuesByDistributionPeriod,
                                                        GetProfilePeriodsForAllocation(
                                                                                    req.AllocationStartDate,
                                                                                    req.AllocationEndDate,
                                                                                    req.FundingStream,
                                                                                    req.FundingPeriod
                                                    ))
            };

            return JsonConvert.SerializeObject(myObj);
        }

    public static string  GetResponse(Request request)
        {
            request.FundingDistributionPeriodType = FromConfigGetFundingDistributionPeriodType(request.FundingStream, request.FundingPeriod);
            request.FundingPeriodType = FromConfigGetFundingPeriodType(request.FundingStream, request.FundingPeriod);

            if (request.FundingDistributionPeriodType == "SinglePeriod")
                return GetReponseForSingleEnvelopeAllocation(request);
            else return GetReponseForMultipleEnvelopeAllocation(request);
        }

     private static string FromConfigGetProfilePeriodType(string fundingstream, string fundingPeriod)
        {
            switch (fundingstream.ToUpper())
            {
                case "PESPORTPREMIUM":
                    return "HalfYearly";
                case "PUPILPREMIUM":
                    return "Quarterly";
                case "DSG":
                    return "DSG";
                default:
                    return "CalendarMonth";
            }
        }
    private static List<ProfilePeriodBreakdown> FromConfigGetProfilePeriodBreakdown(string profilePeriodType)
    {
        List<ProfilePeriodBreakdown> profilePeriodBreakdowns = new List<ProfilePeriodBreakdown>();

        switch (profilePeriodType)
        {
            case "DSG":
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Apr", Occurrence = 3 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "May", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jun", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jul", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Aug", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Sep", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Oct", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Nov", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Dec", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jan", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Feb", Occurrence = 2 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Mar", Occurrence = 2 });
                break;
            case "Quarterly":
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Quarterly", Period = "Q1", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Quarterly", Period = "Q2", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Quarterly", Period = "Q3", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Quarterly", Period = "Q4", Occurrence = 1 });
                break;
            case "HalfYearly":
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "HalfYearly", Period = "H1", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "HalfYearly", Period = "H2", Occurrence = 1 });
                break;
            default:
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Apr", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "May", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jun", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jul", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Aug", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Sep", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Oct", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Nov", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Dec", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Jan", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Feb", Occurrence = 1 });
                profilePeriodBreakdowns.Add(new ProfilePeriodBreakdown { PeriodType = "Month", Period = "Mar", Occurrence = 1 });
                break;
        }
        return profilePeriodBreakdowns;
    }
    private static List<ProfilePeriod> ApplySingleEnvelopeProfilePattern(string fundingStream, string fundingPeriod, 
                                                                            List<RequestPeriodValue> allocationValuesByPeriod, 
                                                                            List<ProfilePeriod> profilePeriods)
        {
            List<ProfilePattern> profilePatterns = LoadProfilePattern(fundingStream, fundingPeriod);
            decimal allocationValueToBeProfiled = Convert.ToDecimal(allocationValuesByPeriod[0].AllocationValue);
            foreach (ProfilePeriod profilePeriod in profilePeriods)
            {
                var profilePercentage = profilePatterns.FirstOrDefault(
                                                                        q => q.Period == profilePeriod.Period &&
                                                                        q.DistributionPeriod == profilePeriod.DistributionPeriod &&
                                                                        q.Occurence == profilePeriod.Occurence
                                                                      )
                                                                      .PeriodPatternPercentage;

                profilePeriod.ProfileValue = (profilePercentage / 100) * allocationValueToBeProfiled;
                profilePeriod.ProfileValue = decimal.Round(profilePeriod.ProfileValue, 2, MidpointRounding.AwayFromZero);
            }
            decimal roundingValue = allocationValueToBeProfiled - profilePeriods.Sum(p => p.ProfileValue);
            if (roundingValue > 0)
                profilePeriods.FirstOrDefault().ProfileValue += roundingValue;

            return profilePeriods;
        }


        private static List<ProfilePeriod> ApplyMultipleEnvelopeProfilePattern(string fundingStream, string fundingPeriod,
                                                                            List<RequestPeriodValue> allocationValuesByPeriod,
                                                                            List<ProfilePeriod> profilePeriods)
        {
            List<ProfilePattern> profilePatterns = LoadProfilePattern(fundingStream, fundingPeriod);

            List<PercentageByDistibutionPeriod> percentagebyDistributionPeriods = GetTotalPercentageByDistibutionPeriods(profilePatterns, fundingStream, fundingPeriod, profilePeriods);

            foreach (RequestPeriodValue requestPeriod in allocationValuesByPeriod)
            {
                decimal? totalPercentage = percentagebyDistributionPeriods.FirstOrDefault(q => q.Period == requestPeriod.Period)?.TotalProfilePercentage;

                decimal allocationValueToBeProfiled = Convert.ToDecimal(requestPeriod.AllocationValue);
                var profilePeriodWithMatchingDistributionPeriod = profilePeriods.FindAll(pp => pp.DistributionPeriod == requestPeriod.Period);

                foreach (ProfilePeriod profilePeriod in profilePeriodWithMatchingDistributionPeriod)
                {
                    var profilePercentage = profilePatterns.FirstOrDefault(
                                                                            q => q.Period == profilePeriod.Period &&
                                                                            q.DistributionPeriod == profilePeriod.DistributionPeriod &&
                                                                            q.Occurence == profilePeriod.Occurence
                                                                            )
                                                                            .PeriodPatternPercentage;

                    profilePeriod.ProfileValue = (profilePercentage / totalPercentage.GetValueOrDefault()) * allocationValueToBeProfiled;
                    profilePeriod.ProfileValue = decimal.Round(profilePeriod.ProfileValue, 2, MidpointRounding.AwayFromZero);
                }
                if (profilePeriodWithMatchingDistributionPeriod.Count > 0)
                {
                    decimal roundingValue = allocationValueToBeProfiled - profilePeriodWithMatchingDistributionPeriod.Sum(p => p.ProfileValue);
                    if (roundingValue > 0)
                        profilePeriodWithMatchingDistributionPeriod.FirstOrDefault().ProfileValue += roundingValue;
                }
            }
            return profilePeriods;
        }
    */
    #endregion
}
