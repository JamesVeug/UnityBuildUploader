using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Post a build notification to the Nintendo team's internal webhook for a given Title.
    ///
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Experimental]
    [Wiki(nameof(NintendoNotifyAction), "actions", "Sends a notification about a Nintendo build upload to the team's internal webhook.")]
    [UploadAction("Nintendo Notify")]
    public partial class NintendoNotifyAction : AUploadAction
    {
        [Wiki("Title", "Which Nintendo Title the notification is about.", 1)]
        private NintendoApp m_app;

        [Wiki("Branch", "Optional: Which release Branch the notification is about.", 2)]
        private NintendoBranch m_branch;

        [Wiki("Text", "Message text to include in the notification.", 3)]
        private string m_text = "";

        [Wiki("Description Format", "Build description that is included alongside the notification payload.", 4)]
        private string m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;

        [Wiki("ResponseFormatName", "If provided, the message ID returned by the notification webhook is exposed as this format key for later actions.", 5)]
        private Command m_responseIdFormat; // Created in CreateContext()

        public NintendoNotifyAction() : base()
        {
            // Required for reflection
        }

        public void SetApp(NintendoApp app)
        {
            m_app = app;
        }

        public void SetBranch(NintendoBranch branch)
        {
            m_branch = branch;
        }

        public void SetText(string text)
        {
            m_text = text;
        }

        public void SetResponseIdFormatName(string formatName)
        {
            if (formatName.Length > 0)
            {
                if (formatName[0] != '{')
                {
                    formatName = '{' + formatName;
                }
                if (formatName[formatName.Length - 1] != '}')
                {
                    formatName += '}';
                }
            }

            m_responseIdFormat.Key = formatName;
        }

        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            string text = m_context.FormatString(m_text);
            string description = m_context.FormatString(m_descriptionFormat);
            string branchName = m_branch != null ? m_branch.name : "";

            NintendoSendMessageResponse response = await Nintendo.SendMessage(
                text,
                m_app.TitleID,
                m_app.Name,
                branchName,
                description,
                NintendoSDK.NotificationWebhook,
                NintendoSDK.NotificationToken,
                stepResult);

            m_recordedResponseId = response.MessageId;
            return response.Successful;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);


            NintendoService service = InternalUtils.GetService<NintendoService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (string.IsNullOrEmpty(NintendoSDK.NotificationWebhook))
            {
                errors.Add(service.PreferencesLink("Nintendo notification webhook URL is not set.", ""));
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Nintendo App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.TitleID))
            {
                errors.Add(service.ProjectSettingsLink($"Nintendo App '{m_app.Name}' does not have a Title ID set.", ""));
            }

            if (string.IsNullOrEmpty(m_text))
            {
                errors.Add(new GUIContent("Text is not set."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "configID", m_app?.Id ?? 0 },
                { "branchID", m_branch?.Id ?? 0 },
                { "text", m_text },
                { "m_descriptionFormat", m_descriptionFormat },
                { "idFormat", m_responseIdFormat.Key }
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            // Title
            NintendoApp[] buildConfigs = NintendoUIUtils.ConfigPopup.Values;
            if (data.TryGetValue("configID", out object configIDObj) && configIDObj != null && configIDObj is long configID)
            {
                m_app = buildConfigs.FirstOrDefault(a => a.Id == configID);
            }

            // Branch (optional - only resolves once we know the Title)
            if (m_app != null && data.TryGetValue("branchID", out object branchIDObj) && branchIDObj != null && branchIDObj is long branchID && branchID > 0)
            {
                m_branch = m_app.ConfigBranches.FirstOrDefault(a => a.Id == branchID);
            }

            // Text
            if (data.TryGetValue("text", out object textObj) && textObj != null)
            {
                m_text = textObj.ToString();
            }
            else
            {
                m_text = string.Empty;
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

            // Response ID Format
            if (data.TryGetValue("idFormat", out object idFormatObj) && idFormatObj is string idFormatString)
            {
                m_responseIdFormat.Key = idFormatString;
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            return new Dictionary<string, object>
            {
                ["recordedResponseId"] = m_recordedResponseId,
            };
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            m_recordedResponseId = state["recordedResponseId"] as string;
        }
    }
}
