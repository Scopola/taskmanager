﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Auth;
using Portal.Configuration;
using Portal.Helpers;
using Portal.Models;
using Serilog.Context;
using WorkflowDatabase.EF;

namespace Portal.Pages.DbAssessment
{
    [TypeFilter(typeof(JavascriptError))]
    public class _EditDatabaseModel : PageModel
    {
        private readonly WorkflowDbContext _dbContext;
        private readonly ILogger<_EditDatabaseModel> _logger;
        private readonly IOptions<GeneralConfig> _generalConfig;
        private IUserIdentityService _userIdentityService;
        private readonly ISessionFileGenerator _sessionFileGenerator;

        [BindProperty(SupportsGet = true)]
        [DisplayName("Select CARIS Workspace:")]
        public string SelectedCarisWorkspace { get; set; }

        [BindProperty(SupportsGet = true)]
        [DisplayName("CARIS Project Name:")]
        public string ProjectName { get; set; }

        public string SessionFilename { get; set; } 

        private string _userFullName;

        public string UserFullName
        {
            get => string.IsNullOrEmpty(_userFullName) ? "Unknown user" : _userFullName;
            private set => _userFullName = value;
        }

        public WorkflowDbContext DbContext
        {
            get { return _dbContext; }
        }

        public _EditDatabaseModel(WorkflowDbContext dbContext, ILogger<_EditDatabaseModel> logger,
            IOptions<GeneralConfig> generalConfig,
            IUserIdentityService userIdentityService,
            ISessionFileGenerator sessionFileGenerator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _generalConfig = generalConfig;
            _userIdentityService = userIdentityService;
            _sessionFileGenerator = sessionFileGenerator;
        }

        public async Task OnGetAsync(int processId, string taskStage)
        {
            LogContext.PushProperty("ActivityName", taskStage);
            LogContext.PushProperty("ProcessId", processId);
            LogContext.PushProperty("PortalResource", nameof(OnGetAsync));

            _logger.LogInformation("Entering {PortalResource} for _EditDatabase with: ProcessId: {ProcessId}; ActivityName: {ActivityName};");

            await GetCarisData(processId, taskStage);

            SessionFilename = _generalConfig.Value.SessionFilename;
        }

        public async Task<JsonResult> OnGetWorkspacesAsync()
        {
            var cachedHpdWorkspaces = await _dbContext.CachedHpdWorkspace.Select(c => c.Name).ToListAsync();
            return new JsonResult(cachedHpdWorkspaces);
        }

        public async Task<IActionResult> OnGetLaunchSourceEditorAsync(int processId, string taskStage, string sessionFilename)
        {
            LogContext.PushProperty("ActivityName", taskStage);
            LogContext.PushProperty("ProcessId", processId);
            LogContext.PushProperty("PortalResource", nameof(OnGetLaunchSourceEditorAsync));

            _logger.LogInformation("Launching Source Editor with: ProcessId: {ProcessId}; ActivityName: {ActivityName};");

            UserFullName = await _userIdentityService.GetFullNameForUser(this.User);
            var sessionFile = await _sessionFileGenerator.PopulateSessionFile(processId, UserFullName, taskStage);

            var serializer = new XmlSerializer(typeof(SessionFile));

            var fs = new MemoryStream();
            try
            {
                serializer.Serialize(fs, sessionFile);

                fs.Position = 0;

                return File(fs, MediaTypeNames.Application.Octet, sessionFilename);
            }
            catch (InvalidOperationException ex)
            {

                fs.Dispose();
                _logger.LogError(ex, "Failed to serialize Caris session file.");
                throw;
            }
            catch (Exception ex)
            {
                fs.Dispose();
                _logger.LogError(ex, "Failed to generate session file.");
                throw;
            }
        }

        private async Task GetCarisData(int processId, string taskStage)
        {
            switch (taskStage)
            {
                case "Assess":
                    var assessData = await _dbContext.DbAssessmentAssessData.FirstAsync(ad => ad.ProcessId == processId);
                    SelectedCarisWorkspace = assessData.WorkspaceAffected;
                    ProjectName = assessData.CarisProjectName;
                    break;
                case "Verify":
                    var verifyData = await _dbContext.DbAssessmentVerifyData.FirstAsync(vd => vd.ProcessId == processId);
                    SelectedCarisWorkspace = verifyData.WorkspaceAffected;
                    ProjectName = verifyData.CarisProjectName;
                    break;
                default:
                    _logger.LogError("{ActivityName} is not implemented for processId: {ProcessId}.");
                    throw new NotImplementedException($"{taskStage} is not implemented for processId: {processId}.");
            }
        }

    }
}