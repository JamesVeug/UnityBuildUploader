using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload a build to the Nintendo Developer Center.
    ///
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Experimental]
    [Wiki("Nintendo", "destinations", "Uploads files to the Nintendo Developer Center")]
    [UploadDestination("Nintendo")]
    public partial class NintendoUploadDestination : AUploadDestination
    {
        [Wiki("Title", "Which Nintendo Title to upload to.", 1)]
        private NintendoApp m_app;

        [Wiki("Branch", "Which Branch / release ring to upload to. eg: internal", 2)]
        private NintendoBranch m_destinationBranch;

        [Wiki("Description Format", "Build description that appears in the Nintendo Developer Center.", 9)]
        private string m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;

        private NintendoApp m_uploadApp;
        private NintendoBranch m_uploadBranch;
        private string m_appPath;

        public NintendoUploadDestination() : base()
        {
            // Required for reflection
        }

        public NintendoUploadDestination(string titleId, string branchName, string applicationId = "") : base()
        {
            SetNintendoApp(titleId, applicationId);
            SetNintendoBranch(branchName);
        }

        public void SetNintendoApp(string titleId, string applicationId = "")
        {
            m_app = new NintendoApp()
            {
                TitleID = titleId,
                ApplicationID = applicationId
            };
        }

        public void SetNintendoBranch(string branchName)
        {
            m_destinationBranch = new NintendoBranch(branchName);
        }

        public override async Task<bool> Prepare(string taskGUID, int configIndex, int destinationIndex,
            string taskContentsFolder, UploadTaskReport.StepResult result)
        {
            await base.Prepare(taskGUID, configIndex, destinationIndex, taskContentsFolder, result);

            if (m_app == null)
            {
                result.SetFailed("No Title selected");
                return false;
            }

            if (m_destinationBranch == null)
            {
                result.SetFailed("No Branch selected");
                return false;
            }

            string buildDescription = m_context.FormatString(m_descriptionFormat);
            string suffix = $"buildUploader_{taskGUID}_{configIndex}_{destinationIndex}";

            result.AddLog("Creating new Nintendo authoring file");
            m_uploadApp = new NintendoApp(m_app);
            m_uploadBranch = new NintendoBranch(m_destinationBranch);

            string appFiles = await NintendoSDK.Instance.CreateAppFiles(m_uploadApp, m_uploadBranch, buildDescription,
                m_taskContentsFolder, result, suffix);
            if (string.IsNullOrEmpty(appFiles))
            {
                // NOTE: SetFailed called in CreateAppFiles
                return false;
            }
            m_appPath = appFiles;

            if (string.IsNullOrEmpty(m_appPath) || !File.Exists(m_appPath))
            {
                result.SetFailed("Failed to create authoring file or authoring file does not exist: " + m_appPath);
                return false;
            }

            return true;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            return await NintendoSDK.Instance.Upload(m_uploadApp, m_appPath, result);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            base.CleanUp(stepResult);

            m_uploadApp = null;
            m_uploadBranch = null;

            if (NintendoService.DeleteAuthoringFilesDuringCleanup)
            {
                if (!string.IsNullOrEmpty(m_appPath) && File.Exists(m_appPath))
                {
                    stepResult.AddLog("Deleting authoring file: " + m_appPath);
                    File.Delete(m_appPath);
                }
            }
            else
            {
                stepResult.AddLog("Skipping deletion of Nintendo authoring file as per preferences.");
            }
            m_appPath = null;

            return Task.CompletedTask;
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["configID"] = m_app?.Id,
                ["branchID"] = m_destinationBranch?.Id,
                ["m_descriptionFormat"] = m_descriptionFormat
            };

            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            // Title
            NintendoApp[] buildConfigs = NintendoUIUtils.ConfigPopup.Values;
            if (data.TryGetValue("configID", out object configIDString) && configIDString != null && configIDString is long configID)
            {
                m_app = buildConfigs.FirstOrDefault(a => a.Id == configID);
            }

            if (m_app == null)
            {
                return;
            }

            // Branch
            if (data.TryGetValue("branchID", out object branchIDString) && branchIDString != null)
            {
                m_destinationBranch = m_app.ConfigBranches.FirstOrDefault(a => a.Id == (long)branchIDString);
            }

            // Description Format
            if (data.TryGetValue("m_descriptionFormat", out object descriptionFormatObj) && descriptionFormatObj != null)
            {
                m_descriptionFormat = descriptionFormatObj.ToString();
            }
            else
            {
                m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;
            }
        }

        public override void TryGetWarnings(List<GUIContent> warnings, Context ctx)
        {
            base.TryGetWarnings(warnings, ctx);
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            NintendoService service = InternalUtils.GetService<NintendoService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("No Title selected"));
            }
            else
            {
                if (string.IsNullOrEmpty(m_app.TitleID))
                {
                    errors.Add(service.ProjectSettingsLink($"Nintendo App '{m_app.Name}' does not have a Title ID set.", ""));
                }

                if (string.IsNullOrEmpty(m_app.ApplicationID))
                {
                    errors.Add(service.ProjectSettingsLink($"Nintendo App '{m_app.Name}' does not have an Application ID set.", ""));
                }
            }

            if (m_destinationBranch == null)
            {
                errors.Add(new GUIContent("No Branch selected"));
            }

            if (string.IsNullOrEmpty(m_descriptionFormat))
            {
                errors.Add(new GUIContent("No build description specified."));
            }
        }
    }
}
