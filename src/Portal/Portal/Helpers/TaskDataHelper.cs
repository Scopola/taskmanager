﻿using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Interfaces;

namespace Portal.Helpers
{
    public class TaskDataHelper : ITaskDataHelper
    {
        private readonly WorkflowDbContext _dbContext;

        public TaskDataHelper(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ITaskData> GetTaskData(string activityName, int processId)
        {
            switch (activityName)
            {
                case "Review":
                    return await _dbContext.DbAssessmentReviewData
                        .FirstOrDefaultAsync(r => r.ProcessId == processId);
                case "Assess":
                    return await _dbContext.DbAssessmentAssessData
                        .FirstOrDefaultAsync(r => r.ProcessId == processId);
                case "Verify":
                    return await _dbContext.DbAssessmentVerifyData
                        .FirstOrDefaultAsync(r => r.ProcessId == processId);
                default:
                    throw new NotImplementedException($"ActivityName not found: {activityName}");
            }
        }

        public async Task<IProductActionData> GetProductActionData(string activityName, int processId)
        {
            switch (activityName)
            {
                case "Assess":
                    return await _dbContext.DbAssessmentAssessData
                        .FirstOrDefaultAsync(r => r.ProcessId == processId);
                case "Verify":
                    return await _dbContext.DbAssessmentVerifyData
                        .FirstOrDefaultAsync(r => r.ProcessId == processId);
                default:
                    throw new NotImplementedException($"ActivityName not found: {activityName}");
            }
        }
    }
}
