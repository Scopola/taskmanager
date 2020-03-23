﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Common.Helpers;
using Common.Helpers.Auth;
using FakeItEasy;
using HpdDatabase.EF.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Portal.Auth;
using Portal.Configuration;
using Portal.Helpers;
using Portal.HttpClients;
using Portal.Pages.DbAssessment;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.UnitTests
{
    [TestFixture]
    public class AssessTests
    {
        private WorkflowDbContext _dbContext;
        private HpdDbContext _hpDbContext;
        private AssessModel _assessModel;
        private int ProcessId { get; set; }
        private ILogger<AssessModel> _fakeLogger;
        private IWorkflowServiceApiClient _fakeWorkflowServiceApiClient;
        private IEventServiceApiClient _fakeEventServiceApiClient;
        private IAdDirectoryService _fakeAdDirectoryService;
        private IPortalUserDbService _fakePortalUserDbService; private ICommentsHelper _commentsHelper;
        private ICommentsHelper _fakeCommentsHelper;
        private IPageValidationHelper _pageValidationHelper;
        private IPageValidationHelper _fakePageValidationHelper;
        private ICarisProjectHelper _fakeCarisProjectHelper;
        private IOptions<GeneralConfig> _generalConfig;

        [SetUp]
        public void Setup()
        {
            var dbContextOptions = new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;

            _dbContext = new WorkflowDbContext(dbContextOptions);

            _fakeWorkflowServiceApiClient = A.Fake<IWorkflowServiceApiClient>();
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
                Assessor = "TestUser"
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

            var hpdDbContextOptions = new DbContextOptionsBuilder<HpdDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;

            _hpDbContext = new HpdDbContext(hpdDbContextOptions);

            _commentsHelper = new CommentsHelper(_dbContext);
            _fakeCommentsHelper = A.Fake<ICommentsHelper>();

            _fakeAdDirectoryService = A.Fake<IAdDirectoryService>();
            _fakePortalUserDbService = A.Fake<IPortalUserDbService>();

            _fakeLogger = A.Dummy<ILogger<AssessModel>>();

            _pageValidationHelper = new PageValidationHelper(_dbContext, _hpDbContext, _fakeAdDirectoryService, _fakePortalUserDbService);
            _fakePageValidationHelper = A.Fake<IPageValidationHelper>();

            _assessModel = new AssessModel(_dbContext, null, _fakeWorkflowServiceApiClient, _fakeEventServiceApiClient, _fakeLogger, _fakeCommentsHelper, _fakeAdDirectoryService, _pageValidationHelper, _fakeCarisProjectHelper, _generalConfig);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
        }

        [Test]
        public async Task Test_entering_an_empty_ion_activityCode_sourceCategory_tasktype_team_assessor_results_in_validation_error_message()
        {
            _hpDbContext.CarisProducts.Add(new CarisProduct()
            { ProductName = "GB1234", ProductStatus = "Active", TypeKey = "ENC" });
            await _hpDbContext.SaveChangesAsync();

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "";
            _assessModel.ActivityCode = "";
            _assessModel.SourceCategory = "";
            _assessModel.TaskType = "";
            _assessModel.Team = "";

            _assessModel.Assessor = "TestUser";
            _assessModel.Verifier = "TestUser";
            _assessModel.DataImpacts = new List<DataImpact>();

            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 5);
            Assert.Contains($"Task Information: Ion cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Activity code cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Source category cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Task type cannot be empty", _assessModel.ValidationErrorMessages);
            Assert.Contains($"Task Information: Team cannot be empty", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_an_empty_Verifier_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Assessor = "TestUser";

            _assessModel.Verifier = "";
            _assessModel.DataImpacts = new List<DataImpact>();

            A.CallTo(() => _fakePortalUserDbService.ValidateUser("TestUser"))
                .Returns(true);


            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Verifier cannot be empty", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_an_empty_assessor_results_in_validation_error_message()
        {
            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Verifier = "TestUser";

            _assessModel.Assessor = "";
            _assessModel.DataImpacts = new List<DataImpact>();

            A.CallTo(() => _fakePortalUserDbService.ValidateUser("TestUser"))
                .Returns(true);

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Assessor cannot be empty", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_duplicate_hpd_usages_in_dataImpact_results_in_validation_error_message()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";

            _assessModel.Assessor = "TestUser";
            _assessModel.Verifier = "TestUser";

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

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Data Impact: More than one of the same Usage selected", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_non_existing_impactedProduct_in_productAction_results_in_validation_error_message()
        {

            _hpDbContext.CarisProducts.Add(new CarisProduct()
            { ProductName = "GB1234", ProductStatus = "Active", TypeKey = "ENC" });
            await _hpDbContext.SaveChangesAsync();


            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Assessor = "TestUser";
            _assessModel.Verifier = "TestUser";

            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.RecordProductAction = new List<ProductAction>
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB5678", ProductActionTypeId = 1}
            };


            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Record Product Action: Impacted product GB5678 does not exist", _assessModel.ValidationErrorMessages);
        }


        [Test]
        public async Task Test_entering_duplicate_impactedProducts_in_productAction_results_in_validation_error_message()
        {
            _hpDbContext.CarisProducts.Add(new CarisProduct()
            { ProductName = "GB1234", ProductStatus = "Active", TypeKey = "ENC" });
            await _hpDbContext.SaveChangesAsync();

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Assessor = "TestUser";
            _assessModel.Verifier = "TestUser";
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.RecordProductAction = new List<ProductAction>
            {
                new ProductAction() { ProductActionId = 1, ImpactedProduct = "GB1234", ProductActionTypeId = 1},
                new ProductAction() { ProductActionId = 2, ImpactedProduct = "GB1234", ProductActionTypeId = 1}
            };

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Record Product Action: More than one of the same Impacted Products selected", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_invalid_username_for_assessor_results_in_validation_error_message()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUser("KnownUser"))
                .Returns(true);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser("TestUser"))
                .Returns(false);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Assessor = "TestUser";
            _assessModel.Verifier = "KnownUser";

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Unable to set Assessor to unknown user {_assessModel.Assessor}", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_entering_invalid_username_for_verifier_results_in_validation_error_message()
        {
            A.CallTo(() => _fakePortalUserDbService.ValidateUser("KnownUser"))
                .Returns(true);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser("TestUser"))
                .Returns(false);

            _assessModel.Ion = "Ion";
            _assessModel.ActivityCode = "ActivityCode";
            _assessModel.SourceCategory = "SourceCategory";
            _assessModel.TaskType = "TaskType";
            _assessModel.Team = "Home Waters";
            _assessModel.Assessor = "KnownUser";
            _assessModel.Verifier = "TestUser";

            await _assessModel.OnPostDoneAsync(ProcessId, "Save");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains($"Operators: Unable to set Verifier to unknown user {_assessModel.Verifier}", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_That_Task_With_No_Assessor_Fails_Validation_On_Done()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("This User"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Review", "Assess"))
                .Returns(true);

            var row = await _dbContext.DbAssessmentAssessData.FirstAsync();
            row.Assessor = "";
            await _dbContext.SaveChangesAsync();

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.Contains("Operators: You are not assigned as the Assessor of this task. Please assign the task to yourself and click Save", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_That_Task_With_Assessor_Fails_Validation_If_CurrentUser_Not_Assigned_At_Done()
        {
            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("TestUser2"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Assess", "Verify"))
                .Returns(true);

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            Assert.GreaterOrEqual(_assessModel.ValidationErrorMessages.Count, 1);
            Assert.Contains("Operators: TestUser is assigned to this task. Please assign the task to yourself and click Save", _assessModel.ValidationErrorMessages);
        }

        [Test]
        public async Task Test_That_Setting_Task_To_On_Hold_Creates_A_Row()
        {
            _assessModel = new AssessModel(_dbContext, null, _fakeWorkflowServiceApiClient, _fakeEventServiceApiClient, _fakeLogger, _fakeCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = "TestUser2";
            _assessModel.Team = "Home Waters";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = true;

            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("TestUser2"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Assess", "Verify"))
                .Returns(true);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Done", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(true);

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            var onHoldRow = await _dbContext.OnHold.FirstAsync(o => o.ProcessId == ProcessId);

            Assert.NotNull(onHoldRow);
            Assert.NotNull(onHoldRow.OnHoldTime);
            Assert.AreEqual(onHoldRow.OnHoldUser, "TestUser2");
        }

        [Test]
        public async Task Test_That_Setting_Task_To_Off_Hold_Updates_Existing_Row()
        {
            _assessModel = new AssessModel(_dbContext, null, _fakeWorkflowServiceApiClient, _fakeEventServiceApiClient, _fakeLogger, _fakeCommentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = "TestUser2";
            _assessModel.Team = "Home Waters";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = false;

            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("TestUser2"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Assess", "Verify"))
                .Returns(true);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Done", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(true);

            await _dbContext.OnHold.AddAsync(new OnHold
            {
                ProcessId = ProcessId,
                OnHoldTime = DateTime.Now,
                OffHoldUser = "TestUser2",
                WorkflowInstanceId = 1
            });
            await _dbContext.SaveChangesAsync();

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            var onHoldRow = await _dbContext.OnHold.FirstAsync(o => o.ProcessId == ProcessId);

            Assert.NotNull(onHoldRow);
            Assert.NotNull(onHoldRow.OffHoldTime);
            Assert.AreEqual(onHoldRow.OffHoldUser, "TestUser2");
        }

        [Test]
        public async Task Test_That_Setting_Task_To_On_Hold_Adds_Comment()
        {
            _assessModel = new AssessModel(_dbContext, null, _fakeWorkflowServiceApiClient, _fakeEventServiceApiClient, _fakeLogger, _commentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = "TestUser2";
            _assessModel.Team = "Home Waters";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = true;

            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("TestUser2"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Assess", "Assess"))
                .Returns(true);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Done", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(true);

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            var comments = await _dbContext.Comment.Where(c => c.ProcessId == ProcessId).ToListAsync();

            Assert.GreaterOrEqual(comments.Count, 1);
            Assert.IsTrue(comments.Any(c =>
                c.Text.Contains($"Task {ProcessId} has been put on hold", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public async Task Test_That_Setting_Task_To_Off_Hold_Adds_Comment()
        {
            _assessModel = new AssessModel(_dbContext, null, _fakeWorkflowServiceApiClient, _fakeEventServiceApiClient, _fakeLogger, _commentsHelper, _fakeAdDirectoryService,
                _fakePageValidationHelper, _fakeCarisProjectHelper, _generalConfig);

            A.CallTo(() => _fakePortalUserDbService.ValidateUser(A<string>.Ignored))
                .Returns(true);
            _assessModel.Assessor = "TestUser2";
            _assessModel.Team = "Home Waters";
            _assessModel.RecordProductAction = new List<ProductAction>()
            {
                new ProductAction() {ImpactedProduct = "GB1234", ProcessId = 123, ProductActionTypeId = 1}
            };
            _assessModel.DataImpacts = new List<DataImpact>();
            _assessModel.IsOnHold = false;

            A.CallTo(() => _fakeAdDirectoryService.GetFullNameForUserAsync(A<ClaimsPrincipal>.Ignored))
                .Returns(Task.FromResult("TestUser2"));
            A.CallTo(() => _fakeWorkflowServiceApiClient.ProgressWorkflowInstance(123, "123_sn", "Assess", "Verify"))
                .Returns(true);
            A.CallTo(() => _fakePageValidationHelper.CheckAssessPageForErrors("Done", A<string>.Ignored, A<string>.Ignored, A<string>.Ignored,
                    A<string>.Ignored, A<List<ProductAction>>.Ignored,
                    A<List<DataImpact>>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<List<string>>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(true);

            _dbContext.OnHold.Add(new OnHold()
            {
                ProcessId = ProcessId,
                OnHoldTime = DateTime.Now.AddDays(-1),
                OnHoldUser = "TestUser",
                WorkflowInstanceId = 1
            });

            _dbContext.SaveChanges();

            await _assessModel.OnPostDoneAsync(ProcessId, "Done");

            var comments = await _dbContext.Comment.Where(c => c.ProcessId == ProcessId).ToListAsync();

            Assert.GreaterOrEqual(comments.Count, 1);
            Assert.IsTrue(comments.Any(c =>
                c.Text.Contains($"Task {ProcessId} taken off hold", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
