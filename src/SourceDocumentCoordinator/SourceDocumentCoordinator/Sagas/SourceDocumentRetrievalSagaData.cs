﻿using System;
using Common.Messages;
using NServiceBus;

namespace SourceDocumentCoordinator.Sagas
{
    public class SourceDocumentRetrievalSagaData : ContainSagaData, ICorrelate
    {
        public bool IsStarted { get; set; }  
        public Guid CorrelationId { get; set; }
        public int SourceDocumentId { get; set; }
        public int SourceDocumentStatusId { get; set; } 
    }
}