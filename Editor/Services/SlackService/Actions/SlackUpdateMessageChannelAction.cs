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
    [Wiki(nameof(SlackUpdateMessageChannelAction), "actions", "Use a bot to send a message to a channel on a Slack server.")]
    [UploadAction("Slack Update Message")]
    public partial class SlackUpdateMessageChannelAction : AUploadAction
    {
        [Wiki("App", "Which App/Bot will be sending the message", 1)]
        private SlackConfig.SlackApp m_app;
        
        [Wiki("Server", "What server to send the message to", 2)]
        private SlackConfig.SlackServer m_server;
        
        [Wiki("Channel", "What channel in the server to send the message to", 3)]
        private SlackConfig.SlackChannel m_channel;

        [Wiki("Text", "What text to update it to", 4)] 
        private string m_text = "";

        [Wiki("Attachments", "A list of attached messages with the message. This is optional and can be used to format the message nicely.", 5)]
        private List<SlackAttachment> m_attachments = new List<SlackAttachment>();
        
        [Wiki("Message Timestamp", "The TimeStamp of the message we want to update.", 6)]
        private string m_messageTimeStamp = "";

        public SlackUpdateMessageChannelAction() : base()
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

        public void AddAttachment(SlackAttachment attachment)
        {
            m_attachments.Add(attachment);
        }
        
        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            string text = m_context.FormatString(m_text);
            string ts = m_context.FormatString("{" + m_messageTimeStamp + "}");
            SlackSendMessageResponse response = await Slack.UpdateMessage(ts, text, m_channel.ChannelID, m_app.Token, m_attachments.Select(a => a.Format(m_context)).ToList(), stepResult);
            return response.Successful;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            SlackService service = InternalUtils.GetService<SlackService>();
            if (!Slack.Enabled)
            {
                errors.Add(service.DisabledServiceGUI);
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
                { "messageTimeStamp", m_messageTimeStamp },
                { "text", m_text }
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

            if (data.TryGetValue("messageTimeStamp", out object messageTimeStampObj) && messageTimeStampObj != null)
            {
                m_messageTimeStamp = messageTimeStampObj.ToString();
            }
        }
    }
}
