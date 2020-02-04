﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCNEWorkflowDatabase.EF.Models
{
    public class TaskStageComment
    {
        [Key]
        [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
        public int TaskStageCommentId { get; set; }

        public int TaskStageId { get; set; }
        public int ProcessId { get; set; }
        public string Comment { get; set; }
        public string Username { get; set; }
        public DateTime Created { get; set; }

    }
}