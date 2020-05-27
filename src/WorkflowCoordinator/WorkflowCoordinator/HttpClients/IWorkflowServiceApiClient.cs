﻿using System.Threading.Tasks;
using WorkflowCoordinator.Models;

namespace WorkflowCoordinator.HttpClients
{
    public interface IWorkflowServiceApiClient
    {
        Task<int> CreateWorkflowInstance(int dbAssessmentWorkflowId);
        Task<int> GetDBAssessmentWorkflowId();
        Task<string> GetWorkflowInstanceSerialNumber(int workflowInstanceId);
        Task TerminateWorkflowInstance(string serialNumber);
        Task<bool> ProgressWorkflowInstance(string k2SerialNumber);
        Task<K2TaskData> GetWorkflowInstanceData(int workflowInstanceId);
    }
}