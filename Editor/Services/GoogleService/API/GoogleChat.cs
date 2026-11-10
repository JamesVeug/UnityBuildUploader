using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Google Chat REST API wrapper.
    ///
    /// Messages are sent to an incoming webhook URL configured per Chat space.
    /// The webhook URL embeds the auth key and target space, so no additional
    /// Authorization header is required.
    ///
    /// https://developers.google.com/workspace/chat/quickstart/webhooks
    /// </summary>
    internal static partial class GoogleChat
    {
        /// <summary>
        /// Post a plain-text message to a Google Chat space via its incoming webhook.
        /// </summary>
        /// <param name="text">Message text (Google Chat-flavoured Markdown is supported).</param>
        /// <param name="webhookUrl">Full incoming webhook URL including the key= query parameter.</param>
        /// <param name="result">StepResult for logging.</param>
        public static async Task<GoogleChatSendMessageResponse> SendMessage(
            string text,
            string webhookUrl,
            UploadTaskReport.StepResult result = null)
        {
            Dictionary<string, object> messageData = new Dictionary<string, object>
            {
                { "text", text }
            };

            using (RequestWrapper www = RequestWrapper.Post(webhookUrl, "https://chat.googleapis.com/v1/spaces/xxx/messages?key=xxx&token=xxx"))
            {
                www.SetJSONData(messageData);

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to send Google Chat message");
                    return new GoogleChatSendMessageResponse(false);
                }

                result?.AddLog(response.Data);

                // {
                //   "name": "spaces/AAAA.../messages/BBBB.CCCC",
                //   "sender": { ... },
                //   "createTime": "2025-...",
                //   "text": "...",
                //   ...
                // }
                string messageName = "";
                Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                if (responseDict != null && responseDict.TryGetValue("name", out object nameObj) && nameObj != null)
                {
                    messageName = nameObj.ToString();
                }

                result?.AddLog("Message Successful");
                return new GoogleChatSendMessageResponse(true, messageName);
            }
        }
    }
}
