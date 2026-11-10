using System;
using System.Collections.Generic;

namespace Wireframe
{
    /// <summary>
    /// Shared embed conversion for the Discord actions so their payloads and saved data stay identical.
    /// </summary>
    internal static class DiscordEmbedUtils
    {
        public static List<Dictionary<string, object>> ToPayload(List<DiscordMessageChannelAction.Embed> embeds, Context context)
        {
            List<Dictionary<string, object>> payload = new List<Dictionary<string, object>>();
            if (embeds == null)
            {
                return payload;
            }

            foreach (DiscordMessageChannelAction.Embed embed in embeds)
            {
                Dictionary<string, object> embedDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(embed.title))
                {
                    embedDict.Add("title", context.FormatString(embed.title));
                }

                if (!string.IsNullOrEmpty(embed.description))
                {
                    embedDict.Add("description", context.FormatString(embed.description));
                }

                if (!string.IsNullOrEmpty(embed.color))
                {
                    string colorHex = embed.color;
                    if (colorHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        colorHex = colorHex.Substring(2);
                    }
                    if (colorHex.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    {
                        colorHex = colorHex.Substring(1);
                    }

                    embedDict.Add("color", int.Parse(colorHex, System.Globalization.NumberStyles.HexNumber));
                }

                payload.Add(embedDict);
            }

            return payload;
        }

        public static List<object> Serialize(List<DiscordMessageChannelAction.Embed> embeds)
        {
            List<object> embedsList = new List<object>();
            if (embeds == null)
            {
                return embedsList;
            }

            foreach (DiscordMessageChannelAction.Embed embed in embeds)
            {
                embedsList.Add(new Dictionary<string, object>
                {
                    { "title", embed.title },
                    { "description", embed.description },
                    { "color", embed.color }
                });
            }

            return embedsList;
        }

        public static List<DiscordMessageChannelAction.Embed> Deserialize(Dictionary<string, object> data)
        {
            List<DiscordMessageChannelAction.Embed> embeds = new List<DiscordMessageChannelAction.Embed>();
            if (!data.TryGetValue("embeds", out object embedsObj) || !(embedsObj is List<object> embedsList))
            {
                return embeds;
            }

            foreach (object embedData in embedsList)
            {
                if (embedData is Dictionary<string, object> embedDict)
                {
                    embeds.Add(new DiscordMessageChannelAction.Embed
                    {
                        title = embedDict.ContainsKey("title") ? embedDict["title"].ToString() : string.Empty,
                        description = embedDict.ContainsKey("description") ? embedDict["description"].ToString() : string.Empty,
                        color = embedDict.ContainsKey("color") ? embedDict["color"].ToString() : string.Empty
                    });
                }
            }

            return embeds;
        }
    }
}
