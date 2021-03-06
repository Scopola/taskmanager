﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Common.Helpers;
using Common.Helpers.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Auth;
using Portal.BusinessLogic;
using Portal.Configuration;
using Portal.Helpers;
using Portal.Models;
using Portal.ViewModels;
using Serilog.Context;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.Pages.DbAssessment
{
    [TypeFilter(typeof(JavascriptError))]
    [Authorize]
    public class _EditDatabaseModel : PageModel
    {
        private readonly WorkflowDbContext _dbContext;
        private readonly ILogger<_EditDatabaseModel> _logger;
        private readonly IOptions<GeneralConfig> _generalConfig;
        private readonly IWorkflowBusinessLogicService _workflowBusinessLogicService;
        private IAdDirectoryService _adDirectoryService;
        private readonly ISessionFileGenerator _sessionFileGenerator;
        private readonly ICarisProjectHelper _carisProjectHelper;
        private readonly ICarisProjectNameGenerator _carisProjectNameGenerator;
        private readonly IPortalUserDbService _portalUserDbService;

        [BindProperty(SupportsGet = true)]
        [DisplayName("Select CARIS Workspace:")]
        public string SelectedCarisWorkspace { get; set; }

        [BindProperty(SupportsGet = true)]
        [DisplayName("CARIS Project Name:")]
        public string ProjectName { get; set; }

        public string SessionFilename { get; set; }

        public CarisProjectDetails CarisProjectDetails { get; set; }

        public bool IsCarisProjectCreated { get; set; }

        public int CarisProjectNameCharacterLimit { get; set; }

        public int UsagesSelectionPageLength { get; set; }

        public int SourcesSelectionPageLength { get; set; }

        public List<string> HpdUsages { get; set; }

        public List<SourceViewModel> SourceDocuments { get; set; } = new List<SourceViewModel>();

        private (string DisplayName, string UserPrincipalName) _currentUser;
        public (string DisplayName, string UserPrincipalName) CurrentUser
        {
            get
            {
                if (_currentUser == default) _currentUser = _adDirectoryService.GetUserDetails(this.User);
                return _currentUser;
            }
        }

        public _EditDatabaseModel(WorkflowDbContext dbContext, ILogger<_EditDatabaseModel> logger,
            IOptions<GeneralConfig> generalConfig,
            IWorkflowBusinessLogicService workflowBusinessLogicService,
            IAdDirectoryService adDirectoryService,
            ISessionFileGenerator sessionFileGenerator,
            ICarisProjectHelper carisProjectHelper,
            ICarisProjectNameGenerator carisProjectNameGenerator,
            IPortalUserDbService portalUserDbService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _generalConfig = generalConfig;
            _workflowBusinessLogicService = workflowBusinessLogicService;
            _adDirectoryService = adDirectoryService;
            _sessionFileGenerator = sessionFileGenerator;
            _carisProjectHelper = carisProjectHelper;
            _carisProjectNameGenerator = carisProjectNameGenerator;
            _portalUserDbService = portalUserDbService;
        }

        public async Task OnGetAsync(int processId, string taskStage)
        {
            LogContext.PushProperty("ActivityName", taskStage);
            LogContext.PushProperty("ProcessId", processId);
            LogContext.PushProperty("PortalResource", nameof(OnGetAsync));

            _logger.LogInformation("Entering {PortalResource} for _EditDatabase with: ProcessId: {ProcessId}; ActivityName: {ActivityName};");

            HpdUsages = await _dbContext.HpdUsage.OrderBy(u => u.SortIndex)
                .Select(u => u.Name).ToListAsync();

            await GetSourceDocuments(processId);

            await GetCarisData(processId, taskStage);

            await GetCarisProjectDetails(processId, taskStage);

            UsagesSelectionPageLength = _generalConfig.Value.UsagesSelectionPageLength;
            SourcesSelectionPageLength = _generalConfig.Value.SourcesSelectionPageLength;
            CarisProjectNameCharacterLimit = _generalConfig.Value.CarisProjectNameCharacterLimit;

            SessionFilename = GenerateSessionFilename();
        }

        public async Task<JsonResult> OnGetWorkspacesAsync()
        {
            var cachedHpdWorkspaces = await _dbContext.CachedHpdWorkspace.Select(c => c.Name).ToListAsync();
            return new JsonResult(cachedHpdWorkspaces);
        }

        public async Task<IActionResult> OnGetLaunchSourceEditorAsync(int processId,
            string workspaceAffected,
            string sessionFilename,
            List<string> selectedHpdUsages, List<string> selectedSources)
        {
            LogContext.PushProperty("ProcessId", processId);
            LogContext.PushProperty("WorkspaceAffected", workspaceAffected);
            LogContext.PushProperty("PortalResource", nameof(OnGetLaunchSourceEditorAsync));
            LogContext.PushProperty("SelectedHpdUsages", (selectedHpdUsages != null && selectedHpdUsages.Count > 0 ? string.Join(',', selectedHpdUsages) : ""));
            LogContext.PushProperty("SelectedSources", (selectedSources != null && selectedSources.Count > 0 ? string.Join(',', selectedSources) : ""));
            LogContext.PushProperty("UserPrincipalName", CurrentUser.UserPrincipalName);

            _logger.LogInformation("Entering {PortalResource} for _EditDatabase with: ProcessId: {ProcessId}; " +
                                   "WorkspaceAffected: {WorkspaceAffected}; " +
                                   "ActivityName: {ActivityName}; " +
                                   "with SelectedHpdUsages {SelectedHpdUsages}, " +
                                   "and SelectedSources {SelectedSources}");

            if (selectedHpdUsages == null || selectedHpdUsages.Count == 0)
            {
                _logger.LogError("Failed to generate session file. No Hpd Usages were selected. " +
                                 "ProcessId: {ProcessId}; " +
                                 "ActivityName: {ActivityName};");
                throw new ArgumentException("Failed to generate session file. No Hpd Usages were selected.");
            }

            var carisProjectDetails = await _dbContext.CarisProjectDetails.FirstOrDefaultAsync(cp => cp.ProcessId == processId);

            var isCarisProjectCreated = carisProjectDetails != null;

            if (!isCarisProjectCreated)
            {
                _logger.LogError("Failed to generate session file. Caris project was never created. " +
                                 "ProcessId: {ProcessId}; " +
                                 "ActivityName: {ActivityName};");
                throw new ArgumentException("Failed to generate session file. Caris project was never created.");
            }

            var sessionFile = await _sessionFileGenerator.PopulateSessionFile(
                                                                                processId,
                                                                                CurrentUser.UserPrincipalName,
                                                                                workspaceAffected,
                                                                                carisProjectDetails,
                                                                                selectedHpdUsages,
                                                                                selectedSources);

            var serializer = new XmlSerializer(typeof(SessionFile));

            var fs = new MemoryStream();
            try
            {

                var xmlnsEmpty = new XmlSerializerNamespaces();
                xmlnsEmpty.Add("", "");
                serializer.Serialize(fs, sessionFile, xmlnsEmpty);

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

        public async Task<IActionResult> OnPostCreateCarisProjectAsync(int processId, string taskStage, string projectName,
            string carisWorkspace)
        {
            LogContext.PushProperty("ActivityName", taskStage);
            LogContext.PushProperty("ProcessId", processId);
            LogContext.PushProperty("PortalResource", nameof(OnPostCreateCarisProjectAsync));
            LogContext.PushProperty("ProjectName", projectName);
            LogContext.PushProperty("CarisWorkspace", carisWorkspace);
            LogContext.PushProperty("UserPrincipalName", CurrentUser.UserPrincipalName);

            _logger.LogInformation("Entering {PortalResource} for _EditDatabase with: ProcessId: {ProcessId}; ActivityName: {ActivityName};");

            var isWorkflowReadOnly = await _workflowBusinessLogicService.WorkflowIsReadOnlyAsync(processId);

            if (isWorkflowReadOnly)
            {
                var appException = new ApplicationException($"Workflow Instance for {nameof(processId)} {processId} has already been completed");
                _logger.LogError(appException,
                    "Workflow Instance for ProcessId {ProcessId} has already been completed");
                throw appException;
            }

            await ValidateCarisProjectDetails(processId, projectName, carisWorkspace, taskStage, CurrentUser.UserPrincipalName);

            var projectId = await CreateCarisProject(processId, projectName);

            await UpdateCarisProjectDetails(processId, projectName, projectId);

            // Add assessor and verifier to created project
            await UpdateCarisProjectWithAdditionalUser(projectId, processId, taskStage);

            var assessData = await _dbContext.DbAssessmentAssessData.FirstAsync(ad => ad.ProcessId == processId);
            assessData.WorkspaceAffected = carisWorkspace;
            await _dbContext.SaveChangesAsync();

            return StatusCode(200);
        }

        private async Task GetSourceDocuments(int processId)
        {
            var workflowInstance = await _dbContext.WorkflowInstance
                                                            .Include(wi => wi.PrimaryDocumentStatus)
                                                            .Include(wi => wi.AssessmentData)
                                                            .FirstOrDefaultAsync(wi => wi.ProcessId == processId
                                                                                                        && wi.AssessmentData.SourceNature == "Graphical");

            var primaryDocumentStatus = workflowInstance?.PrimaryDocumentStatus;

            if (primaryDocumentStatus != null &&
                primaryDocumentStatus.Status == SourceDocumentRetrievalStatus.FileGenerated.ToString())
            {
                var primarySourceDocument = new SourceViewModel()
                {
                    DocumentName = primaryDocumentStatus.Filename,
                    DocumentFullName = Path.Combine(primaryDocumentStatus.Filepath, primaryDocumentStatus.Filename)
                };

                SourceDocuments.Add(primarySourceDocument);
            }

            var linkedDocuments = await _dbContext.LinkedDocument
                                                                        .Where(ld => ld.ProcessId == processId
                                                                                    && ld.SourceNature == "Graphical"
                                                                                    && ld.Status == SourceDocumentRetrievalStatus.FileGenerated.ToString())
                                                                        .Select(ld => new SourceViewModel()
                                                                        {
                                                                            DocumentName = ld.Filename,
                                                                            DocumentFullName = Path.Combine(ld.Filepath, ld.Filename)
                                                                        })
                                                                        .ToListAsync();

            SourceDocuments.AddRange(linkedDocuments);

            var databaseDocuments = await _dbContext.DatabaseDocumentStatus
                                                                        .Where(dd => dd.ProcessId == processId
                                                                                            && dd.SourceNature == "Graphical"
                                                                                            && dd.Status == SourceDocumentRetrievalStatus.FileGenerated.ToString())
                                                                        .Select(dd => new SourceViewModel()
                                                                        {
                                                                            DocumentName = dd.Filename,
                                                                            DocumentFullName = Path.Combine(dd.Filepath, dd.Filename)
                                                                        })
                                                                        .ToListAsync();

            SourceDocuments.AddRange(databaseDocuments);
        }

        private async Task<int> CreateCarisProject(int processId, string projectName)
        {

            var carisProjectDetails = await _dbContext.CarisProjectDetails.FirstOrDefaultAsync(cp => cp.ProcessId == processId);

            if (carisProjectDetails != null)
            {
                return carisProjectDetails.ProjectId;
            }

            // which will also implicitly validate if the current user has been mapped to HPD account in our database
            var hpdUser = await GetHpdUser(CurrentUser.UserPrincipalName);

            _logger.LogInformation(
                "Creating Caris Project with ProcessId: {ProcessId}; ProjectName: {ProjectName}.");

            var projectId = await _carisProjectHelper.CreateCarisProject(processId, projectName,
                hpdUser.HpdUsername, _generalConfig.Value.CarisNewProjectType,
                _generalConfig.Value.CarisNewProjectStatus,
                _generalConfig.Value.CarisNewProjectPriority, _generalConfig.Value.CarisProjectTimeoutSeconds);

            return projectId;
        }

        private async Task UpdateCarisProjectDetails(int processId, string projectName, int projectId)
        {
            // If somehow the user has already created a project, remove it and create new row
            var toRemove = await _dbContext.CarisProjectDetails.Where(cp => cp.ProcessId == processId).ToListAsync();
            if (toRemove.Any())
            {
                _dbContext.CarisProjectDetails.RemoveRange(toRemove);
                await _dbContext.SaveChangesAsync();
            }

            _dbContext.CarisProjectDetails.Add(new CarisProjectDetails
            {
                ProcessId = processId,
                Created = DateTime.Now,
                CreatedBy = await _portalUserDbService.GetAdUserAsync(CurrentUser.UserPrincipalName),
                ProjectId = projectId,
                ProjectName = projectName
            });

            await _dbContext.SaveChangesAsync();
        }

        private async Task UpdateCarisProjectWithAdditionalUser(int projectId, int processId, string taskStage)
        {
            var additionalUsername = await GetAdditionalUserToAssignedToCarisproject(processId, taskStage);
            if (additionalUsername != null)
            {
                var hpdUsername =
                    await GetHpdUser(additionalUsername
                        .UserPrincipalName); // which will also implicitly validate if the other user has been mapped to HPD account in our database

                try
                {
                    await _carisProjectHelper.UpdateCarisProject(projectId, hpdUsername.HpdUsername,
                        _generalConfig.Value.CarisProjectTimeoutSeconds);
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        $"Project created but failed to assign {additionalUsername} ({hpdUsername.HpdUsername}) to Caris project: {projectId}");
                    throw new InvalidOperationException(
                        $"Project created but failed to assign {additionalUsername} ({hpdUsername.HpdUsername}) to Caris project: {projectId}. {e.Message}");
                }
            }
        }

        private async Task ValidateCarisProjectDetails(int processId, string projectName, string carisWorkspace, string taskStage, string currentLoggedInUserEmail)
        {
            var userAssignedToTask = await GetUserAssignedToTask(processId, taskStage);

            if (userAssignedToTask.UserPrincipalName != currentLoggedInUserEmail)
            {
                LogContext.PushProperty("UserAssignedToTask", userAssignedToTask.UserPrincipalName);
                _logger.LogError("{UserPrincipalName} is not assigned to this task with processId {ProcessId}, {UserAssignedToTask} is assigned to this task.");
                throw new InvalidOperationException($"{userAssignedToTask.DisplayName} is assigned to this task. Please assign the task to yourself and click Save");
            }

            if (!await _dbContext.CachedHpdWorkspace.AnyAsync(c =>
                c.Name == carisWorkspace))
            {
                _logger.LogError($"Current Caris Workspace {carisWorkspace} is invalid.");
                throw new InvalidOperationException($"Current Caris Workspace {carisWorkspace} is invalid.");
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("Please provide a Caris Project Name.");
            }
        }

        private async Task<AdUser> GetUserAssignedToTask(int processId, string taskStage)
        {
            switch (taskStage)
            {
                case "Assess":
                    var assessData = await _dbContext.DbAssessmentAssessData.FirstAsync(ad => ad.ProcessId == processId);
                    return assessData.Assessor;
                case "Verify":
                    var verifyData = await _dbContext.DbAssessmentVerifyData.FirstAsync(vd => vd.ProcessId == processId);
                    return verifyData.Verifier;
                default:
                    _logger.LogError("{ActivityName} is not implemented for processId: {ProcessId}.");
                    throw new NotImplementedException($"{taskStage} is not implemented for processId: {processId}.");
            }
        }

        private async Task<AdUser> GetAdditionalUserToAssignedToCarisproject(int processId, string taskStage)
        {
            switch (taskStage)
            {
                case "Assess":
                    // at Assess stage; Assessor will be current user; so only use Verifier as the additional 'assigned to'

                    var assessData = await _dbContext.DbAssessmentAssessData.FirstAsync(ad => ad.ProcessId == processId);
                    return assessData.Verifier;
                case "Verify":
                    // at Verify stage; Verifier will be current user; so only use Assessor as the additional 'assigned to'

                    var verifyData = await _dbContext.DbAssessmentVerifyData.FirstAsync(vd => vd.ProcessId == processId);
                    return verifyData.Assessor;
                default:
                    _logger.LogError("{ActivityName} is not implemented for processId: {ProcessId}.");
                    throw new NotImplementedException($"{taskStage} is not implemented for processId: {processId}.");
            }
        }

        private async Task<HpdUser> GetHpdUser(string userPrincipalName)
        {
            try
            {
                return await _dbContext.HpdUser.SingleAsync(u => u.AdUser.UserPrincipalName == userPrincipalName);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError("Unable to find HPD Username for {UserPrincipalName} in our system.");
                throw new InvalidOperationException($"Unable to find HPD username for {userPrincipalName} in our system.",
                    ex.InnerException);
            }

        }

        private async Task GetCarisData(int processId, string taskStage)
        {
            switch (taskStage)
            {
                case "Assess":
                    var assessData = await _dbContext.DbAssessmentAssessData.FirstAsync(ad => ad.ProcessId == processId);
                    SelectedCarisWorkspace = assessData.WorkspaceAffected;
                    break;
                case "Verify":
                    var verifyData = await _dbContext.DbAssessmentVerifyData.FirstAsync(vd => vd.ProcessId == processId);
                    SelectedCarisWorkspace = verifyData.WorkspaceAffected;
                    break;
                default:
                    _logger.LogError("{ActivityName} is not implemented for processId: {ProcessId}.");
                    throw new NotImplementedException($"{taskStage} is not implemented for processId: {processId}.");
            }
        }

        private async Task GetCarisProjectDetails(int processId, string taskStage)
        {
            CarisProjectDetails = await _dbContext.CarisProjectDetails.FirstOrDefaultAsync(cp => cp.ProcessId == processId);

            IsCarisProjectCreated = CarisProjectDetails != null;

            if (!IsCarisProjectCreated)
            {
                if (taskStage == "Assess")
                {
                    var assessmentData =
                        await _dbContext.AssessmentData.SingleAsync(ad => ad.ProcessId == processId);
                    var parsedRsdraNumber = assessmentData.ParsedRsdraNumber;
                    var sourceDocumentName = assessmentData.SourceDocumentName;

                    ProjectName = _carisProjectNameGenerator.Generate(processId, parsedRsdraNumber, sourceDocumentName);
                    return;
                }

                ProjectName = "NO PROJECT WAS CREATED AT ASSESS";
                return;
            }

            ProjectName = CarisProjectDetails.ProjectName;
        }

        private string GenerateSessionFilename()
        {
            var filename = Path.GetFileNameWithoutExtension(_generalConfig.Value.SessionFilename);
            var ext = Path.GetExtension(_generalConfig.Value.SessionFilename);
            return $"{filename}{ext}";
        }
    }
}
