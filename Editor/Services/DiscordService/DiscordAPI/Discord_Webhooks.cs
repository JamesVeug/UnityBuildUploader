using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wireframe
{
    internal partial class Discord
    {
        private const string WebhooksPathSegment = "/webhooks/";

        /// <summary>
        /// Posts a message through an incoming webhook. The webhook URL holds its own token so no Authorization header is sent.
        /// https://discord.com/developers/docs/resources/webhook#execute-webhook
        /// </summary>
        public static async Task<bool> SendWebhookMessage(string webhookURL, string text, string username = null, string avatarURL = null, List<Dictionary<string, object>> embeds = null, UploadTaskReport.StepResult result = null, bool dryRun = false)
        {
            Dictionary<string, object> messageData = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(text))
            {
                messageData["content"] = text;
            }

            if (!string.IsNullOrEmpty(username))
            {
                messageData["username"] = username;
            }

            if (!string.IsNullOrEmpty(avatarURL))
            {
                messageData["avatar_url"] = avatarURL;
            }

            if (embeds != null && embeds.Count > 0)
            {
                messageData["embeds"] = embeds;
            }

            const string maskedURL = "https://discord.com/api/webhooks/xxx/xxx";
            if (dryRun)
            {
                string payload = JSON.SerializeObject(messageData);
                result?.AddLog("Discord webhook dry run: POST " + maskedURL);
                result?.AddLog(payload);
                return true;
            }

            using (RequestWrapper www = RequestWrapper.Post(webhookURL, maskedURL))
            {
                www.SetJSONData(messageData);

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to send discord webhook message");
                    return false;
                }

                result?.AddLog("Discord webhook message sent");
                return true;
            }
        }

        /// <summary>
        /// True for https://.../webhooks/{id}/{token} with both segments present.
        /// </summary>
        public static bool IsWebhookURL(string webhookURL)
        {
            if (string.IsNullOrEmpty(webhookURL) || !webhookURL.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int webhooksIndex = webhookURL.IndexOf(WebhooksPathSegment, System.StringComparison.OrdinalIgnoreCase);
            if (webhooksIndex < 0)
            {
                return false;
            }

            int idStart = webhooksIndex + WebhooksPathSegment.Length;
            int tokenStart = webhookURL.IndexOf('/', idStart);
            return tokenStart > idStart && tokenStart + 1 < webhookURL.Length && webhookURL[tokenStart + 1] != '?';
        }
    }
}
