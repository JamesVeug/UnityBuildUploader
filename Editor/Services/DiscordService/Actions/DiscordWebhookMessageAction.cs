using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Send a message to a Discord channel through an incoming webhook. Needs no bot or app.
    ///
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(DiscordWebhookMessageAction), "actions", "Send a message to a Discord channel through an incoming webhook. No bot or app is needed.")]
    [UploadAction("Discord Webhook Message")]
    public partial class DiscordWebhookMessageAction : AUploadAction
    {
        /// <summary>
        /// A dry run writes the request to the report without sending it
        /// </summary>
        public override bool SkippedDuringDryRun => false;

        [Wiki("Webhook", "Which webhook to post through. Webhooks are created in Discord under Channel Settings -> Integrations -> Webhooks.", 1)]
        private DiscordConfig.DiscordWebhook m_webhook;

        [Wiki("Username", "Overrides the webhook's name for this message. This is optional.", 2)]
        private string m_username = "";

        [Wiki("Avatar URL", "Overrides the webhook's avatar for this message. This is optional.", 3)]
        private string m_avatarURL = "";

        [Wiki("Text", "What text to send. Optional when the message has embeds.", 4)]
        private string m_text = "";

        [Wiki("Embeds", "A list of embeds to send with the message. This is optional and can be used to format the message nicely.", 5)]
        private List<DiscordMessageChannelAction.Embed> m_embeds = new List<DiscordMessageChannelAction.Embed>();

        private string m_webhookURLOverride;

        public DiscordWebhookMessageAction() : base()
        {
            // Required for reflection
        }

        public void SetWebhook(DiscordConfig.DiscordWebhook webhook)
        {
            m_webhook = webhook;
        }

        /// <summary>
        /// Sends to this URL instead of the selected webhook's saved URL. Not saved with the action.
        /// </summary>
        public void SetWebhookURL(string webhookURL)
        {
            m_webhookURLOverride = webhookURL;
        }

        public void SetUsername(string username)
        {
            m_username = username;
        }

        public void SetAvatarURL(string avatarURL)
        {
            m_avatarURL = avatarURL;
        }

        public void SetText(string text)
        {
            m_text = text;
        }

        public void AddEmbed(string title, string description, Color color)
        {
            AddEmbed(new DiscordMessageChannelAction.Embed()
            {
                title = title,
                description = description,
                color = "#" + ColorUtility.ToHtmlStringRGB(color),
            });
        }

        public void AddEmbed(DiscordMessageChannelAction.Embed embed)
        {
            m_embeds.Add(embed);
        }

        private string WebhookURL
        {
            get
            {
                if (!string.IsNullOrEmpty(m_webhookURLOverride))
                {
                    return m_webhookURLOverride;
                }

                return m_webhook != null ? m_webhook.WebhookURL : "";
            }
        }

        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            List<Dictionary<string, object>> embeds = DiscordEmbedUtils.ToPayload(m_embeds, m_context);
            string text = m_context.FormatString(m_text);
            string username = m_context.FormatString(m_username);
            string avatarURL = m_context.FormatString(m_avatarURL);
            return await Discord.SendWebhookMessage(WebhookURL, text, username, avatarURL, embeds, stepResult, m_dryRun);
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            DiscordService service = InternalUtils.GetService<DiscordService>();
            if (!m_dryRun && !service.IsReadyToStartBuild(out GUIContent reason))
            {
                errors.Add(reason);
            }

            if (m_webhook == null && string.IsNullOrEmpty(m_webhookURLOverride))
            {
                errors.Add(new GUIContent("Webhook is not set."));
            }
            else if (string.IsNullOrEmpty(WebhookURL))
            {
                errors.Add(service.PreferencesLink($"Discord Webhook {m_webhook.Name} does not have a URL set.", ""));
            }
            else if (!Discord.IsWebhookURL(WebhookURL))
            {
                errors.Add(service.PreferencesLink($"Discord Webhook {m_webhook?.Name} URL should look like https://discord.com/api/webhooks/<id>/<token>.", ""));
            }

            if (string.IsNullOrEmpty(m_text) && (m_embeds == null || m_embeds.Count == 0))
            {
                errors.Add(new GUIContent("Text or at least one embed must be set."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "webhook", m_webhook?.Id ?? 0 },
                { "username", m_username },
                { "avatarURL", m_avatarURL },
                { "text", m_text },
                { "embeds", DiscordEmbedUtils.Serialize(m_embeds) },
                { "dryRun", m_dryRun }
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_webhook = null;
            if (data.TryGetValue("webhook", out object webhookID) && webhookID != null)
            {
                DiscordConfig.DiscordWebhook[] webhooks = DiscordUIUtils.WebhookPopup.Values;
                m_webhook = webhooks.FirstOrDefault(a => a.Id == (long)webhookID);
            }

            m_username = data.TryGetValue("username", out object usernameObj) && usernameObj != null ? usernameObj.ToString() : "";
            m_avatarURL = data.TryGetValue("avatarURL", out object avatarObj) && avatarObj != null ? avatarObj.ToString() : "";
            m_text = data.TryGetValue("text", out object textObj) && textObj != null ? textObj.ToString() : "";
            m_dryRun = data.TryGetValue("dryRun", out object dryRunObj) && dryRunObj is bool dryRun && dryRun;
            m_embeds = DiscordEmbedUtils.Deserialize(data);
        }
    }
}
