using System;

namespace Wireframe
{
    public partial class DiscordConfig
    {
        [Serializable]
        public class DiscordWebhook : DropdownElement
        {
            public int Id
            {
                get => m_id;
                set => m_id = value;
            }

            public string DisplayName => Name;

            public string WebhookURL
            {
                get => EncodedEditorPrefs.GetString($"DiscordWebhookURL_{Name}", "");
                set => EncodedEditorPrefs.SetString($"DiscordWebhookURL_{Name}", value);
            }

            public string Name;

            private int m_id;

            public DiscordWebhook()
            {
                m_id = 0;
                Name = "Template";
            }

            public DiscordWebhook(int id, string displayName)
            {
                m_id = id;
                Name = displayName;
            }
        }
    }
}
