﻿namespace DataServices.Config
{
    public class StartupSecretsConfig
    {
        public string DataSource { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string SqlLoggingUsername { get; set; }
        public string SqlLoggingPassword { get; set; }
    }
}
