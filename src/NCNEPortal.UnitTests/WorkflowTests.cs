﻿using Common.Helpers;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCNEPortal.Auth;
using NCNEPortal.Calculators;
using NCNEPortal.Configuration;
using NCNEPortal.Helpers;
using NCNEWorkflowDatabase.EF;
using NCNEWorkflowDatabase.EF.Models;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NCNEPortal.UnitTests
{
    [TestFixture]
    public class WorkflowTests
    {
        private NcneWorkflowDbContext _dbContext;
        private WorkflowModel _workflowModel;
        private ILogger<WorkflowModel> _fakeLogger;
        private IUserIdentityService _fakeUserIdentityService;
        private ICommentsHelper _fakecommentsHelper;
        private ICarisProjectHelper _carisProjectHelper;
        private IMilestoneCalculator _milestoneCalculator;
        private IDirectoryService _fakeDirectoryService;
        private IPageValidationHelper _pageValidationHelper;
        private IStageTypeFactory _stageTypeFactory;
        private readonly IOptions<GeneralConfig> _fakeGeneralConfig;
        private int ProcessId { get; set; }

        [SetUp]
        public void Setup()
        {
            var dbContextOptions = new DbContextOptionsBuilder<NcneWorkflowDbContext>()
                .UseInMemoryDatabase(databaseName: "inmemory")
                .Options;


            _dbContext = new NcneWorkflowDbContext(dbContextOptions);
            _fakecommentsHelper = A.Fake<ICommentsHelper>();

            ProcessId = 123;


            _fakeUserIdentityService = A.Fake<IUserIdentityService>();
            _fakeDirectoryService = A.Fake<IDirectoryService>();

            _fakeLogger = A.Dummy<ILogger<WorkflowModel>>();

            _pageValidationHelper = new PageValidationHelper(_dbContext, _fakeUserIdentityService, _fakeDirectoryService);

            _stageTypeFactory = new StageTypeFactory(_dbContext);

            _workflowModel = new WorkflowModel(_dbContext, _fakeLogger, _fakeUserIdentityService, _fakecommentsHelper, _carisProjectHelper,
                                  _fakeGeneralConfig, _milestoneCalculator, _fakeDirectoryService, _pageValidationHelper);


            AddStageTypes(_dbContext);

        }


        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
        }

        [Test]
        public async Task Test_validating_adoption_charttype_for_repromate_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = null;
            _workflowModel.ChartType = "Adoption";
            _workflowModel.PublicationDate = null;
            _workflowModel.Compiler = "Stuart";

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("Task Information: Repromat Date cannot be empty"));

        }

        [Test]
        public async Task Test_validating_Primary_charttype_for_publication_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = null;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = null;
            _workflowModel.Compiler = "Stuart";

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("Task Information: Publication Date cannot be empty"));

        }

        [Test]
        public async Task Test_validating_the_workflow_for_Duration()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 0;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("Task Information: Duration cannot be empty"));

        }
        [Test]
        public async Task Test_validating_the_workflow_for_Compiler()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = null;
            _workflowModel.Dating = 0;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("Task Information: Compiler cannot be empty"));

        }

        [Test]
        public async Task Test_validating_the_taskinformation_all_valid()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuat";
            _workflowModel.Dating = 1;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 0);


        }

        [Test]
        public async Task Test_3ps_Expected_date_without_sent_to_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 1;
            _workflowModel.SentTo3Ps = true;
            _workflowModel.SendDate3ps = null;
            _workflowModel.ExpectedReturnDate3ps = DateTime.Now;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("3PS : Please enter date sent to 3PS before entering expected return date"));
        }


        [Test]
        public async Task Test_3ps_Actual_return_date_without_sent_to_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 1;
            _workflowModel.SentTo3Ps = true;
            _workflowModel.SendDate3ps = null;
            _workflowModel.ActualReturnDate3ps = DateTime.Now;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("3PS : Please enter date sent to 3PS before entering actual return date"));
        }
        [Test]
        public async Task Test_3ps_Expected_return_date_earlier_than_sent_to_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 1;
            _workflowModel.SentTo3Ps = true;
            _workflowModel.SendDate3ps = DateTime.Now;
            _workflowModel.ExpectedReturnDate3ps = DateTime.Now.AddDays(-2);

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("3PS : Expected return date cannot be earlier than Sent to 3PS date"));
        }

        [Test]
        public async Task Test_3ps_Actual_return_date_earlier_than_sent_to_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 1;
            _workflowModel.SentTo3Ps = true;
            _workflowModel.SendDate3ps = DateTime.Now;
            _workflowModel.ActualReturnDate3ps = DateTime.Now.AddDays(-2);

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("3PS : Actual return date cannot be earlier than Sent to 3PS date"));
        }

        [Test]
        public async Task Test_3ps_Entering_Actual_return_date_without_expected_return_date()
        {
            _workflowModel.ProcessId = 123;
            _workflowModel.RepromatDate = DateTime.Now;
            _workflowModel.ChartType = "Primary";
            _workflowModel.PublicationDate = DateTime.Now;
            _workflowModel.Compiler = "Stuart";
            _workflowModel.Dating = 1;
            _workflowModel.SentTo3Ps = true;
            _workflowModel.SendDate3ps = DateTime.Now;
            _workflowModel.ActualReturnDate3ps = DateTime.Now;
            _workflowModel.ExpectedReturnDate3ps = null;

            await _workflowModel.OnPostSaveAsync(_workflowModel.ProcessId, _workflowModel.ChartType);

            Assert.GreaterOrEqual(_workflowModel.ValidationErrorMessages.Count, 1);
            Assert.IsTrue(_workflowModel.ValidationErrorMessages.Contains("3PS : Please enter expected return date before entering actual return date"));
        }

        [Test]
        public async Task Test_StageType_Factory_For_Adoption_ChartType()
        {
            var stagetypes = _stageTypeFactory.GetTaskStages("Adoption");

            var Sdra = stagetypes.Where(t => t.Name == "With SDRA");

            Assert.AreEqual(stagetypes.Count, 18);
            Assert.AreEqual(Sdra.Count(), 1);
        }


        [Test]
        public async Task Test_StageType_Factory_For_Primary_ChartType()
        {
            var stagetypes = _stageTypeFactory.GetTaskStages("Primary");

            var Sdra = stagetypes.Where(t => t.Name == "With SDRA");

            Assert.AreEqual(stagetypes.Count, 16);
            Assert.AreEqual(Sdra.Count(), 0);
        }

        private void AddStageTypes(NcneWorkflowDbContext dbContext)
        {
            if (!File.Exists(@"Data\TaskStageType.json")) throw new FileNotFoundException(@"Data\TaskStageType.json");

            var jsonString = File.ReadAllText(@"Data\TaskStageType.json");
            var stageType = JsonConvert.DeserializeObject<IEnumerable<TaskStageType>>(jsonString);

            dbContext.TaskStageType.AddRange(stageType);

            _dbContext.SaveChanges();
        }

    }

}
