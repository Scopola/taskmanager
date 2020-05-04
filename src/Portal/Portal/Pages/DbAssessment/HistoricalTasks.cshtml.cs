﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portal.Calculators;
using Portal.Configuration;
using Portal.Models;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.Pages.DbAssessment
{
    public class HistoricalTasksModel : PageModel
    {
        private readonly WorkflowDbContext _dbContext;
        private readonly IDmEndDateCalculator _dmEndDateCalculator;
        private readonly IMapper _mapper;
        private readonly IOptions<GeneralConfig> _generalConfig;

        [BindProperty(SupportsGet = true)]
        public HistoricalTasksSearchParameters SearchParameters { get; set; }

        public List<HistoricalTasksData> HistoricalTasks { get; set; }

        public List<string> ErrorMessages { get; set; }

        public HistoricalTasksModel(
                                    WorkflowDbContext dbContext,
                                    IDmEndDateCalculator dmEndDateCalculator,
                                    IMapper mapper,
                                    IOptions<GeneralConfig> generalConfig)
        {
            _dbContext = dbContext;
            _dmEndDateCalculator = dmEndDateCalculator;
            _mapper = mapper;
            _generalConfig = generalConfig;
            ErrorMessages = new List<string>();
            HistoricalTasks = new List<HistoricalTasksData>();
        }

        public async Task OnGetAsync()
        {
            var workflows = await _dbContext.WorkflowInstance
                .Include(a => a.AssessmentData)
                .Include(d => d.DbAssessmentReviewData)
                .Include(vd => vd.DbAssessmentVerifyData)
                .Where(wi =>
                    wi.Status == WorkflowStatus.Completed.ToString() ||
                    wi.Status == WorkflowStatus.Terminated.ToString())
                .OrderByDescending(wi => wi.ActivityChangedAt)
                .Take(_generalConfig.Value.HistoricalTasksInitialNumberOfRecords)
                .ToListAsync();


            HistoricalTasks = _mapper.Map<List<WorkflowInstance>, List<HistoricalTasksData>>(workflows);

            foreach (var instance in workflows)
            {
                var task = HistoricalTasks.First(t => t.ProcessId == instance.ProcessId);
                SetUsersOnTask(instance, task);

                var taskType = GetTaskType(instance, task);

                if (instance.AssessmentData.EffectiveStartDate.HasValue)
                {
                    var result = _dmEndDateCalculator.CalculateDmEndDate(
                        instance.AssessmentData.EffectiveStartDate.Value,
                        taskType,
                        instance.ActivityName);

                    task.DmEndDate = result.dmEndDate;

                }

            }
        }

        public async Task OnPost()
        {
            // TODO: Validate search parameters
            // TODO: Get results
            // TODO: Check results count. if zero or too large then warn user
            //ErrorMessages = new List<string>()  
            //{
            //    "Error1",
            //    "Error2"
            //};

            var workflows = await _dbContext.WorkflowInstance
                .Include(a => a.AssessmentData)
                .Include(d => d.DbAssessmentReviewData)
                .Include(vd => vd.DbAssessmentVerifyData)
                .Where(wi =>
                    (wi.Status == WorkflowStatus.Completed.ToString() || wi.Status == WorkflowStatus.Terminated.ToString())
                    && (
                        (!SearchParameters.ProcessId.HasValue || wi.ProcessId == SearchParameters.ProcessId.Value)
                        && (!SearchParameters.SourceDocumentId.HasValue || wi.AssessmentData.PrimarySdocId == SearchParameters.SourceDocumentId.Value)
                        && (string.IsNullOrWhiteSpace(SearchParameters.RsdraNumber) || wi.AssessmentData.RsdraNumber.ToUpper().Contains(SearchParameters.RsdraNumber.ToUpper()))
                        && (string.IsNullOrWhiteSpace(SearchParameters.SourceDocumentName) || wi.AssessmentData.SourceDocumentName.ToUpper().Contains(SearchParameters.SourceDocumentName.ToUpper()))
                       ))
                       .OrderByDescending(wi => wi.ActivityChangedAt)
                .ToListAsync();

            HistoricalTasks = _mapper.Map<List<WorkflowInstance>, List<HistoricalTasksData>>(workflows);

            foreach (var instance in workflows)
            {
                var task = HistoricalTasks.First(t => t.ProcessId == instance.ProcessId);
                SetUsersOnTask(instance, task);

                var taskType = GetTaskType(instance, task);

                if (instance.AssessmentData.EffectiveStartDate.HasValue)
                {
                    var result = _dmEndDateCalculator.CalculateDmEndDate(
                        instance.AssessmentData.EffectiveStartDate.Value,
                        taskType,
                        instance.ActivityName);

                    task.DmEndDate = result.dmEndDate;

                }

            }


        }

        private void SetUsersOnTask(WorkflowInstance instance, HistoricalTasksData task)
        {
            switch (task.TaskStage)
            {
                case WorkflowStage.Review:
                    task.Reviewer = instance.DbAssessmentReviewData.Reviewer;
                    task.Assessor = instance.DbAssessmentReviewData.Assessor;
                    task.Verifier = instance.DbAssessmentReviewData.Verifier;
                    break;
                case WorkflowStage.Verify:
                    task.Reviewer = instance.DbAssessmentVerifyData.Reviewer;
                    task.Assessor = instance.DbAssessmentVerifyData.Assessor;
                    task.Verifier = instance.DbAssessmentVerifyData.Verifier;
                    break;
                default:
                    throw new NotImplementedException($"{task.TaskStage} is not implemented.");
            }
        }

        private string GetTaskType(WorkflowInstance instance, HistoricalTasksData task)
        {
            switch (task.TaskStage)
            {
                case WorkflowStage.Review:
                    return instance.DbAssessmentReviewData.TaskType;
                case WorkflowStage.Verify:
                    return instance.DbAssessmentVerifyData.TaskType;
                default:
                    throw new NotImplementedException($"'{instance.ActivityName}' not implemented");
            }
        }

    }
}