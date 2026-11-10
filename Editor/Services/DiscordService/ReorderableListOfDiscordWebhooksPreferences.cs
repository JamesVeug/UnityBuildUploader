using System;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class ReorderableListOfDiscordWebhooksPreferences : InternalReorderableList<DiscordConfig.DiscordWebhook>
    {
        private bool showWebhook;

        protected override void DrawItem(Rect containerRect, int index, bool isActive, bool isFocused)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DiscordConfig.DiscordWebhook element = list[index];

                // Name
                float labelWidth = 60;
                float textWidth = 150;
                Rect rect0 = new Rect(containerRect.x, containerRect.y, labelWidth, containerRect.height);
                GUI.Label(rect0, new GUIContent("Name", "Display name for this webhook. UI only — not sent to Discord."));
                rect0.x += rect0.width;

                rect0.width = textWidth;
                string n = EditorUtils.PlaceholderTextField(rect0, element.Name, "e.g. Build Notifications");
                rect0.x += rect0.width;
                if (n != element.Name)
                {
                    element.Name = n;
                    dirty = true;
                }

                // Webhook URL
                rect0.width = labelWidth;
                GUI.Label(rect0, new GUIContent("URL", "Webhook URL from Discord: Channel Settings -> Integrations -> Webhooks -> Copy Webhook URL."));
                rect0.x += rect0.width;

                rect0.width = containerRect.width - textWidth - labelWidth * 2 - 90;
                string url = element.WebhookURL;
                if (showWebhook)
                {
                    string t = EditorUtils.PlaceholderTextField(rect0, url, "https://discord.com/api/webhooks/xxx/xxx");
                    if (t != url)
                    {
                        element.WebhookURL = t;
                        dirty = true;
                    }
                }
                else
                {
                    GUI.Label(rect0, new string('*', url.Length));
                }
                rect0.x += rect0.width;

                // Padding
                rect0.width = 10;
                GUI.Label(rect0, "");
                rect0.x += rect0.width;

                // Show Toggle
                rect0.width = 70;
                showWebhook = GUI.Toggle(rect0, showWebhook, "Show");
            }
        }

        protected override DiscordConfig.DiscordWebhook CreateItem(int index)
        {
            return new DiscordConfig.DiscordWebhook(index, "MyWebhook");
        }

        protected override int CompareTo(DiscordConfig.DiscordWebhook a, DiscordConfig.DiscordWebhook b)
        {
            return String.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        }
    }
}
