using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wireframe
{
    [Serializable]
    public partial class DiscordConfig 
    {
        [SerializeField]
        public List<DiscordServer> servers;
        
        [SerializeField]
        public List<DiscordApp> apps;
        
        [SerializeField]
        public List<DiscordWebhook> webhooks;
        
        public void Initialize()
        {
            apps = new List<DiscordApp>(2);
            servers = new List<DiscordServer>(2);
            webhooks = new List<DiscordWebhook>(2);
        }
    }
}