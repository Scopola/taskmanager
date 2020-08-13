﻿using System;

namespace Portal.Configuration
{
    public class StartupConfig
    {
        public string WorkflowDbName { get; set; }
        public string WorkflowDbServer { get; set; }
        public string LocalDbServer { get; set; }
        public string TenantId { get; set; }
        public string AzureAdClientId { get; set; }
        public Uri LocalDevLandingPageHttpsUrl { get; set; }
        public Uri LandingPageUrl { get; set; }
    }
}
