using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// TODO: Move requests to a wrapper
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Wireframe
{
    /// <summary>
    /// https://discord.com/developers/applications
    /// </summary>
    internal partial class Discord
    {
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("discord_enabled");
            set => ProjectEditorPrefs.SetBool("discord_enabled", value);
        }

        /// <summary>
        /// https://discord.com/developers/docs/resources/message#create-message-jsonform-params
        /// </summary>
        public static async Task<bool> SendMessageToChannel(long channelID, string text, string token, bool isBot, List<Dictionary<string, object>> embeds = null, UploadTaskReport.StepResult result = null, bool dryRun = false)
        {
            string url = $"https://discord.com/api/v10/channels/{channelID}/messages";
            Dictionary<string, object> messageData = new Dictionary<string, object>
            {
                { "content", text },
            };

            if (embeds != null && embeds.Count > 0)
            {
                messageData["embeds"] = embeds;
            }

            if (dryRun)
            {
                // Exercise the same payload serializer without constructing or sending a request.
                string payload = JSON.SerializeObject(messageData);
                result?.AddLog("Discord dry run: POST " + url);
                result?.AddLog(payload);
                return true;
            }

            using (RequestWrapper www = RequestWrapper.Post(url, "https://discord.com/api/v10/channels/xxx/messages"))
            {
                www.SetJSONData(messageData);
                if (isBot)
                {
                    www.SetRequestHeader("Authorization", $"Bot {token}");
                }
                else
                {
                    www.SetRequestHeader("Authorization", token);
                }

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to send discord message");
                    return false;
                }

                result?.AddLog("Discord Message sent");
                return true;
            }
        }

        public static async Task<bool> GetMe(string token, bool isBot)
        {
            string url = "https://discord.com/api/v10/users/@me";
            RequestWrapper request = RequestWrapper.Get(url, "https://discord.com/api/v10/users/@me");
            if (isBot)
            {
                request.SetRequestHeader("Authorization", $"Bot {token}");
            }
            else
            {
                request.SetRequestHeader("Authorization", token);
            }
            
            var response = await request.SendAsync(null);
            if (response.IsSuccessful)
            {
                Debug.Log("Response: " + response.Data);
                return true;
            }
            else
            {
                Debug.LogError("Error: " + response.Data);
                return false;
            }
        }

        public static async Task<bool> GetServers(string token, bool isBot)
        {
            string url = $"https://discord.com/api/v10/@me/guids";
            RequestWrapper request = RequestWrapper.Get(url, "https://discord.com/api/v10/@me/guids");
            if (isBot)
            {
                request.SetRequestHeader("Authorization", $"Bot {token}");
            }
            else
            {
                request.SetRequestHeader("Authorization", token);
            }
            
            var response = await request.SendAsync(null);
            if (response.IsSuccessful)
            {
                Debug.Log("Response: " + response.Data);
                return true;
            }
            else
            {
                Debug.LogError("Error: " + response.Data);
                return false;
            }
        }
    }
}
