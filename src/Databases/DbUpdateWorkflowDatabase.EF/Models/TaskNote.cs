﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DbUpdateWorkflowDatabase.EF.Models
{
    public class TaskNote
    {
        [Key]
        [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
        public int TaskNoteId { get; set; }
        public int ProcessId { get; set; }
        public string Text { get; set; }
        public DateTime Created { get; set; }
        public string CreatedByUsername { get; set; }
        public DateTime LastModified { get; set; }
        public string LastModifiedByUsername { get; set; }
    }
}
