﻿using System;
using System.Threading.Tasks;
using AutoMapper;
using Common.Helpers;
using Common.Messages.Commands;
using Common.Messages.Enums;
using Common.Messages.Events;
using DataServices.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NServiceBus;
using WorkflowCoordinator.Config;
using WorkflowCoordinator.HttpClients;
using WorkflowCoordinator.Messages;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;
using Task = System.Threading.Tasks.Task;

namespace WorkflowCoordinator.Sagas
{
    public class StartDbAssessmentSaga : Saga<StartDbAssessmentSagaData>,
                                            IAmStartedByMessages<StartDbAssessmentCommand>,
                                            IHandleMessages<RetrieveAssessmentDataCommand>
    {
        private readonly IOptionsSnapshot<GeneralConfig> _generalConfig;
        private readonly IDataServiceApiClient _dataServiceApiClient;
        private readonly IWorkflowServiceApiClient _workflowServiceApiClient;
        private readonly WorkflowDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILogger<StartDbAssessmentSaga> _logger;

        public StartDbAssessmentSaga(IOptionsSnapshot<GeneralConfig> generalConfig,
            IDataServiceApiClient dataServiceApiClient,
            IWorkflowServiceApiClient workflowServiceApiClient,
            WorkflowDbContext dbContext, IMapper mapper,
            ILogger<StartDbAssessmentSaga> logger)
        {
            _generalConfig = generalConfig;
            _dataServiceApiClient = dataServiceApiClient;
            _workflowServiceApiClient = workflowServiceApiClient;
            _dbContext = dbContext;
            _mapper = mapper;
            _logger = logger;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<StartDbAssessmentSagaData> mapper)
        {
            mapper.ConfigureMapping<StartDbAssessmentCommand>(message => message.SourceDocumentId)
                  .ToSaga(sagaData => sagaData.SourceDocumentId);

            mapper.ConfigureMapping<RetrieveAssessmentDataCommand>(message => message.SourceDocumentId)
                .ToSaga(sagaData => sagaData.SourceDocumentId);
        }

        public async Task Handle(StartDbAssessmentCommand message, IMessageHandlerContext context)
        {
            _logger.LogInformation($"Handling {nameof(StartDbAssessmentCommand)}: {message.ToJSONSerializedString()}");

            if (!Data.IsStarted)
            {
                Data.IsStarted = true;
                Data.CorrelationId = message.CorrelationId;
                Data.SourceDocumentId = message.SourceDocumentId;

                _logger.LogInformation($"Saved {Data.ToJSONSerializedString()} " +
                          $"to {nameof(StartDbAssessmentSagaData)}");
            }

            if (Data.ProcessId == 0)
            {
                var workflowId = await _workflowServiceApiClient.GetDBAssessmentWorkflowId();
                Data.ProcessId = await _workflowServiceApiClient.CreateWorkflowInstance(workflowId);
            }

            var serialNumber = await _workflowServiceApiClient.GetWorkflowInstanceSerialNumber(Data.ProcessId);

            var workflowInstanceId = await UpdateWorkflowInstanceTable(Data.ProcessId, serialNumber, WorkflowStatus.Started);

            _logger.LogInformation($"Sending {nameof(RetrieveAssessmentDataCommand)}");
            await context.SendLocal(new RetrieveAssessmentDataCommand
            {
                CorrelationId = Data.CorrelationId,
                ProcessId = Data.ProcessId,
                SourceDocumentId = Data.SourceDocumentId,
                WorkflowInstanceId = workflowInstanceId.Value
            });

            _logger.LogInformation($"Sending {nameof(InitiateSourceDocumentRetrievalEvent)}");

            var initiateRetrievalEvent = new InitiateSourceDocumentRetrievalEvent()
            {
                CorrelationId = Data.CorrelationId,
                ProcessId = Data.ProcessId,
                SourceDocumentId = Data.SourceDocumentId,
                GeoReferenced = true,
                SourceType = SourceType.Primary
            };

            await context.Publish(initiateRetrievalEvent).ConfigureAwait(false);

            ConstructAndSendLinkedDocumentRetrievalCommands(context);

            _logger.LogInformation($"Finished handling {nameof(StartDbAssessmentCommand)}");
        }

        /// <summary>
        /// Get assessment data from SDRA and store it in the AssessmentData table and then mark Saga Complete
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Handle(RetrieveAssessmentDataCommand message, IMessageHandlerContext context)
        {
            _logger.LogInformation($"Handling {nameof(RetrieveAssessmentDataCommand)}: {message.ToJSONSerializedString()}");

            // Call DataServices to get the assessment data for the given sdoc Id
            var assessmentData =
                await _dataServiceApiClient.GetAssessmentData(_generalConfig.Value.CallerCode,
                    message.SourceDocumentId);

            // Add assessment data and any comments to DB, after auto mapping it
            var mappedAssessmentData = _mapper.Map<DocumentAssessmentData, AssessmentData>(assessmentData);
            var mappedComments = _mapper.Map<DocumentAssessmentData, Comment>(assessmentData);

            mappedAssessmentData.ProcessId = message.ProcessId;
            mappedComments.WorkflowInstanceId = message.WorkflowInstanceId;
            mappedComments.ProcessId = message.ProcessId;

            // TODO: Exception triggered due to Created and Username not being populated
            mappedComments.Created = DateTime.Now;
            mappedComments.Username = string.Empty;

            _dbContext.AssessmentData.Add(mappedAssessmentData);

            if (mappedComments.Text != null)
            {
                _dbContext.Comment.Add(mappedComments);
            }

            await _dbContext.SaveChangesAsync();

            // TODO: Fire message to create the first 'Assigned Task' in the new Tasks table and move MarkAsComplete to its handler

            MarkAsComplete();
        }

        private async Task<int?> UpdateWorkflowInstanceTable(int processId, string serialNumber, WorkflowStatus status)
        {
            var existingInstance = await _dbContext.WorkflowInstance.FirstOrDefaultAsync(w => w.ProcessId == processId);

            if (existingInstance != null) return existingInstance.WorkflowInstanceId;

            var workflowInstance = new WorkflowInstance()
            {
                ProcessId = processId,
                SerialNumber = serialNumber,
                ActivityName = WorkflowConstants.ActivityName,
                Status = status.ToString(),
                StartedAt = DateTime.Now
            };

            await _dbContext.WorkflowInstance.AddAsync(workflowInstance);
            await _dbContext.SaveChangesAsync();

            var newId = workflowInstance.WorkflowInstanceId;
            return newId;
        }

        private async void ConstructAndSendLinkedDocumentRetrievalCommands(IMessageHandlerContext context)
        {
            var backwardDocumentLinkCommand = new GetBackwardDocumentLinksCommand()
            {
                CorrelationId = Data.CorrelationId,
                ProcessId = Data.ProcessId,
                SourceDocumentId = Data.SourceDocumentId
            };

            await context.Send(backwardDocumentLinkCommand).ConfigureAwait(false);

            var forwardDocumentLinkCommand = new GetForwardDocumentLinksCommand()
            {
                CorrelationId = Data.CorrelationId,
                ProcessId = Data.ProcessId,
                SourceDocumentId = Data.SourceDocumentId
            };

            await context.Send(forwardDocumentLinkCommand).ConfigureAwait(false);

            var sepDocumentLinkCommand = new GetSepDocumentLinksCommand()
            {
                CorrelationId = Data.CorrelationId,
                ProcessId = Data.ProcessId,
                SourceDocumentId = Data.SourceDocumentId
            };

            await context.Send(sepDocumentLinkCommand).ConfigureAwait(false);
        }
    }
}
