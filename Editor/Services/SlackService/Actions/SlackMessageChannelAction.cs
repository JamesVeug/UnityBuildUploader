using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Send a message to a channel on a Slack server using a bot.
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(SlackMessageChannelAction), "actions", "Use a bot to send a message to a channel on a Slack server.")]
    [UploadAction("Slack Send Message")]
    public partial class SlackMessageChannelAction : AUploadAction
    {
        [Wiki("App", "Which App/Bot will be sending the message", 1)]
        private SlackConfig.SlackApp m_app;
        
        [Wiki("Server", "What server to send the message to", 2)]
        private SlackConfig.SlackServer m_server;
        
        [Wiki("Channel", "What channel in the server to send the message to", 3)]
        private SlackConfig.SlackChannel m_channel;

        [Wiki("Text", "What text to send", 4)] 
        private string m_text = "";

        [Wiki("Attachments", "A list of attached messages with the message. This is optional and can be used to format the message nicely.", 5)]
        private List<SlackAttachment> m_attachments = new List<SlackAttachment>();
        
        [Wiki("ResponseFormatName", "When sending a Slack message we receive a Timestamp of that message. If a formatName is provided then that TS is saved as a string format to be used elsewhere in the Upload Task. eg: editing it as a post action.", 6)]
        private Command m_responseTSFormat; // Created in CreateContext()

        public SlackMessageChannelAction() : base()
        {
            // Required for reflection
        }

        public void SetApp(string token)
        {
            m_app = new SlackConfig.SlackApp()
            {
                Token = token
            };
        }

        public void SetServer(long serverID)
        {
            m_server = new SlackConfig.SlackServer()
            {
                ServerID = serverID
            };
        }

        public void SetChannel(string channelID)
        {
            m_channel = new SlackConfig.SlackChannel()
            {
                ChannelID = channelID
            };
        }

        public void SetText(string text)
        {
            m_text = text;
        }

        public void SetResponseTSFormatName(string TSFormatName)
        {
            if (TSFormatName.Length > 0)
            {
                if (TSFormatName[0] != '{')
                {
                    TSFormatName = '{' + TSFormatName;
                }
                if (TSFormatName[TSFormatName.Length - 1] != '}')
                {
                    TSFormatName += '}';
                }
            }
            
            m_responseTSFormat.Key = TSFormatName;
        }

        public void AddAttachment(SlackAttachment attachment)
        {
            m_attachments.Add(attachment);
        }
        
        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            string text = m_context.FormatString(m_text);
            SlackSendMessageResponse response = await Slack.SendMessage(text, m_channel.ChannelID, m_app.Token, m_attachments, stepResult);
            m_recordedMessageTimeStamp = response.MessageTimeStamp;
            return response.Successful;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);
            
            SlackService service = InternalUtils.GetService<SlackService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Slack App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.Token))
            {
                errors.Add(service.PreferencesLink($"Slack App {m_app.Name} does not have a token set.", ""));
            }

            if (m_server == null)
            {
                errors.Add(new GUIContent("Server is not set."));
            }

            if (m_channel == null)
            {
                errors.Add(new GUIContent("Channel is not set."));
            }

            if (string.IsNullOrEmpty(m_text))
            {
                errors.Add(new GUIContent("Text is not set."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            var data = new Dictionary<string, object>
            {
                { "app", m_app?.Id ?? 0 },
                { "serverId", m_server?.Id ?? 0 },
                { "channelId", m_channel?.ChannelID ?? "" },
                { "text", m_text },
                { "tsFormat", m_responseTSFormat.Key }
            };
            
            if (m_attachments != null && m_attachments.Count > 0)
            {
                List<Dictionary<string, object>> embedsList = new List<Dictionary<string, object>>();
                foreach (var embed in m_attachments)
                {
                    embedsList.Add(embed.Serialize());
                }
                data["attachments"] = embedsList;
            }
            else
            {
                data["attachments"] = new List<Dictionary<string, object>>(); // Ensure attachments are always present
            }
            
            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            SlackConfig.SlackApp[] buildConfigs = SlackUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object configIDString) && configIDString != null)
            {
                m_app = buildConfigs.FirstOrDefault(a => a.Id == (long)configIDString);
            }
            
            SlackConfig.SlackServer[] servers = SlackUIUtils.ServerPopup.Values;
            if (data.TryGetValue("serverId", out object serverIDString) && serverIDString != null)
            {
                m_server = servers.FirstOrDefault(a => a.Id == (long)serverIDString);
                if (m_server != null)
                {
                    List<(SlackConfig.SlackServer, List<SlackConfig.SlackChannel>)> channels = SlackUIUtils.ChannelPopup.GetAllData();
                    if (data.TryGetValue("channelId", out object channelIDString) && channelIDString != null &&
                        channels != null)
                    {
                        (SlackConfig.SlackServer, List<SlackConfig.SlackChannel>) channel =
                            channels.FirstOrDefault(a => a.Item1.Id == m_server.Id);
                        if (channel.Item2 != null)
                        {
                            m_channel = channel.Item2.FirstOrDefault(c => c.ChannelID == (string)channelIDString);
                        }
                    }
                }
            }
            
            
            if (data.TryGetValue("text", out object textObj) && textObj != null)
            {
                m_text = textObj.ToString();
            }
            else
            {
                m_text = string.Empty; // Default to empty string if not set
            }

            if (data.TryGetValue("tsFormat", out object tsFormatObj) && tsFormatObj != null)
            {
                if (tsFormatObj is string tsFormatString)
                {
                    m_responseTSFormat.Key = tsFormatString;
                }
            }
            
            if (data.TryGetValue("attachments", out object embedsObj) && embedsObj is List<object> embedsList)
            {
                m_attachments = new List<SlackAttachment>();
                foreach (var embedData in embedsList)
                {
                    if (embedData is Dictionary<string, object> embedDict)
                    {
                        SlackAttachment attachment = SlackAttachment.Deserialize(embedDict);
                        m_attachments.Add(attachment);
                    }
                }
            }
            else
            {
                m_attachments = new List<SlackAttachment>(); // Default to empty list if not set
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
                ["recordedMessageTimeStamp"] = m_recordedMessageTimeStamp,
            };
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            m_recordedMessageTimeStamp = state["recordedMessageTimeStamp"] as string;
        }
    }
}
