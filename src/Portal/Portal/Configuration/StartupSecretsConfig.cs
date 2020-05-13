﻿namespace Portal.Configuration
{
    public class StartupSecretsConfig
    {
        public string DataSource { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string K2RestApiUsername { get; set; }
        public string K2RestApiPassword { get; set; }
        public string SqlLoggingUsername { get; set; }
        public string SqlLoggingPassword { get; set; }
        public string PCPEventServiceUsername { get; set; }
        public string PCPEventServicePassword { get; set; }
        public string ClientAzureAdSecret { get; set; }

        // Expected to come from KV as comma separated
        public string AdUserGroups { get; set; }
    }
}
