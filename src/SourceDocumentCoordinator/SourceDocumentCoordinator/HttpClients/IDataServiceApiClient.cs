﻿using System.Threading.Tasks;
using DataServices.Models;

namespace SourceDocumentCoordinator.HttpClients
{
    public interface IDataServiceApiClient
    {
        Task<ReturnCode> GetDocumentForViewing(string callerCode, int sdocId, string writableFolderName, bool imageAsGeotiff);
        Task<bool> CheckDataServicesConnection();
        Task<QueuedDocumentObjects> GetDocumentRequestQueueStatus(string callerCode);
        Task<ReturnCode> DeleteDocumentRequestJobFromQueue(string callerCode, int sdocId, string writeableFolderName);
    }
}