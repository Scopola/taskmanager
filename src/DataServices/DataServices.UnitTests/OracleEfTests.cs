using System;
using DataServices.Adapters;
using DataServices.Controllers;
using DataServices.Models;
using FakeItEasy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DataServices.UnitTests
{
    public class OracleEfTests
    {
        private SdraDbContext _dbContext;
        private ILogger<DataAccessApiController> _fakeLogger;
        private IDataAccessWebServiceSoapClientAdapter _fakeDataAccessWebServiceSoapClientAdapter;

        [SetUp]
        public void Setup()
        {
            var dbContextOptions = new DbContextOptionsBuilder<SdraDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;

            _dbContext = new SdraDbContext(dbContextOptions);

            _fakeLogger = A.Fake<ILogger<DataAccessApiController>>();
            _fakeDataAccessWebServiceSoapClientAdapter = A.Fake<DataAccessWebServiceSoapClientAdapter>();
        }

        [Test]
        public void Test_GetDocumentAssessmentData_returns_assessment_data_for_given_sdoc_Id()
        {
            var controller = new DataAccessApiController(_dbContext, _fakeLogger, _fakeDataAccessWebServiceSoapClientAdapter);

            _dbContext.AssessmentData.AddAsync(new DocumentAssessmentData()
            {
               SdocId = 1871160,
               Datum = null,
               DocumentNature = "Textual",
               DocumentType = "Correspondence (Letter / Email / Fax / Signal) - requiring action",
               EffectiveStartDate = new DateTime(2017, 03, 27),
               Name = "COR_IN_GBENC_GB303790_000_P007_07-03-17",
               Notes = null,
               ReceiptDate = new DateTime(17, 03, 30),
               SDODate = new DateTime(17,03,30),
               SourceName = "RSDRA2017000073439",
               Team = "HDB Master Data"
            });
            _dbContext.SaveChanges();

            var actionResult = controller.GetDocumentAssessmentData("HDB", 1871160);
            var objectResult = actionResult as ObjectResult;
            var data = objectResult.Value as DocumentAssessmentData;

            Assert.AreEqual(data.SdocId, 1871160);
        }
    }
}