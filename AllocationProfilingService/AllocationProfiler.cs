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
            FundingStreamPeriodProfilePattern fspPattern = LoadFSPConfiguration(request.AllocationOrganisation.AlternateOrganisation.Identifier, 
                                                                                request.FundingStream, 
                                                                                request.FundingPeriod, 
                                                                                log);

            //Three types of profiling:
            //Profile/Re-profile with balancing payment calc , don't change past periods (ReProfilePastPeriods =false, e.g. PE Sport and Premium, DSG)
            //Profile/Re-profile with no balancing payment calc, don't change past periods (ReProfilePastPeriods =false, e.g. 16-19 funding)
            //Profile/Re-Profile wiith no balancing payment calc, change past periods (e.g. SFA type of funding)
            if (request.LastApprovedProfilePeriods != null && !fspPattern.ReProfilePastPeriods && !fspPattern.CalculateBalancingPayment )
            {
                return GetReProfileResponse(request, fspPattern.ProfilePattern, log);
            }
           

            return GetReponseForAnyDistributionPattern(request, fspPattern.ProfilePattern,fspPattern.CalculateBalancingPayment, log);

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


        private static string GetReponseForAnyDistributionPattern(Request req, List<ProfilePeriodPattern> patterns , bool balancingPaymentCalculate, TraceWriter log)
        {
            List<AllocationProfilePeriod> profilePeriods = GetProfiledPeriods(req.AllocationStartDate, req.AllocationEndDate, req.AllocationValuesByDistributionPeriod, patterns, log);
            List<AllocationProfilePeriod> resultProfilePeriods = profilePeriods;
            if (balancingPaymentCalculate)
            {
                resultProfilePeriods = GetBalancingPaymentProfilePeriods(req.CalculationDate, profilePeriods, req.LastApprovedProfilePeriods, patterns, log);
            }

            var myObj = new
            {
                AllocationOrganisation = req.AllocationOrganisation,
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods = resultProfilePeriods
            };

            return JsonConvert.SerializeObject(myObj);
        }

        public static string GetReProfileResponse(Request req, List<ProfilePeriodPattern> patterns ,TraceWriter log)
        {
            DateTime calculationDate = DateTime.ParseExact(req.CalculationDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            //Assumption here that this is not a multiple distribution period, perhaps throw an error - Not Supported?
            decimal originalAllocationValue = Convert.ToDecimal(req.AllocationValuesByDistributionPeriod.First().AllocationValue);
            string allocationDistributionPeriod = req.AllocationValuesByDistributionPeriod.FirstOrDefault().Period;

            List<AllocationProfilePeriod> pastPeriodAllocationProfile = GetPastPeriodsFromAllocationProfile(calculationDate, req.LastApprovedProfilePeriods, patterns);

            var revisedAllocationValuesByDistributionPeriod = GetRevisedAllocationValueRequest(calculationDate, originalAllocationValue, allocationDistributionPeriod, pastPeriodAllocationProfile, patterns);
            List <RequestPeriodValue> revisedAllocValsByDistPeriod= new List<RequestPeriodValue>() { revisedAllocationValuesByDistributionPeriod };

            var myObj = new
            {
                AllocationOrganisation = req.AllocationOrganisation,
                FundingStream = req.FundingStream,
                FundingPeriod = req.FundingPeriod,
                AllocationValuesByDistributionPeriod = req.AllocationValuesByDistributionPeriod,
                ProfilePeriods= GetReProfiledPeriods(calculationDate, revisedAllocValsByDistPeriod, pastPeriodAllocationProfile, patterns, log)
            };

            return JsonConvert.SerializeObject(myObj);
        }

        public static List<AllocationProfilePeriod> GetBalancingPaymentProfilePeriods(string reqCalculationDate, List<AllocationProfilePeriod> currentProfilePeriods, List<AllocationProfilePeriod> previousProfilePeriods, List<ProfilePeriodPattern> patterns, TraceWriter log)
        {
            DateTime calculationDate = DateTime.Now;
            if (!string.IsNullOrEmpty(reqCalculationDate))
                calculationDate = DateTime.ParseExact(reqCalculationDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            List<AllocationProfilePeriod> pastProfilePeriodsCurrentAllocationProfile = GetPastPeriodsFromAllocationProfile(calculationDate, currentProfilePeriods, patterns);
            List<AllocationProfilePeriod> resultProfilePeriod = pastProfilePeriodsCurrentAllocationProfile;

            decimal shouldBeenPaidPreviously = pastProfilePeriodsCurrentAllocationProfile.Sum(p => p.ProfileValue);
            decimal balancingPayment = shouldBeenPaidPreviously;

            List<AllocationProfilePeriod> futureProfilePeriods = GetFuturePeriodsFromAllocationProfile(calculationDate, currentProfilePeriods, patterns);

            if (previousProfilePeriods != null)
            {
                List<AllocationProfilePeriod> pastProfilePeriodsPreviousAllocationProfile = GetPastPeriodsFromAllocationProfile(calculationDate, previousProfilePeriods, patterns);
                decimal paidPreviously = pastProfilePeriodsPreviousAllocationProfile.Sum(p => p.ProfileValue);
                balancingPayment -= paidPreviously;
                resultProfilePeriod = pastProfilePeriodsPreviousAllocationProfile;
            }
            else
            {
                pastProfilePeriodsCurrentAllocationProfile.ForEach(p => p.ProfileValue = 0);
            }
            
            futureProfilePeriods.First().ProfileValue += balancingPayment; //ToDO:check if balancing payment <0, in which case add to a period with non-zero profile value
            resultProfilePeriod.AddRange(futureProfilePeriods);

            return resultProfilePeriod;
        }


        private static List<AllocationProfilePeriod> GetProfiledPeriods(string reqAllocationStartDate, string reqAllocationEndDate, List<RequestPeriodValue> allocationValuesByPeriod, List<ProfilePeriodPattern> patterns, TraceWriter log)
        {
            DateTime allocationStartDate = DateTime.ParseExact(reqAllocationStartDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            DateTime allocationEndDate = DateTime.ParseExact(reqAllocationEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            List<AllocationProfilePeriod> allocationProfilePeriods = GetProfilePeriodsForAllocation(allocationStartDate, allocationEndDate, patterns, log);
            return ApplyProfilePattern(allocationValuesByPeriod, patterns, allocationProfilePeriods, log);
        }


        private static List<AllocationProfilePeriod> GetReProfiledPeriods(DateTime calculationDate, List<RequestPeriodValue> revisedAllocationPeriodValue,List<AllocationProfilePeriod> pastProfilePeriods, List<ProfilePeriodPattern> patterns, TraceWriter log)
        {

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

        private static decimal GetRevisedAllocationValueToProfile(DateTime currentDateTime,List<AllocationProfilePeriod> previousPastAllocationProfilePeriods, 
                                                                    List<ProfilePeriodPattern> profilePatterns, decimal revisedAllocationValue)
        {
            decimal pastPeriodTotal = previousPastAllocationProfilePeriods.Sum(p => p.ProfileValue);
            return revisedAllocationValue - pastPeriodTotal;
        }

        private static List<ProfilePeriodPattern> GetFutureProfilePatternsForAllocation(DateTime currentDateTime, List<ProfilePeriodPattern> profilePatterns)
        {
            // find the pattern periods where cut-off date is less than the current date
            return profilePatterns.Where(p => p.PeriodCutOffDate > currentDateTime).ToList();
        }

        private static List<AllocationProfilePeriod> GetPastPeriodsFromAllocationProfile(DateTime currentDateTime,
                                                                         List<AllocationProfilePeriod> allocationProfilePeriods, List<ProfilePeriodPattern> profilePatterns)
        {
            // find the pattern periods where cut-off date is less than the current date
            var pastPeriodPatterns= profilePatterns.Where(p => p.PeriodCutOffDate<=currentDateTime).ToList();
            return GetProfilePeriodsMatchingPatternPeriods(allocationProfilePeriods, pastPeriodPatterns);
        }

        private static List<AllocationProfilePeriod> GetFuturePeriodsFromAllocationProfile(DateTime currentDateTime,
                                                                         List<AllocationProfilePeriod> allocationProfilePeriods, List<ProfilePeriodPattern> profilePatterns)
        {
            // find the pattern periods where cut-off date is less than the current date
            return GetProfilePeriodsMatchingPatternPeriods(allocationProfilePeriods, GetFutureProfilePatternsForAllocation(currentDateTime, profilePatterns));
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
                    if (pattern.Period == period.Period && pattern.DistributionPeriod == period.DistributionPeriod && pattern.Occurrence== period.Occurence & pattern.PeriodEndDate.Year == period.PeriodYear) //TODO: Perhaps more checks needed- year? Occurence?
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

        private static FundingStreamPeriodProfilePattern LoadFSPConfiguration(string providerIdentifier, string fundingStream, string fundingPeriod,TraceWriter log)
        {
            // Get Provider Profile Pattern for an FSP, if not get for FS for the provider
            // if not available then get one for FSP
            // if not available then get for FS ToDo

            FundingStreamPeriodProfilePattern providerPattern = FromCofigGetProviderFspProfilePattern(providerIdentifier, fundingStream, fundingPeriod,log);
            return providerPattern ?? FromCofigGetFspProfilePattern(providerIdentifier, fundingStream, fundingPeriod,log);

        }

        private static FundingStreamPeriodProfilePattern FromCofigGetFspProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod, TraceWriter log)
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
                    return result;

                }
            }
            return null;
            //return FromConfigGetNonLevy1618AppsProfilePattern();
        }

        private static FundingStreamPeriodProfilePattern FromCofigGetProviderFspProfilePattern(string providerIdentifier, string fundingStream, string fundingPeriod, TraceWriter log)
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
                    return result.FundingStreamPeriodProfilePattern;

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
        public bool ReProfilePastPeriods { get; set; }
        public bool CalculateBalancingPayment { get; set; }
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
}
