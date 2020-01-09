﻿namespace NCNEPortal.Configuration
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
    }
}
