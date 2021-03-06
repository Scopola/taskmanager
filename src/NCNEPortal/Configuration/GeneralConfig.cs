namespace NCNEPortal.Configuration
{
    public class GeneralConfig
    {
        public string CallerCode { get; set; }
        public string AzureAdClientId { get; set; }
        public string TenantId { get; set; }
        public int FormsDaysFromPubDate { get; set; }
        public int CisDaysFromPubDate { get; set; }
        public int Commit2WDaysFromPubDate { get; set; }
        public int Commit3WDaysFromPubDate { get; set; }
        public int PublishDaysFromRepromat { get; set; }
        public string CarisNewProjectStatus { get; set; }
        public string CarisNewProjectPriority { get; set; }
        public string CarisNcneProjectType { get; set; }
        public int CarisProjectTimeoutSeconds { get; set; }
        public int HistoricalTasksInitialNumberOfRecords { get; set; }
    }
}