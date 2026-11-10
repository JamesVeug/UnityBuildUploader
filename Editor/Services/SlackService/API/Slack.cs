using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;

// TODO: Move requests to a wrapper
#pragma warning disable CS0618 // Type or member is obsolete

namespace Wireframe
{
    public static partial class Slack
    {
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("slack_enabled", false);
            set => ProjectEditorPrefs.SetBool("slack_enabled", value);
        }

        /// <summary>
        /// https://api.slack.com/apps/
        /// https://docs.slack.dev/apis/web-api/
        /// https://docs.slack.dev/app-management/quickstart-app-settings/
        /// https://docs.slack.dev/reference/methods/chat.postmessage
        /// </summary>
        public static async Task<SlackSendMessageResponse> SendMessage(string text, string channel, string token, List<SlackAttachment> attachments = null, UploadTaskReport.StepResult result = null)
        {
            /*
            POST /api/chat.postMessage
            Content-type: application/json
            Authorization: Bearer xoxp-xxxxxxxxx-xxxx
            {   
                "channel":"xxxxxxxxxx",
                "text":"I hope the tour went well, Mr. Wonka.",
                "attachments":[
                    {
                        "text":"Who wins the lifetime supply of chocolate?",
                        "fallback":"You could be telling the computer exactly what it can do with a lifetime supply of chocolate.",
                        "color":"#3AA3E3",
                        "attachment_type":"default",
                        "callback_id":"select_simple_1234",
                        "actions":[
                            {
                                "name":"winners_list",
                                "text":"Who should win?",
                                "type":"select",
                                "data_source":"users"
                            }
                        ]
                    }
                ]
            }
            */

            string url = $"https://slack.com/api/chat.postMessage";
            Dictionary<string, object> messageData = new Dictionary<string, object>
            {
                { "channel", channel },
                { "text", text },
            };

            if (attachments != null && attachments.Count > 0)
            {
                List<Dictionary<string, object>> attachmentsList = new List<Dictionary<string, object>>();
                foreach (SlackAttachment attachment in attachments)
                {
                    Dictionary<string, object> attachmentDict = attachment.Serialize();
                    attachmentsList.Add(attachmentDict);
                }
                messageData["attachments"] = attachmentsList;
            }

            using (RequestWrapper www = RequestWrapper.Post(url, "https://slack.com/api/chat.postMessage"))
            {
                www.SetJSONData(messageData);
                www.SetRequestHeader("Authorization", $"Bearer {token}");

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to send slack message");
                    return new SlackSendMessageResponse(false);
                }

                string handlerText = response.Data;
                result?.AddLog(handlerText);
                
                // {
                //     "ok": true,
                //     "channel": "xxxxxxxxxx",
                //     "ts": "1763462862.962839",
                //     "message": {
                //         "user": "xxxxxxxxxx",
                //         "type": "message",
                //         "ts": "1763462862.962839",
                //         "bot_id": "xxxxxxxxxx",
                //         "app_id": "xxxxxxxxxx",
                //         "text": "Hello!",
                //         "team": "xxxxxxxxxx",
                //         "bot_profile": {
                //             "id": "xxxxxxxxxx",
                //             "app_id": "xxxxxxxxxx",
                //             "user_id": "xxxxxxxxxx",
                //             "name": "Build Uploader",
                //             "icons": {
                //                 "image_36": "https://a.slack-edge.com/80588/img/plugins/app/bot_36.png",
                //                 "image_48": "https://a.slack-edge.com/80588/img/plugins/app/bot_48.png",
                //                 "image_72": "https://a.slack-edge.com/80588/img/plugins/app/service_72.png"
                //             },
                //             "deleted": false,
                //             "updated": 1763462201,
                //             "team_id": "xxxxxxxxxx"
                //         },
                //         "blocks": [
                //         {
                //             "type": "rich_text",
                //             "block_id": "A6sd",
                //             "elements": [
                //             {
                //                 "type": "rich_text_section",
                //                 "elements": [
                //                 {
                //                     "type": "text",
                //                     "text": "Hello!"
                //                 }
                //                 ]
                //             }
                //             ]
                //         }
                //         ]
                //     },
                //     "warning": "missing_charset",
                //     "response_metadata": {
                //         "warnings": [
                //         "missing_charset"
                //             ]
                //     }
                // }
                
                // {
                //     "ok": false,
                //     "error": "invalid_auth",
                //     "warning": "missing_charset",
                //     "response_metadata": {
                //         "warnings": [
                //         "missing_charset"
                //             ]
                //     }
                // }
                string ts = "";
                string json = handlerText;
                Dictionary<string,string> responseDict = JSON.DeserializeObject<Dictionary<string, string>>(json);
                if (responseDict != null)
                {
                    responseDict.TryGetValue("ts", out ts);
                    if (responseDict.ContainsKey("ok") && responseDict["ok"] == "false")
                    {
                        string error = responseDict.ContainsKey("error") ? responseDict["error"] : "unknown_error";
                        result?.SetFailed($"Failed to send message to channel: {error}");
                        return new SlackSendMessageResponse(false);
                    }
                }

                result?.AddLog("Message Successful");
                return new SlackSendMessageResponse(true, ts);
            }
        }
        
        /// <summary>
        /// https://api.slack.com/apps/
        /// https://docs.slack.dev/apis/web-api/
        /// https://docs.slack.dev/app-management/quickstart-app-settings/
        /// https://docs.slack.dev/reference/methods/chat.update
        /// </summary>
        public static async Task<SlackSendMessageResponse> UpdateMessage(string ts, string text, string channel, string token, List<SlackAttachment> attachments = null, UploadTaskReport.StepResult result = null)
        {
            string url = $"https://slack.com/api/chat.update";
            Dictionary<string, object> messageData = new Dictionary<string, object>
            {
                { "ts", ts },
                { "channel", channel },
                { "text", text },
            };

            if (attachments != null && attachments.Count > 0)
            {
                List<Dictionary<string, object>> attachmentsList = new List<Dictionary<string, object>>();
                foreach (SlackAttachment attachment in attachments)
                {
                    Dictionary<string, object> attachmentDict = attachment.Serialize();
                    attachmentsList.Add(attachmentDict);
                }
                messageData["attachments"] = attachmentsList;
            }

            using (RequestWrapper www = RequestWrapper.Post(url, "https://slack.com/api/chat.update"))
            {
                www.SetJSONData(messageData);
                www.SetRequestHeader("Authorization", $"Bearer {token}");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to update slack message");
                    return new SlackSendMessageResponse(false);
                }

                string handlerText = response.Data;
                result?.AddLog(handlerText);
                
                // {
                //     "ok": true,
                //     "channel": "xxxxxxxxxxx",
                //     "ts": "1764926445.133279",
                //     "text": "Edited HAHAAA",
                //     "message": {
                //         "user": "xxxxxxxxxxx",
                //         "type": "message",
                //         "edited": {
                //             "user": "xxxxxxxxxxx",
                //             "ts": "1764926454.000000"
                //         },
                //         "bot_id": "xxxxxxxxxxx",
                //         "app_id": xxxxxxxxxxxx",
                //         "text": "Edited HAHAAA",
                //         "team": "T09T6PBP32B",
                //         "bot_profile": {
                //             "id": xxxxxxxxxxxx",
                //             "app_id": "xxxxxxxxxxx",
                //             "user_id": xxxxxxxxxxxx",
                //             "name": "Build Uploader",
                //             "icons": {
                //                 "image_36": "https://a.slack-edge.com/80588/img/plugins/app/bot_36.png",
                //                 "image_48": "https://a.slack-edge.com/80588/img/plugins/app/bot_48.png",
                //                 "image_72": "https://a.slack-edge.com/80588/img/plugins/app/service_72.png"
                //             },
                //             "deleted": false,
                //             "updated": 1763462201,
                //             "team_id": "T09T6PBP32B"
                //         },
                //         "blocks": [
                //         {
                //             "type": "rich_text",
                //             "block_id": "MTDw",
                //             "elements": [
                //             {
                //                 "type": "rich_text_section",
                //                 "elements": [
                //                 {
                //                     "type": "text",
                //                     "text": "Edited HAHAAA"
                //                 }
                //                 ]
                //             }
                //             ]
                //         }
                //         ]
                //     },
                //     "warning": "missing_charset",
                //     "response_metadata": {
                //         "warnings": [
                //         "missing_charset"
                //             ]
                //     }
                // }
                string json = handlerText;
                Dictionary<string,string> responseDict = JSON.DeserializeObject<Dictionary<string, string>>(json);
                if (responseDict != null)
                {
                    responseDict.TryGetValue("ts", out ts);
                    if (responseDict.ContainsKey("ok") && responseDict["ok"] == "false")
                    {
                        string error = responseDict.ContainsKey("error") ? responseDict["error"] : "unknown_error";
                        result?.SetFailed($"Failed to send message to channel: {error}");
                        return new SlackSendMessageResponse(false);
                    }
                }

                result?.AddLog("Message Successful");
                return new SlackSendMessageResponse(true, ts);
            }
        }
        
    }
}