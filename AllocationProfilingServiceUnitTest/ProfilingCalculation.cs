﻿using System;
using System.Collections.Generic;
using AllocationProfilingService;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Microsoft.Azure.Documents.Client;
using System.Configuration;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Host;
using AllocatinProfilingService;

namespace AllocationProfilingServiceUnitTest
{
    [TestClass]
    public class ProfilingCalculation
    {
        private static DocumentClient client = GetCustomClient();
        
        private TraceWriter writer = new TraceMonitor();

        private static DocumentClient GetCustomClient()
        {
            DocumentClient customClient = new DocumentClient(new Uri(ConfigurationManager.AppSettings["CosmosDBAccountEndpoint"]),
                ConfigurationManager.AppSettings["CosmosDBAccountKey"],
                new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                    RetryOptions = new RetryOptions()
                      {
                          MaxRetryAttemptsOnThrottledRequests = 10,
                          MaxRetryWaitTimeInSeconds = 30
                      }
                });

           customClient.ConnectionPolicy.PreferredLocations.Add(LocationNames.WestEurope);

           return customClient;
       }

        [TestInitialize]
        public void SetUp()
        {
            AllocationProfiler._client = GetCustomClient();
            AllocationProfiler.Log = writer;
        }

        [TestMethod]
        public void CheckDSGProfile()
        {
            
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(DSGRequest, writer));
            
            Assert.AreEqual(expected: 96.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr" && q.Occurence== 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr" && q.Occurence== 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr" && q.Occurence== 3 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec" && q.Occurence == 1 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec" && q.Occurence == 2 && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan" && q.Occurence == 1 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan" && q.Occurence == 2 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb" && q.Occurence == 1 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb" && q.Occurence == 2 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar" && q.Occurence == 1 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(96.00M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar" && q.Occurence == 2 && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 25);
        }

        [TestMethod]
        public void CheckPESportPremiumProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumRequest, writer));
            Assert.AreEqual(expected: 14000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "H1").ProfileValue);
            Assert.AreEqual(expected: 10000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "H2").ProfileValue);

            Assert.AreEqual(response.ProfilePeriods.Count, 2);
        }

        [TestMethod]
        public void CheckPESportPremiumProfileWithMonthlyPeriods()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportRequest, writer));
            Assert.AreEqual(expected: 14000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct").ProfileValue);
            Assert.AreEqual(expected: 10000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr").ProfileValue);

            Assert.AreEqual(response.ProfilePeriods.Count, 2);
        }

        [TestMethod]
        public void CheckPESportPremiumNewProfileStartOfYear()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumNewRequest, writer));
            Assert.AreEqual(expected: 14000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P1").ProfileValue);
            Assert.AreEqual(expected: 0.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P2").ProfileValue);
            Assert.AreEqual(expected: 10000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P3").ProfileValue);

            Assert.AreEqual(response.ProfilePeriods.Count, 3);
        }

        [TestMethod]
        public void CheckPESportPremiumNewProfilePastFirstPeriodToCaterForNewSchools()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumRequestNewProfilePastFirstPeriodRequest, writer));
            Assert.AreEqual(expected: 0.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P1").ProfileValue);
            Assert.AreEqual(expected: 14000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P2").ProfileValue);
            Assert.AreEqual(expected: 10000000.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P3").ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 3);
        }

        [TestMethod]
        public void CheckPESportPremiumNewReProfilePastTwoPeriodToCaterForUpdates()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumNewReProfilePastTwoPeriodsRequest, writer));
            Assert.AreEqual(expected: 70.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P1").ProfileValue);
            Assert.AreEqual(expected: 0.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P2").ProfileValue);
            Assert.AreEqual(expected: 80.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P3").ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 3);
        }

        [TestMethod]
        public void CheckPESportPremiumNewReProfilePastFirstPeriod()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumRequestReProfilePastFirstPeriodRequest, writer));
            Assert.AreEqual(expected: 70.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P1").ProfileValue);
            Assert.AreEqual(expected: 70.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P2").ProfileValue);
            Assert.AreEqual(expected: 100.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P3").ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 3);
        }

        [TestMethod]
        public void CheckPESportPremiumNewReProfileBeforeFirstPayment()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumNewReProfileBeforeFirstPaymentRequest, writer));
            Assert.AreEqual(expected: 140.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P1").ProfileValue);
            Assert.AreEqual(expected: 0.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P2").ProfileValue);
            Assert.AreEqual(expected: 100.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "P3").ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 3);
        }

        [TestMethod]
        public void CheckPESportPremiumReProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PESportPremiumRequestReProfile, writer));
            Assert.AreEqual(expected: 70.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "H1").ProfileValue);
            Assert.AreEqual(expected: 80.00M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "H2").ProfileValue);

            Assert.AreEqual(response.ProfilePeriods.Count, 2);
        }

        [TestMethod]
        public void CheckPupilPremiumProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(PupilPremiumRequest, writer));
            
            Assert.AreEqual(expected: 600M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Q1").ProfileValue);
            Assert.AreEqual(expected: 600M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Q2").ProfileValue);
            Assert.AreEqual(expected: 840M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Q3").ProfileValue);
            Assert.AreEqual(expected: 360M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Q4").ProfileValue);
            Assert.AreEqual(response.ProfilePeriods.Count, 4);
        }

        [TestMethod]
        public void CheckNonLevy1618Profile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(NonLevy1618Request, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 15);
            Assert.AreEqual(expected: 372.88M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan" && q.PeriodYear==2018).ProfileValue);
            Assert.AreEqual(expected: 677.97M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 949.15M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 18.90M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr" && q.PeriodYear == 2018).ProfileValue); //penny out - £18.90 as per excel
            Assert.AreEqual(expected: 18.72M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 19.13M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 19.50M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 21.34M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 32.40M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep" && q.PeriodYear == 2018).ProfileValue); 
            Assert.AreEqual(expected: 40.76M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 35.60M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 57.15M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(expected: 50.14M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan" && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(expected: 45.27M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb" && q.PeriodYear == 2019).ProfileValue);
            Assert.AreEqual(expected: 41.09M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar" && q.PeriodYear == 2019).ProfileValue);
        }

        [TestMethod]
        public void CheckNonLevy1618ProfileShortAllocation()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(NonLevy1618RequestShortAllocation, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 6);
            Assert.AreEqual(35.96M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(54.60M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(68.68M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(59.98M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(96.29M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec" && q.PeriodYear == 2018).ProfileValue);
            Assert.AreEqual(84.49M, response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan" && q.PeriodYear == 2019).ProfileValue);
        }
        [TestMethod]
        public void CheckNonLevyAdultProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(NonLevyAdultRequest, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 15);
        }


        [TestMethod]
        public void CheckAEBProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(AEBRequest, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 12);
            Assert.AreEqual(expected:460.97M, actual:response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug").ProfileValue);
            Assert.AreEqual(expected: 273.90M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep").ProfileValue);
            Assert.AreEqual(expected: 276.78M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct").ProfileValue);
            Assert.AreEqual(expected: 226.02M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov").ProfileValue);
            Assert.AreEqual(expected: 181.64M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec").ProfileValue);
            Assert.AreEqual(expected: 237.51M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan").ProfileValue);
            Assert.AreEqual(expected: 172.07M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb").ProfileValue);
            Assert.AreEqual(expected: 171.11M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar").ProfileValue);
            Assert.AreEqual(expected: 135.91M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr").ProfileValue); //PENNY OUT - 135.90 AS PER EXCEL
            Assert.AreEqual(expected: 109.34M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May").ProfileValue);
            Assert.AreEqual(expected: 93.17M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun").ProfileValue);
            Assert.AreEqual(expected: 61.58M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul").ProfileValue);
            
        }

        [TestMethod]
        public void CheckAEBProfileShortAllocation()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(AEBRequestShortAllocation, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 5);
        }

        [TestMethod]
        public void CheckCLProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(CLRequest, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 12);
            Assert.AreEqual(expected: 249.87M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan").ProfileValue);
            Assert.AreEqual(expected: 249.89M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb").ProfileValue);
            Assert.AreEqual(expected: 250.79M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar").ProfileValue);
            Assert.AreEqual(expected: 99.97M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr").ProfileValue);
            Assert.AreEqual(expected: 99.97M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May").ProfileValue);
            Assert.AreEqual(expected: 99.97M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun").ProfileValue);
            Assert.AreEqual(expected: 100.09M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul").ProfileValue);
        }

        [TestMethod]
        public void Check1618AppsProfile()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(Sixteen18AAppsRequest, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 12);
            Assert.AreEqual(expected: 249.82M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan").ProfileValue);
            Assert.AreEqual(expected: 249.81M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb").ProfileValue);
            Assert.AreEqual(expected: 251.32M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar").ProfileValue);
            Assert.AreEqual(expected: 100.15M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr").ProfileValue);
            Assert.AreEqual(expected: 100.15M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May").ProfileValue);
            Assert.AreEqual(expected: 100.15M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun").ProfileValue);
            Assert.AreEqual(expected: 99.55M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul").ProfileValue);
        }

        [TestMethod]
        public void CheckAEBProfileForProviderWithPattern()
        {
            Response response = JsonConvert.DeserializeObject<Response>(AllocationProfiler.GetGenericResponse(AEBRequestForProviderWithPattern, writer));
            Assert.AreEqual(response.ProfilePeriods.Count, 12);
            Assert.AreEqual(expected: 171.11M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Aug").ProfileValue);
            Assert.AreEqual(expected: 273.90M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Sep").ProfileValue);
            Assert.AreEqual(expected: 276.78M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Oct").ProfileValue);
            Assert.AreEqual(expected: 226.02M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Nov").ProfileValue);
            Assert.AreEqual(expected: 237.51M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Dec").ProfileValue);
            Assert.AreEqual(expected: 181.64M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jan").ProfileValue);
            Assert.AreEqual(expected: 172.07M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Feb").ProfileValue);
            Assert.AreEqual(expected: 460.97M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Mar").ProfileValue);
            Assert.AreEqual(expected: 135.91M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Apr").ProfileValue);
            Assert.AreEqual(expected: 109.34M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "May").ProfileValue);
            Assert.AreEqual(expected: 61.58M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jun").ProfileValue);
            Assert.AreEqual(expected: 93.17M, actual: response.ProfilePeriods.ToArray().FirstOrDefault(q => q.Period == "Jul").ProfileValue);
        }

        private static Request DSGRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "DSG1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2400",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/04/2018",
                    AllocationEndDate = "31/03/2019",
                    CalculationDate = "01/03/2018"
                };
                return req;
            }
        }

        private static Request PESportRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORT1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="24000000",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019"
                };
                return req;
            }
        }

        private static Request PESportPremiumRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREM1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                    {
                        new RequestPeriodValue {AllocationValue="24000000",Period="2018-2019" }
                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019"
                };
                return req;
            }
        }

        private static Request PESportPremiumNewRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREMNEW1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="24000000",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate = "20/07/2018"
                };
                return req;
            }
        }

        private static Request PESportPremiumRequestNewProfilePastFirstPeriodRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREMNEW1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="24000000",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate = "02/11/2018"
                };
                return req;
            }
        }
        

        private static Request PESportPremiumRequestReProfile
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREM1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="150",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate="22/10/2018",
                     LastApprovedProfilePeriods=new List<AllocationProfilePeriod>()
                     {
                         new AllocationProfilePeriod()
                         {
                              Period="H1",
                              Occurence=1,
                              PeriodYear=2018,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=70.00M
                         },
                         new AllocationProfilePeriod()
                         {
                             Period="H2",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=50.00M
                         }
                     }

                };
                return req;
            }
        }

        private static Request PESportPremiumNewReProfilePastTwoPeriodsRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREMNEW1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="150",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate = "02/02/2019",
                    LastApprovedProfilePeriods = new List<AllocationProfilePeriod>()
                     {
                         new AllocationProfilePeriod()
                         {
                              Period="P1",
                              Occurence=1,
                              PeriodYear=2018,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=70.00M
                         },
                          new AllocationProfilePeriod()
                         {
                              Period="P2",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=0.00M
                         },
                         new AllocationProfilePeriod()
                         {
                             Period="P3",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=50.00M
                         }
                     }

                };
                return req;
            }
        }

        private static Request PESportPremiumRequestReProfilePastFirstPeriodRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREMNEW1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="240",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate = "02/11/2018",
                    LastApprovedProfilePeriods = new List<AllocationProfilePeriod>()
                     {
                         new AllocationProfilePeriod()
                         {
                              Period="P1",
                              Occurence=1,
                              PeriodYear=2018,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=70.00M
                         },
                          new AllocationProfilePeriod()
                         {
                              Period="P2",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=0.00M
                         },
                         new AllocationProfilePeriod()
                         {
                             Period="P3",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=50.00M
                         }
                     }

                };
                return req;
            }
        }

        private static Request PESportPremiumNewReProfileBeforeFirstPaymentRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PESPORTPREMNEW1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="240",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019",
                    CalculationDate = "10/10/2018",
                    LastApprovedProfilePeriods = new List<AllocationProfilePeriod>()
                     {
                         new AllocationProfilePeriod()
                         {
                              Period="P1",
                              Occurence=1,
                              PeriodYear=2018,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=70.00M
                         },
                          new AllocationProfilePeriod()
                         {
                              Period="P2",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=0.00M
                         },
                         new AllocationProfilePeriod()
                         {
                             Period="P3",
                              Occurence=1,
                              PeriodYear=2019,
                              DistributionPeriod="2018-19",
                              PeriodType="HalfYearly",
                              ProfileValue=50.00M
                         }
                     }

                };
                return req;
            }
        }

        private static Request PupilPremiumRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "PUPPREM1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2400",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/04/2018",
                    AllocationEndDate = "31/03/2019"
                };
                return req;
            }
        }

        private static Request NonLevy1618Request
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "NonLevy16181819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2017-2018" },
                                                        new RequestPeriodValue {AllocationValue="400",Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/01/2018",
                    AllocationEndDate = "31/03/2019"
                };
                return req;
            }
        }

        private static Request NonLevy1618RequestShortAllocation
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "NonLevy16181819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2017-2018" },
                                                        new RequestPeriodValue {AllocationValue="400", Period="2018-2019" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "01/01/2019"
                };
                return req;
            }
        }

        private static Request NonLevyAdultRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "NonLevyAdult1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="12000",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "01/01/2018",
                    AllocationEndDate = "31/03/2019"
                };
                return req;
            }
        }

        private static Request AEBRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "AEB1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="400",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "22/08/2018",
                    AllocationEndDate = "05/07/2019"
                };
                return req;
            }
        }

        private static Request AEBRequestShortAllocation
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "AEB1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="12000",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/12/2018"
                };
                return req;
            }
        }

        private static Request CLRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "CL1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="400",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019"
                };
                return req;
            }
        }

        private static Request Sixteen18AAppsRequest
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestData,
                    FundingStreamPeriod = "1618APPS1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="400",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019"
                };
                return req;
            }
        }

        private static Request AEBRequestForProviderWithPattern
        {
            get
            {

                Request req = new Request
                {
                    AllocationOrganisation = OrganisationTestDataWithPattern,
                    FundingStreamPeriod = "AEB1819",
                    AllocationValuesByDistributionPeriod = new List<RequestPeriodValue>
                                                    {
                                                        new RequestPeriodValue {AllocationValue="2000",Period="2018-2019" },
                                                        new RequestPeriodValue {AllocationValue="400",Period="2019-2020" }
                                                    },
                    AllocationStartDate = "01/08/2018",
                    AllocationEndDate = "31/07/2019"
                };
                return req;
            }
        }
        


        private static Organisation OrganisationTestData => new Organisation
        {
            OrganisationId = "ORG0001",
            AlternateOrganisation = new AlternateOrganisationIdentifier
            {
                IdentifierName = "UKPRN",
                Identifier = "10001234"
            }
        };

        private static Organisation OrganisationTestDataWithPattern => new Organisation
        {
            OrganisationId = "ORG0001",
            AlternateOrganisation = new AlternateOrganisationIdentifier
            {
                IdentifierName = "UKPRN",
                Identifier = "1001024"
            }
        };
    }
}
