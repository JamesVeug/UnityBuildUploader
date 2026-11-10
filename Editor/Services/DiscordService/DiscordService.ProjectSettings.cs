using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal partial class DiscordService
    {
        private static ReorderableListOfDiscordAppsProjectSettings _reorderableListOfDiscordAppsProjectSettings;
        private static ReorderableListOfDiscordChannels _reorderableListOfDiscordChannels;
        private static ReorderableListOfDiscordWebhooksProjectSettings _reorderableListOfDiscordWebhooksProjectSettings;
        private static DiscordConfig.DiscordServer m_SelectedServer;
        private static Context m_context = new Context();

        public override bool HasProjectSettingsGUI => true;

        public override void ProjectSettingsGUI()
        {
            base.ProjectSettingsGUI();
            using (new GUILayout.VerticalScope("box"))
            {
                DiscordConfig discordConfig = DiscordUIUtils.GetConfig();

                GUILayout.Label("Servers", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DiscordUIUtils.ServerPopup.DrawPopup(ref m_SelectedServer, m_context, GUILayout.Width(120)))
                    {
                        _reorderableListOfDiscordChannels = null;
                    }
                    
                    if(GUILayout.Button("Add Server", GUILayout.Width(100)))
                    {
                        DiscordConfig.DiscordServer config = new DiscordConfig.DiscordServer();
                        List<DiscordConfig.DiscordServer> servers = DiscordUIUtils.GetConfig().servers;
                        config.Id = servers.Count > 0 ? servers.Max(a=>a.Id) + 1 : 1;
                        servers.Add(config);
                        DiscordUIUtils.Save();
                        DiscordUIUtils.ServerPopup.Refresh();
                        DiscordUIUtils.AppPopup.Refresh();
                        DiscordUIUtils.ChannelPopup.Refresh();
                        _reorderableListOfDiscordChannels = null;
                        m_SelectedServer = config;
                    }
                    
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledGroupScope(m_SelectedServer == null))
                    {
                        if (GUILayout.Button("Remove Server", GUILayout.Width(100)))
                        {
                            if (EditorUtility.DisplayDialog("Remove Server",
                                    "Are you sure you want to remove the selected discord server?", "Yes", "No"))
                            {
                                List<DiscordConfig.DiscordServer> servers = DiscordUIUtils.GetConfig().servers;
                                servers.Remove(m_SelectedServer);
                                DiscordUIUtils.Save();
                                DiscordUIUtils.ServerPopup.Refresh();
                                DiscordUIUtils.AppPopup.Refresh();
                                DiscordUIUtils.ChannelPopup.Refresh();
                                m_SelectedServer = null;
                            }
                        }
                    }
                }

                using (new GUILayout.VerticalScope("box"))
                {
                    if (m_SelectedServer != null)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Name:", GUILayout.Width(120));
                            string newConfigName = EditorGUILayout.TextField(m_SelectedServer.Name);
                            if (newConfigName != m_SelectedServer.Name)
                            {
                                m_SelectedServer.Name = newConfigName;
                                DiscordUIUtils.Save();
                                DiscordUIUtils.ServerPopup.Refresh();
                                DiscordUIUtils.AppPopup.Refresh();
                                DiscordUIUtils.ChannelPopup.Refresh();
                            }
                        }
                        
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Server ID:", GUILayout.Width(120));
                            long newConfigName = EditorGUILayout.LongField(m_SelectedServer.ServerID);
                            if (newConfigName != m_SelectedServer.ServerID)
                            {
                                m_SelectedServer.ServerID = newConfigName;
                                DiscordUIUtils.Save();
                                DiscordUIUtils.ServerPopup.Refresh();
                                DiscordUIUtils.AppPopup.Refresh();
                                DiscordUIUtils.ChannelPopup.Refresh();
                            }
                        }
                        
                        if(_reorderableListOfDiscordChannels == null)
                        {
                            _reorderableListOfDiscordChannels = new ReorderableListOfDiscordChannels();
                            _reorderableListOfDiscordChannels.Initialize(m_SelectedServer.channels, "Channels", 
                                true, (_) => { DiscordUIUtils.Save(); });
                        }
                        
                        if (_reorderableListOfDiscordChannels.OnGUI())
                        {
                            DiscordUIUtils.Save();
                        }
                    }
                }


                GUILayout.Space(20);

                if (_reorderableListOfDiscordAppsProjectSettings == null)
                {
                    _reorderableListOfDiscordAppsProjectSettings = new ReorderableListOfDiscordAppsProjectSettings();
                    _reorderableListOfDiscordAppsProjectSettings.Initialize(discordConfig.apps, "Apps",
                        true, (_) => { DiscordUIUtils.Save(); });
                }

                GUILayout.Label("Apps", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Apps are created on the developer dashboard.");
                    if (GUILayout.Button("Developer Dashboard", GUILayout.Width(150)))
                    {
                        Application.OpenURL("https://discord.com/developers/applications");
                    }
                }
                GUILayout.Label("See Edit->Preferences->Build Uploader->Services->Discord to enter App Token");
                
                if (_reorderableListOfDiscordAppsProjectSettings.OnGUI())
                {
                    DiscordUIUtils.Save();
                }

                GUILayout.Label("To set Token used by the App/Bos see Preferences->Build Uploader->Services->Discord.",
                    EditorStyles.wordWrappedLabel);

                GUILayout.Space(20);

                GUILayout.Label("Webhooks", EditorStyles.boldLabel);
                GUILayout.Label("Webhooks are referenced by name. See Edit->Preferences->Build Uploader->Services->Discord to enter the Webhook URL.",
                    EditorStyles.wordWrappedLabel);

                if (_reorderableListOfDiscordWebhooksProjectSettings == null)
                {
                    _reorderableListOfDiscordWebhooksProjectSettings = new ReorderableListOfDiscordWebhooksProjectSettings();
                    _reorderableListOfDiscordWebhooksProjectSettings.Initialize(discordConfig.webhooks, "Webhooks",
                        true, (_) =>
                        {
                            DiscordUIUtils.WebhookPopup.Refresh();
                            DiscordUIUtils.Save();
                        });
                }

                if (_reorderableListOfDiscordWebhooksProjectSettings.OnGUI())
                {
                    DiscordUIUtils.WebhookPopup.Refresh();
                    DiscordUIUtils.Save();
                }
            }
        }
    }
}