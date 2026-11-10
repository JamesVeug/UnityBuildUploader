using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wireframe
{
    internal static partial class DiscordUIUtils
    {
        private static readonly string FilePath = Application.dataPath + "/../BuildUploader/DiscordConfig.json";

        private static DiscordConfig data;

        public static DiscordConfig GetConfig(bool createIfMissing = true)
        {
            if (data == null && createIfMissing)
            {
                if (File.Exists(FilePath))
                {
                    LoadFile(FilePath);
                }
                else
                {
                    Debug.Log("DiscordConfig does not exist. Creating new file");
                    data = new DiscordConfig();
                    data.Initialize();
                    Save();
                }
            }

            return data;
        }

        private static void LoadFile(string path)
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<DiscordConfig>(json);
            if (data == null)
            {
                Debug.Log("Config has bad json so creating new config");
                data = new DiscordConfig();
                data.Initialize();
                Save();
            }
            else
            {
                if (data.webhooks == null)
                {
                    data.webhooks = new List<DiscordConfig.DiscordWebhook>(2);
                }

                for (var i = 0; i < data.apps.Count; i++)
                {
                    DiscordConfig.DiscordApp app = data.apps[i];
                    app.Id = i + 1;
                }

                for (var i = 0; i < data.servers.Count; i++)
                {
                    data.servers[i].Id = i + 1;
                    for (var j = 0; j < data.servers[i].channels.Count; j++)
                    {
                        data.servers[i].channels[j].Id = j + 1;
                    }
                }

                for (var i = 0; i < data.webhooks.Count; i++)
                {
                    data.webhooks[i].Id = i + 1;
                }
            }
        }

        public static void Save()
        {
            if (data != null)
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, true);
                if (!File.Exists(FilePath))
                {
                    var stream = File.Create(FilePath);
                    stream.Close();
                }

                File.WriteAllText(FilePath, json);
            }
        }
        
        public class DiscordAppPopup : CustomDropdown<DiscordConfig.DiscordApp>
        {
            public override string FirstEntryText => "Choose App";

            protected override List<DiscordConfig.DiscordApp> FetchAllData()
            {
                GetConfig();
                return data.apps;
            }
        }
        
        public class DiscordServerPopup : CustomDropdown<DiscordConfig.DiscordServer>
        {
            public override string FirstEntryText => "Choose Server";

            protected override List<DiscordConfig.DiscordServer> FetchAllData()
            {
                GetConfig();
                return data.servers;
            }
        }
        
        
        
        public class DiscordChannelPopup : CustomMultiDropdown<DiscordConfig.DiscordServer, DiscordConfig.DiscordChannel>
        {
            public override string FirstEntryText => "Choose Channel";
            
            public override List<(DiscordConfig.DiscordServer, List<DiscordConfig.DiscordChannel>)> GetAllData()
            {
                GetConfig();
                
                List<(DiscordConfig.DiscordServer, List<DiscordConfig.DiscordChannel>)> dataList = new List<(DiscordConfig.DiscordServer, List<DiscordConfig.DiscordChannel>)>();
                foreach (DiscordConfig.DiscordServer server in data.servers)
                {
                    dataList.Add((server, server.channels));
                }
                return dataList;
            }
        }
        
        public class DiscordWebhookPopup : CustomDropdown<DiscordConfig.DiscordWebhook>
        {
            public override string FirstEntryText => "Choose Webhook";

            protected override List<DiscordConfig.DiscordWebhook> FetchAllData()
            {
                GetConfig();
                return data.webhooks;
            }
        }
        
        public static DiscordAppPopup AppPopup => m_appPopup ?? (m_appPopup = new DiscordAppPopup());
        private static DiscordAppPopup m_appPopup;
        
        public static DiscordServerPopup ServerPopup => m_serverPopup ?? (m_serverPopup = new DiscordServerPopup());
        private static DiscordServerPopup m_serverPopup;
        
        public static DiscordChannelPopup ChannelPopup => m_channelPopup ?? (m_channelPopup = new DiscordChannelPopup());
        private static DiscordChannelPopup m_channelPopup;
        
        public static DiscordWebhookPopup WebhookPopup => m_webhookPopup ?? (m_webhookPopup = new DiscordWebhookPopup());
        private static DiscordWebhookPopup m_webhookPopup;
    }
}