using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal partial class DiscordService
    {
        private static ReorderableListOfDiscordAppsPreferences _reorderableListOfDiscordAppsPreferences;
        private static ReorderableListOfDiscordWebhooksPreferences _reorderableListOfDiscordWebhooksPreferences;

        public override void PreferencesGUI()
        {
            base.PreferencesGUI();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Apps are created on the developer dashboard.");
                if (GUILayout.Button("Developer Dashboard", GUILayout.Width(150)))
                {
                    Application.OpenURL("https://discord.com/developers/applications");
                }
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                Discord.Enabled = GUILayout.Toggle(Discord.Enabled, "Enabled");
                if (!Discord.Enabled)
                {
                    return;
                }

                DiscordConfig discordConfig = DiscordUIUtils.GetConfig();
                if (_reorderableListOfDiscordAppsPreferences == null)
                {
                    _reorderableListOfDiscordAppsPreferences = new ReorderableListOfDiscordAppsPreferences();
                    _reorderableListOfDiscordAppsPreferences.Initialize(discordConfig.apps, "Apps", 
                        true, (_) => 
                        {
                            DiscordUIUtils.AppPopup.Refresh();
                            DiscordUIUtils.ServerPopup.Refresh();
                            DiscordUIUtils.ChannelPopup.Refresh();
                            DiscordUIUtils.Save();
                        });
                }

                if (_reorderableListOfDiscordAppsPreferences.OnGUI())
                {
                    DiscordUIUtils.AppPopup.Refresh();
                    DiscordUIUtils.ServerPopup.Refresh();
                    DiscordUIUtils.ChannelPopup.Refresh();
                    DiscordUIUtils.Save();
                }

                GUILayout.Space(10);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Webhooks post to one channel without a bot. Create them in Channel Settings -> Integrations -> Webhooks.");
                    if (GUILayout.Button("Webhook Guide", GUILayout.Width(150)))
                    {
                        Application.OpenURL("https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks");
                    }
                }

                if (_reorderableListOfDiscordWebhooksPreferences == null)
                {
                    _reorderableListOfDiscordWebhooksPreferences = new ReorderableListOfDiscordWebhooksPreferences();
                    _reorderableListOfDiscordWebhooksPreferences.Initialize(discordConfig.webhooks, "Webhooks",
                        true, (_) =>
                        {
                            DiscordUIUtils.WebhookPopup.Refresh();
                            DiscordUIUtils.Save();
                        });
                }

                if (_reorderableListOfDiscordWebhooksPreferences.OnGUI())
                {
                    DiscordUIUtils.WebhookPopup.Refresh();
                    DiscordUIUtils.Save();
                }
            }
        }
    }
}