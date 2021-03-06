﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Common.Helpers;
using Common.Helpers.Auth;
using Common.Messages.Events;
using FakeItEasy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Portal.Auth;
using Portal.Configuration;
using Portal.Helpers;
using Portal.HttpClients;
using Portal.Pages.DbAssessment;
using Portal.UnitTests.Helpers;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.UnitTests
{
    [TestFixture]
    public class AssessTests
    {
        private WorkflowDbContext _dbContext;
        private AssessModel _assessModel;
        private int ProcessId { get; set; }
        private ILogger<AssessModel> _fakeLogger;
        private IEventServiceApiClient _fakeEventServiceApiClient;
        private IAdDirectoryService _fakeAdDirectoryService;
        private IPortalUserDbService _fakePortalUserDbService;
        private IPortalUserDbService _realPortalUserDbService;
        private ICommentsHelper _commentsHelper;
        private ICommentsHelper _fakeDbAssessmentCommentsHelper;
        private IPageValidationHelper _pageValidationHelper;
        private IPageValidationHelper _fakePageValidationHelper;
        private ICarisProjectHelper _fakeCarisProjectHelper;
        private IOptions<GeneralConfig> _generalConfig;

        public AdUser TestUser { get; set; }

        [SetUp]
        public void Setup()
        {
            var dbContextOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;

            _dbContext = new WorkflowDbContext(dbContextOptions);
            _realPortalUserDbService = new PortalUserDbService(_dbContext);

            TestUser = AdUserHelper.CreateTestUser(_dbContext);

            _fakeEventServiceApiClient = A.Fake<IEventServiceApiClient>();
            _fakeCarisProjectHelper = A.Fake<ICarisProjectHelper>();
            _generalConfig = A.Fake<IOptions<GeneralConfig>>();

            ProcessId = 123;

            _dbContext.WorkflowInstance.Add(new WorkflowInstance
            {
                AssessmentData = new AssessmentData(),
                ProcessId = ProcessId,
                ActivityName = "",
                SerialNumber = "123_sn"
            });

            _dbContext.AssessmentData.Add(new AssessmentData
            {
                ProcessId = ProcessId
            });

            _dbContext.DbAssessmentAssessData.Add(new DbAssessmentAssessData()
            {
                ProcessId = ProcessId,
                Assessor = TestUser
            });

            _dbContext.ProductAction.Add(new ProductAction
            {
                ProcessId = ProcessId,
                ImpactedProduct = "",
                ProductActionType = new ProductActionType { Name = "Test" },
                Verified = false
            });

            _dbContext.DataImpact.Add(new DataImpact
            {
                ProcessId = ProcessId
            });

            _dbContext.PrimaryDocumentStatus.Add(new PrimaryDocumentStatus
            {
                ProcessId = ProcessId,
                CorrelationId = Guid.NewGuid()
            });

            _dbContext.SaveChanges();

            _fakeAdDirectoryService = A.Fake<IAdDirectoryService>();
            _fakePortalUserDbService = A.Fake<IPortalUserDbService>();

            _commentsHelper = new CommentsHelper(_dbContext, _fakePortalUserDbService);
            _fakeDbAssessmentCommentsHelper = A.Fake<ICommentsHelper>();

            _fakeLogger = A.Dummy<ILogger<AssessModel>>();

            _pageValidationHelper = new PageValidationHelper(_dbContext, _fakeAdDirectoryService, _fakePortalUserDbService);
            _fakePageValidationHelper = A.Fake<IPageValidationHelper>();

            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService, _pageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
        }

        [TestCase("High", "Save", ExpectedResult = true)]
        [TestCase("High", "Done", ExpectedResult = true)]
        [TestCase("Medium", "Save", ExpectedResult = true)]
        [TestCase("Medium", "Done", ExpectedResult = true)]
        [TestCase("Low", "Save", ExpectedResult = true)]
        [TestCase("Low", "Done", ExpectedResult = true)]
        [TestCase("", "Save", ExpectedResult = true)]
        [TestCase("  ", "Save", ExpectedResult = true)]
        [TestCase(null, "Save", ExpectedResult = true)]
        [TestCase("INVALID", "Done", ExpectedResult = false)]
        [TestCase("3454", "Done", ExpectedResult = false)]
        public async Task<bool> Test_CheckAssessPageForErrors_with_valid_and_invalid_complexity_returns_expected_result(string complexity, string action)
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            var valid = await _pageValidationHelper.CheckAssessPageForErrors(
                action,
                "ion",
                complexity,
                "activity",
                "source category",
                "task type",
                A.Dummy<bool>(),
                A.Dummy<string>(),
                A.Dummy<List<ProductAction>>(),
                A.Dummy<List<DataImpact>>(),
                A.Dummy<DataImpact>(),
                "team",
                TestUser,
                TestUser,
                A.Dummy<List<string>>(),
               TestUser.UserPrincipalName,
                TestUser);


            return valid;
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_an_empty_ion_activityCode_sourceCategory_tasktype_team_assessor_results_in_validation_error_message()
        {
            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() {Name = "GB1234"});
            await _dbContext.SaveChangesAsync();

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "";
            _assessModel.ActivityCode = "";
            _assessModel.SourceCategory = "";
            _assessModel.TaskType = "";
            _assessModel.Team = "";

            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.DataImpacts = new List<DataImpact>();

            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 5);
            Assert.Contains($"Task Information: Ion cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Activity code cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Source category cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Task type cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Team cannot be empty", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_an_empty_Verifier_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Assessor = TestUser;

            _assessModel.Verifier = null;
            _assessModel.DataImpacts = new List<DataImpact>();

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Verifier cannot be empty", _assessModel.ValidationErrorMessages);

            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_an_empty_assessor_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Verifier = TestUser;

            _assessModel.Assessor = null;
            _assessModel.DataImpacts = new List<DataImpact>();

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Assessor cannot be empty", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_duplicate_hpd_usages_in_dataImpact_results_in_validation_error_message()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";

            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;

            var hpdUsage = new HpdUsage()
            {
                HpdUsageId = 1,
                Name = "HpdUsageName"
            };
            _assessModel.DataImpacts = new List<DataImpact>()
            {
                new DataImpact() { DataImpactId = 1, HpdUsageId = 1, HpdUsage = hpdUsage, ProcessId = 123},
                new DataImpact() {DataImpactId = 2, HpdUsageId = 1, HpdUsage = hpdUsage, ProcessId = 123}
            };

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Data Impact: More than one of the same Usage selected", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_duplicate_impactedProducts_in_productAction_results_in_validation_error_message()
        {
            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1234" });
            await _dbContext.SaveChangesAsync();

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "Some change details";
            _assessModel.RecordProductAction = new List<ProductAction>
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1},
                new ProductAction() { ProductActionId = 2, ImpactedProduct = "GB1234", ProductActionTypeId = 1}
            };

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Record Product Action: More than one of the same Impacted Products selected", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_invalid_impactedProducts_in_productAction_results_in_validation_error_message()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "Some change details";
            _assessModel.RecordProductAction = new List<ProductAction>
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1},
                new ProductAction() { ProductActionId = 2, ImpactedProduct = "GB1235", ProductActionTypeId = 1}
            };

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 2);
            Assert.Contains($"Record Product Action: Impacted Product {_assessModel.RecordProductAction[0].ImpactedProduct} does not exist", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Record Product Action: Impacted Product {_assessModel.RecordProductAction[1].ImpactedProduct} does not exist", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_invalid_username_for_assessor_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Assessor = new AdUser { DisplayName = "unknown", UserPrincipalName = "unknown" };
            _assessModel.Verifier = TestUser;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(AdUser.Unknown))
                .Returns(false);

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Unable to set Assessor to unknown user {_assessModel.Assessor.DisplayName}", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_entering_invalid_username_for_verifier_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "HW";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = new AdUser { DisplayName = "unknown", UserPrincipalName = "unknown" }; ;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(AdUser.Unknown))
                .Returns(false);

            await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Unable to set Verifier to unknown user {_assessModel.Verifier.DisplayName}", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_Given_CurrentUser_Is_Not_Valid_Then_Returns_Validation_Error_Message()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            var invalidPrincipalName = "THIS-USER-PRINCIPAL-NAME-DOES-NOT-EXIST@example.com";
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns(("THIS DISPLAY NAME DOES NOT EXIST", invalidPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(invalidPrincipalName))
                .Returns(false);

            var result = (JsonResult)await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, result.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Your user account is not in the correct authorised group. Please contact system administrators",
                _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_OnPostSaveAsync_Given_CurrentUser_Is_Not_Valid_Then_Returns_Validation_Error_Message()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();

            var invalidPrincipalName = "THIS-USER-PRINCIPAL-NAME-DOES-NOT-EXIST@example.com";
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns(("THIS DISPLAY NAME DOES NOT EXIST", invalidPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(invalidPrincipalName))
                .Returns(false);

            var result = (JsonResult)await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, result.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Your user account is not in the correct authorised group. Please contact system administrators",
                _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_OnPostDoneAsync_That_Task_With_No_Assessor_Fails_Validation()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
               .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored)).Returns(TestUser);

            var row = await _dbContext.DbAssessmentAssessData.FirstAsync();
            row.AssessorAdUserId = null;
            await _dbContext.SaveChangesAsync();

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.Contains("Operators: You are not assigned as the Assessor of this task. Please assign the task to yourself and click Save", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_That_Task_With_Assessor_Fails_Validation_If_CurrentUser_Not_Assigned()
        {
            var testUser2 = AdUserHelper.CreateTestUser(_dbContext, 2);

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((testUser2.DisplayName, testUser2.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(testUser2.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored)).Returns(testUser2);

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: {TestUser.DisplayName} is assigned to this task. Please assign the task to yourself and click Save", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_Given_StsDataUsage_Has_Invalid_HpdUsageId_Then_No_Record_Is_Saved()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored)).Returns(TestUser);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.IsOnHold = false;
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.StsDataImpact = new DataImpact() { HpdUsageId = 0 };

            var response = (StatusCodeResult)await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode);

            var stsDataImpact = await _dbContext.DataImpact.SingleOrDefaultAsync(di => di.ProcessId == ProcessId && di.StsUsage);

            Assert.IsNull(stsDataImpact);
        }

        [Test]
        public async Task Test_OnPostSaveAsync_Given_StsDataUsage_Has_Valid_HpdUsageId_Then_Record_Is_Saved()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored)).Returns(TestUser);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.IsOnHold = false;
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.StsDataImpact = new DataImpact()
            {
                ProcessId = ProcessId,
                HpdUsageId = 2,
                FeaturesVerified = false,
                Comments = "This is a test comment",
                //The following properties should be overwritten
                FeaturesSubmitted = true,
                Edited = true,
                DataImpactId = 99999,
                StsUsage = false
            };

            var response = (StatusCodeResult)await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode);

            var stsDataImpact = await _dbContext.DataImpact.SingleOrDefaultAsync(di => di.ProcessId == ProcessId && di.StsUsage);

            Assert.IsNotNull(stsDataImpact);

            Assert.AreEqual(ProcessId, stsDataImpact.ProcessId);
            Assert.AreEqual(_assessModel.StsDataImpact.HpdUsageId, stsDataImpact.HpdUsageId);
            Assert.AreEqual(_assessModel.StsDataImpact.Comments, stsDataImpact.Comments);
            Assert.AreEqual(_assessModel.StsDataImpact.FeaturesVerified, stsDataImpact.FeaturesVerified);

            Assert.IsFalse(stsDataImpact.FeaturesSubmitted);
            Assert.IsFalse(stsDataImpact.Edited);
        }

        [Test]
        public async Task Test_OnPostSaveAsync_That_Setting_Task_To_On_Hold_Creates_A_Row()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = true;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored)).Returns(TestUser);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            await _assessModel.OnPostSaveAsync(ProcessId);

            var onHoldRow = await _dbContext.OnHold.FirstAsync(o => o.ProcessId == ProcessId);

            Assert.NotNull(onHoldRow);
            Assert.NotNull(onHoldRow.OnHoldTime);
            Assert.AreEqual(onHoldRow.OnHoldBy.UserPrincipalName, TestUser.UserPrincipalName);
        }

        [Test]
        public async Task Test_OnPostSaveAsync_That_Setting_Task_To_Off_Hold_Updates_Existing_Row()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored))
                .Returns(TestUser);
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = false;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            await _dbContext.OnHold.AddAsync(new OnHold
            {
                ProcessId = ProcessId,
                OnHoldTime = DateTime.Now,
                OffHoldBy = new AdUser
                {
                    AdUserId = 4,
                    DisplayName = "sdfd",
                    UserPrincipalName = "sdfds@dsfds.coim",
                    LastCheckedDate = DateTime.Now
                },
                WorkflowInstanceId = 1
            });
            await _dbContext.SaveChangesAsync();

            await _assessModel.OnPostSaveAsync(ProcessId);

            var onHoldRow = await _dbContext.OnHold.FirstAsync(o => o.ProcessId == ProcessId);

            Assert.NotNull(onHoldRow);
            Assert.NotNull(onHoldRow.OffHoldTime);
            Assert.AreEqual(onHoldRow.OffHoldBy.UserPrincipalName, TestUser.UserPrincipalName);
        }

        [Test]
        public async Task Test_OnPostSaveAsync_That_Setting_Task_To_On_Hold_Adds_Comment()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _commentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.GetAdUserAsync(A<string>.Ignored))
                .Returns(TestUser);
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = true;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            await _assessModel.OnPostSaveAsync(ProcessId);

            var comments = await _dbContext.Comments.Where(c => c.ProcessId == ProcessId).ToListAsync();

            Assert.GreaterOrEqual(comments.Count, 1);
            Assert.IsTrue(comments.Any(c =>
                c.Text.Contains($"Task {ProcessId} has been put on hold", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public async Task Test_OnPostSaveAsync_That_Setting_Task_To_Off_Hold_Adds_Comment()
        {
            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _commentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = false;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Save", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<DataImpact>.Ignored, A<string>.Ignored, A<AdUser>.Ignored, A<AdUser>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<AdUser>.Ignored))
                .Returns(true);

            _dbContext.OnHold.Add(new OnHold()
            {
                ProcessId = ProcessId,
                OnHoldTime = DateTime.Now.AddDays(-1),
                OnHoldBy = TestUser,
                WorkflowInstanceId = 1
            });

            _dbContext.SaveChanges();

            await _assessModel.OnPostSaveAsync(ProcessId);

            var comments = await _dbContext.Comments.Where(c => c.ProcessId == ProcessId).ToListAsync();

            Assert.GreaterOrEqual(comments.Count, 1);
            Assert.IsTrue(comments.Any(c =>
                c.Text.Contains($"Task {ProcessId} taken off hold", StringComparison.OrdinalIgnoreCase)));
        }


        [Test]
        public async Task Test_OnPostDoneAsync_given_action_done_and_stsdataimpact_usage_not_selected_then_validation_error_message_is_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1234" });
            await _dbContext.SaveChangesAsync();

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";

            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1, Verified = true}
            };

            var hpdUsage1 = new HpdUsage()
            {
                HpdUsageId = 1,
                Name = "HpdUsageName1"
            };
            _assessModel.DataImpacts = new List<DataImpact>()
            {
                new DataImpact() { DataImpactId = 1, HpdUsageId = 1, HpdUsage = hpdUsage1, FeaturesSubmitted = true, ProcessId = 123}
            };

            _assessModel.Team = "HW";

            var response = (JsonResult)await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)AssessCustomHttpStatusCode.WarningsDetected, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Data Impact: STS Usage has not been selected, are you sure you want to continue?", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_given_action_done_and_features_unsubmitted_on_dataimpacts_then_validation_error_message_is_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1234" });
            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1235" });
            await _dbContext.SaveChangesAsync();

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";

            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1, Verified = true},
                new ProductAction() { ProductActionId = 2, ImpactedProduct = "GB1235", ProductActionTypeId = 1, Verified = true}
            };

            var hpdUsage1 = new HpdUsage()
            {
                HpdUsageId = 1,
                Name = "HpdUsageName1"
            };
            var hpdUsage2 = new HpdUsage()
            {
                HpdUsageId = 2,
                Name = "HpdUsageName2"
            };
            _assessModel.DataImpacts = new List<DataImpact>()
            {
                new DataImpact() { DataImpactId = 1, HpdUsageId = 1, HpdUsage = hpdUsage1, ProcessId = 123},
                new DataImpact() {DataImpactId = 2, HpdUsageId = 2, HpdUsage = hpdUsage2, ProcessId = 123}
            };

            _assessModel.Team = "HW";

            var response = (JsonResult)await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)AssessCustomHttpStatusCode.WarningsDetected, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Data Impact: There are incomplete Features Submitted tick boxes.", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_given_action_done_and_unsubmitted_features_on_empty_dataimpacts_then_no_validation_error_message_is_present()
        {
            var correlationId = Guid.NewGuid();

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser.UserPrincipalName))
                .Returns(true);
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(TestUser))
                .Returns(true);

            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1234" });
            _dbContext.CachedHpdEncProduct.Add(new CachedHpdEncProduct() { Name = "GB1235" });
            await _dbContext.SaveChangesAsync();

            var primaryDocumentStatus =
                await _dbContext.PrimaryDocumentStatus.FirstOrDefaultAsync(pds => pds.ProcessId == ProcessId);

            primaryDocumentStatus.CorrelationId = correlationId;

            await _dbContext.SaveChangesAsync();

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";

            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1, Verified = true},
                new ProductAction() { ProductActionId = 2, ImpactedProduct = "GB1235", ProductActionTypeId = 1, Verified = true}
            };

            _assessModel.DataImpacts = new List<DataImpact>();

            _assessModel.StsDataImpact = new DataImpact() { HpdUsageId = 1 };


            var response = await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)HttpStatusCode.OK, ((StatusCodeResult)response).StatusCode);
            Assert.AreEqual(0, _assessModel.ValidationErrorMessages.Count);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(
                                            nameof(ProgressWorkflowInstanceEvent),
                                            A<ProgressWorkflowInstanceEvent>.That.Matches(p =>
                                                                                                        p.CorrelationId == correlationId
                                                                                                        && p.ProcessId == ProcessId
                                                                                                        && p.FromActivity == WorkflowStage.Assess
                                                                                                        && p.ToActivity == WorkflowStage.Verify
                    ))).MustHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_given_action_confirmedDone_must_not_run_validation()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Team = "HW";

            _assessModel.Verifier = TestUser;

            _assessModel.RecordProductAction = new List<ProductAction>();
            _assessModel.DataImpacts = new List<DataImpact>();

            var childProcessId = 555;

            _dbContext.WorkflowInstance.Add(new WorkflowInstance()
            {
                WorkflowInstanceId = 2,
                ProcessId = childProcessId,
                ActivityName = "Assess",
                SerialNumber = "555_456",
                ParentProcessId = ProcessId,
                Status = WorkflowStatus.Started.ToString()

            });

            await _dbContext.SaveChangesAsync();

            _pageValidationHelper = A.Fake<IPageValidationHelper>();

            _assessModel = new AssessModel(_dbContext, _fakeEventServiceApiClient, _fakeLogger, _fakeDbAssessmentCommentsHelper, _fakeAdDirectoryService, _pageValidationHelper, _fakeCarisProjectHelper, _generalConfig, _fakePortalUserDbService);


            await _assessModel.OnPostDoneAsync(ProcessId, "ConfirmedDone");

            // Assert
            A.CallTo(() => _pageValidationHelper.CheckAssessPageForErrors(A<string>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<bool>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<List<ProductAction>>.Ignored,
                                                                                A<List<DataImpact>>.Ignored,
                                                                                A<DataImpact>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<AdUser>.Ignored,
                                                                                A<AdUser>.Ignored,
                                                                                A<List<string>>.Ignored,
                                                                                A<string>.Ignored,
                                                                                A<AdUser>.Ignored))
                                                            .MustNotHaveHappened();

            A.CallTo(() => _pageValidationHelper.CheckAssessPageForWarnings(A<string>.Ignored,
                                                                                        A<List<DataImpact>>.Ignored,
                                                                                        A<DataImpact>.Ignored,
                                                                                        A<List<string>>.Ignored))
                                                            .MustNotHaveHappened();


            A.CallTo(() => _fakeEventServiceApiClient.PostEvent(
                                                                                nameof(ProgressWorkflowInstanceEvent),
                                                                                A<ProgressWorkflowInstanceEvent>.Ignored))
                                                            .MustHaveHappened();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Test_OnPostDoneAsync_Given_ProductActionedChangeDetails_Exceeds_Character_Limit_And_ProductActioned_Is_Provided_Then_Validation_Error_Message_Is_Present(bool productActioned)
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.ProductActioned = productActioned;
            //Set ProductActionChangeDetails to 251 characters
            _assessModel.ProductActionChangeDetails = string.Empty;
            for (int i = 0; i < 25; i++)
            {
                _assessModel.ProductActionChangeDetails += "0123456789";
            }
            _assessModel.ProductActionChangeDetails += "0";

            var response = (JsonResult)await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Record Product Action: Please ensure product action change details does not exceed 250 characters", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Test_OnPostDoneSave_Given_ProductActionedChangeDetails_Exceeds_Character_Limit_And_ProductActioned_Is_Provided_Then_Validation_Error_Message_Is_Present(bool productActioned)
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.ProductActioned = productActioned;
            //Set ProductActionChangeDetails to 251 characters
            _assessModel.ProductActionChangeDetails = string.Empty;
            for (int i = 0; i < 25; i++)
            {
                _assessModel.ProductActionChangeDetails += "0123456789";
            }
            _assessModel.ProductActionChangeDetails += "0";

            var response = (JsonResult)await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Record Product Action: Please ensure product action change details does not exceed 250 characters", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_where_ProductActioned_ticked_and_no_ProductActionChangeDetails_entered_then_validation_error_message_is_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "";

            var response = (JsonResult)await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Record Product Action: Please ensure you have entered product action change details", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostSaveAsync_where_ProductActioned_ticked_and_no_ProductActionChangeDetails_entered_then_validation_error_message_is_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "";

            var response = (JsonResult)await _assessModel.OnPostSaveAsync(ProcessId);

            Assert.AreEqual((int)AssessCustomHttpStatusCode.FailedValidation, response.StatusCode);
            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Record Product Action: Please ensure you have entered product action change details", _assessModel.ValidationErrorMessages);
            A.CallTo(() =>
                    _fakeEventServiceApiClient.PostEvent(A<string>.Ignored, A<ProgressWorkflowInstanceEvent>.Ignored))
                .WithAnyArguments().MustNotHaveHappened();
        }

        [Test]
        public async Task Test_OnPostDoneAsync_where_ProductActioned_ticked_and_ProductActionChangeDetails_entered_then_validation_error_message_is_not_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "Test change details";

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure you have entered product action change details");
        }

        [Test]
        public async Task Test_OnPostSaveAsync_where_ProductActioned_ticked_and_ProductActionChangeDetails_entered_then_validation_error_message_is_not_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = true;
            _assessModel.ProductActionChangeDetails = "Test change details";

            await _assessModel.OnPostSaveAsync(ProcessId);

            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure you have entered product action change details");
        }

        [Test]
        public async Task Test_OnPostDoneAsync_where_ProductActioned_not_ticked_then_validation_error_messages_are_not_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = false;

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure you have entered product action change details");
            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure impacted product is fully populated");
            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: More than one of the same Impacted Products selected");
        }

        [Test]
        public async Task Test_OnPostSaveAsync_where_ProductActioned_not_ticked_then_validation_error_messages_are_not_present()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((TestUser.DisplayName, TestUser.UserPrincipalName));
            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.Assessor = TestUser;
            _assessModel.Verifier = TestUser;
            _assessModel.Team = "HW";
            _assessModel.TaskType = "TaskType";
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.ProductActioned = false;

            await _assessModel.OnPostSaveAsync(ProcessId);

            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure you have entered product action change details");
            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: Please ensure impacted product is fully populated");
            CollectionAssert.DoesNotContain(_assessModel.ValidationErrorMessages, "Record Product Action: More than one of the same Impacted Products selected");
        }



        [TestCase("Done")]
        [TestCase("ConfirmedDone")]
        public async Task Test_OnPostDoneAsync_When_All_Steps_Were_Successful_Then_Status_Is_Updating_And_Progress_Event_Is_Fired_And_Comment_Is_Added(string action)
        {
            var correlationId = Guid.NewGuid();
            var userFullName = TestUser.DisplayName;
            var userEmail = TestUser.UserPrincipalName;

            A.CallTo(() => _fakeAdDirectoryService.GetUserDetails(A<ClaimsPrincipal>.Ignored))
                .Returns((userFullName, userEmail));

            A.CallTo(() => _fakePortalUserDbService.ValidateUserAsync(userEmail))
                .Returns(true);

            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors(
                null,
                null,
                null,
                null,
                null,
                null,
                A<bool>.Ignored,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)).WithAnyArguments().Returns(true);

            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForWarnings(
                null,
                null,
                null,
                null)).WithAnyArguments().Returns(false);

            var primaryDocumentStatus =
                await _dbContext.PrimaryDocumentStatus.FirstOrDefaultAsync(pds => pds.ProcessId == ProcessId);

            primaryDocumentStatus.CorrelationId = correlationId;

            await _dbContext.SaveChangesAsync();

            _assessModel = new AssessModel(
                            _dbContext,
                            _fakeEventServiceApiClient,
                            _fakeLogger,
                            _fakeDbAssessmentCommentsHelper,
                            _fakeAdDirectoryService,
                            _fakePageValidationHelper,
                            _fakeCarisProjectHelper,
                            _generalConfig, _fakePortalUserDbService);

            _assessModel.DataImpacts = new List<DataImpact>();

            await _assessModel.OnPostDoneAsync(ProcessId, action);

            var workflowInstance =
                await _dbContext.WorkflowInstance.FirstOrDefaultAsync(wi => wi.ProcessId == ProcessId);

            Assert.IsNotNull(workflowInstance);
            Assert.AreEqual(WorkflowStatus.Updating.ToString(), workflowInstance.Status);
            A.CallTo(() =>
                _fakeEventServiceApiClient.PostEvent(
                    nameof(ProgressWorkflowInstanceEvent),
                    A<ProgressWorkflowInstanceEvent>.That.Matches(p =>
                        p.CorrelationId == correlationId
                        && p.ProcessId == ProcessId
                        && p.FromActivity == WorkflowStage.Assess
                        && p.ToActivity == WorkflowStage.Verify
                    ))).MustHaveHappened();
            A.CallTo(() => _fakeDbAssessmentCommentsHelper.AddComment(
                "Task progression from Assess to Verify has been triggered",
                ProcessId,
                workflowInstance.WorkflowInstanceId,
                userEmail)).MustHaveHappened();
        }
    }
}
