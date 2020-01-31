using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCNEWorkflowDatabase.EF.Models
{
    public class TaskInfo
    {
        [Key]
        [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
        public int ProcessId { get; set; }

        public string Ion { get; set; }
        public int ChartNumber { get; set; }
        public string Country { get; set; }
        public string ChartType { get; set; }
        public string ChartTitle { get; set; }
        public string WorkflowType { get; set; }
        public string Duration { get; set; }
        public DateTime PublicationDate { get; set; }
        public DateTime AnnounceDate { get; set; }
        public DateTime CommitDate { get; set; }
        public DateTime CisDate { get; set; }
        public bool Sdr { get; set; } = false;
        public bool Geodesy { get; set; } = false;
        public bool ThreePs { get; set; } = false;
        public DateTime? SentDate3Ps { get; set; }
        public DateTime? ExpectedDate3Ps { get; set; }
        public DateTime? ActualDate3Ps { get; set; }
        public string AssignedUser { get; set; }
        public DateTime AssignedDate { get; set; }

        public virtual TaskNote TaskNote { get; set; }

        public virtual TaskRole TaskRole { get; set; }

        public virtual List<TaskComment> TaskComment { get; set; }

        public virtual List<TaskStage> TaskStage { get; set; }
    }
}
