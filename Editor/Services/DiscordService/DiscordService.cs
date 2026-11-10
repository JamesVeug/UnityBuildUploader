using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal partial class DiscordService : AService
    {
        public static DiscordService Instance => InternalUtils.GetService<DiscordService>();
        
        public override string ServiceName => "Discord";
        public override string[] SearchKeywords => new string[]{"discord", "chat", "messaging"};
        
        public DiscordService()
        {
            // Needed for reflection
        }

        public override bool IsReadyToStartBuild(out GUIContent reason)
        {
            if (!Discord.Enabled)
            {
                reason = DisabledServiceGUI;
                return false;
            }

            reason = null;
            return true;
        }

        public override bool IsProjectSettingsSetup()
        {
            DiscordConfig data = DiscordUIUtils.GetConfig(false);
            if (data == null)
            {
                return false;
            }
            
            return data.servers.Count > 0 || (data.webhooks != null && data.webhooks.Count > 0);
        }
    }
}