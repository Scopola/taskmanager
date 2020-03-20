﻿using System.ComponentModel;

namespace WorkflowDatabase.EF.Models
{
    public class DataImpact
    {
        public int DataImpactId { get; set; }
        public int ProcessId { get; set; }
        public int HpdUsageId { get; set; }
        public bool Edited { get; set; }
        public string Comments { get; set; }

        [DisplayName("Features Submitted")]
        public bool FeaturesSubmitted { get; set; }

        [DisplayName("Features Verified")]
        public bool FeaturesVerified { get; set; }

        public virtual HpdUsage HpdUsage { get; set; }
    }
}
