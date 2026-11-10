using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class ReorderableListOfDiscordWebhooksProjectSettings : InternalReorderableList<DiscordConfig.DiscordWebhook>
    {
        protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DiscordConfig.DiscordWebhook element = list[index];

                float labelWidth = 50;
                float width = Mathf.Min(200, rect.width / 2);
                Rect rect1 = new Rect(rect.x, rect.y, labelWidth, rect.height);
                GUI.Label(rect1, new GUIContent("Name", "Display name for this webhook. UI only — not sent to Discord."));
                rect1.x += rect1.width;
                rect1.width = width;
                string n = EditorUtils.PlaceholderTextField(rect1, element.Name, "e.g. Build Notifications");
                if (n != element.Name)
                {
                    element.Name = n;
                    dirty = true;
                }
            }
        }

        protected override DiscordConfig.DiscordWebhook CreateItem(int index)
        {
            return new DiscordConfig.DiscordWebhook(index, "MyWebhook");
        }

        protected override int CompareTo(DiscordConfig.DiscordWebhook a, DiscordConfig.DiscordWebhook b)
        {
            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
        }
    }
}
