using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Send a message to a channel on a discord server using a bot.
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(DiscordMessageChannelAction), "actions", "Use a bot to send a message to a channel on a discord server.")]
    [UploadAction("Discord Message Channel")]
    public partial class DiscordMessageChannelAction : AUploadAction
    {
        [Serializable]
        [Wiki("Embed", "An embed to send with the message. This is optional and can be used to format the message nicely.")]
        public class Embed
        {
            [Wiki("Title", "The title of the embed. This is optional.")]
            public string title;
            
            [Wiki("Description", "The description of the embed. This is optional.")]
            public string description;
            
            [Wiki("Color", "The color of the embed in hexidecimal. This is optional.")]
            public string color;
        }

        /// <summary>
        /// A dry run writes the request to the report without sending it
        /// </summary>
        public override bool SkippedDuringDryRun => false;
        
        [Wiki("App", "Which App/Bot will be sending the message", 1)]
        private DiscordConfig.DiscordApp m_app;
        
        [Wiki("Server", "What Discord server to send the message to", 2)]
        private DiscordConfig.DiscordServer m_server;
        
        [Wiki("Channel", "What channel in the Discord server to send the message to", 2)]
        private DiscordConfig.DiscordChannel m_channel;

        [Wiki("Text", "What text to send", 3)] 
        private string m_text = "";

        [Wiki("Embeds", "A list of embeds to send with the message. This is optional and can be used to format the message nicely.", 5)]
        private List<Embed> m_embeds = new List<Embed>();

        public DiscordMessageChannelAction() : base()
        {
            // Required for reflection
        }

        public void SetApp(string token)
        {
            m_app = new DiscordConfig.DiscordApp()
            {
                Token =  token
            };
        }
        
        public void SetServer(long server)
        {
            m_server = new DiscordConfig.DiscordServer(0, "Configured server", server);
        }
        
        public void SetChannel(long channel)
        {
            m_channel = new DiscordConfig.DiscordChannel()
            {
                ChannelID = channel
            };
        }

        public void SetText(string text)
        {
            m_text = text;
        }

        public void AddEmbed(string title, string description, Color color)
        {
            Embed embed = new Embed()
            {
                title = title,
                description = description,
                color = "#" + ColorUtility.ToHtmlStringRGB(color),
            };
            AddEmbed(embed);
        }

        public void AddEmbed(Embed embed)
        {
            m_embeds.Add(embed);
        }
        
        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            List<Dictionary<string, object>> embeds = DiscordEmbedUtils.ToPayload(m_embeds, m_context);
            string text = m_context.FormatString(m_text);
            return await Discord.SendMessageToChannel(m_channel.ChannelID, text, m_app?.Token, m_app?.IsBot ?? true, embeds, stepResult, m_dryRun);
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            DiscordService service = InternalUtils.GetService<DiscordService>();
            if (!m_dryRun && !service.IsReadyToStartBuild(out GUIContent reason))
            {
                errors.Add(reason);
            }

            if (!m_dryRun && m_app == null)
            {
                errors.Add(new GUIContent("Discord App is not set."));
            }
            else if (!m_dryRun && string.IsNullOrEmpty(m_app.Token))
            {
                errors.Add(service.PreferencesLink($"Discord App {m_app.Name} does not have a token set.", ""));
            }

            if (!m_dryRun && m_server == null)
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
                { "channelId", m_channel?.ChannelID ?? 0 },
                { "text", m_text },
                { "dryRun", m_dryRun }
            };

            data["embeds"] = DiscordEmbedUtils.Serialize(m_embeds);
            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            DiscordConfig.DiscordApp[] buildConfigs = DiscordUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object configIDString) && configIDString != null)
            {
                m_app = buildConfigs.FirstOrDefault(a => a.Id == (long)configIDString);
            }
            
            DiscordConfig.DiscordServer[] servers = DiscordUIUtils.ServerPopup.Values;
            if (data.TryGetValue("serverId", out object serverIDString) && serverIDString != null)
            {
                m_server = servers.FirstOrDefault(a => a.Id == (long)serverIDString);
                if (m_server != null){
                    List<(DiscordConfig.DiscordServer, List<DiscordConfig.DiscordChannel>)> channels = DiscordUIUtils.ChannelPopup.GetAllData();
                    if (data.TryGetValue("channelId", out object channelIDString) && channelIDString != null && channels != null)
                    {
                        (DiscordConfig.DiscordServer, List<DiscordConfig.DiscordChannel>) channel = channels.FirstOrDefault(a => a.Item1.Id == m_server.Id);
                        if (channel.Item2 != null)
                        {
                            m_channel = channel.Item2.FirstOrDefault(c => c.ChannelID == (long)channelIDString);
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
            m_dryRun = data.TryGetValue("dryRun", out object dryRunObj) && dryRunObj is bool dryRun && dryRun;

            m_embeds = DiscordEmbedUtils.Deserialize(data);
        }
    }
}
