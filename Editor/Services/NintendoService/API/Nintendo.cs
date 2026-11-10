using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wireframe
{
    public static partial class Nintendo
    {
        /// <summary>
        /// Posts a build notification to a configurable team-notification webhook URL.
        /// The URL is expected to accept a JSON payload describing the upload (Title, Branch, Description, Message).
        ///
        /// Nintendo's official Developer Center APIs are NDA-gated; this endpoint is intended for an internal
        /// team channel (e.g. an internal relay service) configured per Title.
        /// </summary>
        public static async Task<NintendoSendMessageResponse> SendMessage(string text, string titleId,
            string titleName, string branch, string description, string webhookUrl, string token,
            UploadTaskReport.StepResult result = null)
        {
            Dictionary<string, object> messageData = new Dictionary<string, object>
            {
                { "title_id", titleId },
                { "title_name", titleName },
                { "branch", branch },
                { "description", description },
                { "text", text },
            };

            using (RequestWrapper www = RequestWrapper.Post(webhookUrl, RequestWrapper.MaskPathAndQuery(webhookUrl)))
            {
                www.SetJSONData(messageData);
                if (!string.IsNullOrEmpty(token))
                {
                    www.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to send Nintendo notification");
                    return new NintendoSendMessageResponse(false);
                }

                string handlerText = response.Data;
                result?.AddLog(handlerText);

                string messageId = "";
                Dictionary<string, string> responseDict = JSON.DeserializeObject<Dictionary<string, string>>(handlerText);
                if (responseDict != null)
                {
                    responseDict.TryGetValue("id", out messageId);
                    if (responseDict.TryGetValue("ok", out string ok) && ok == "false")
                    {
                        string error = responseDict.TryGetValue("error", out string e) ? e : "unknown_error";
                        result?.SetFailed($"Failed to send Nintendo notification: {error}");
                        return new NintendoSendMessageResponse(false);
                    }
                }

                result?.AddLog("Notification sent successfully");
                return new NintendoSendMessageResponse(true, messageId);
            }
        }
    }
}
