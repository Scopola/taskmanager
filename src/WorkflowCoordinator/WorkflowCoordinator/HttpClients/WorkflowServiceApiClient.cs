﻿using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WorkflowCoordinator.Config;

namespace WorkflowCoordinator.HttpClients
{
    public class WorkflowServiceApiClient : IWorkflowServiceApiClient
    {
        private readonly IOptions<GeneralConfig> _generalConfig;
        private readonly HttpClient _httpClient;

        public WorkflowServiceApiClient(HttpClient httpClient, IOptions<GeneralConfig> generalConfig)
        {
            _generalConfig = generalConfig;
            _httpClient = httpClient;
        }

        public async Task<int> CreateWorkflowInstance()
        {
            //TODO: Get Workflows to get WorkflowID for "DB Assessment"; this is the main workflow id and not the instance id

            //TODO: Create Workflow Instance
            //TODO: Get SerialNumber
            return 0;
        }
    }
}
