using System.Collections.Generic;

namespace Wireframe
{
    public class SlackAttachment
    {
        public class Action
        {
            public string name;
            public string text;
            public string type;
            public string data_source;
        }
        
        public string text;
        public string fallback;
        public string color;
        public string attachment_type;
        public string callback_id;
        public List<Action> actions = new List<Action>();

        public Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> attachmentDict = new Dictionary<string, object>
            {
                { "text", text },
                { "fallback", fallback },
                { "color", color },
                { "attachment_type", attachment_type },
                { "callback_id", callback_id },
            };
                
            List<Dictionary<string, string>> attachmentsList2 = new List<Dictionary<string, string>>();
            foreach (Action action in actions)
            {
                Dictionary<string, string> actionDict = new Dictionary<string, string>
                {
                    { "name", action.name },
                    { "text", action.text },
                    { "type", action.type },
                    { "data_source", action.data_source },
                };
                attachmentsList2.Add(actionDict);
            }
            attachmentDict["actions"] = attachmentsList2;
            return attachmentDict;
        }
        
        public static SlackAttachment Deserialize(Dictionary<string, object> dict)
        {
            SlackAttachment slackAttachment = new SlackAttachment();
            slackAttachment.text = dict.ContainsKey("text") ? dict["text"].ToString() : "";
            slackAttachment.fallback = dict.ContainsKey("fallback") ? dict["fallback"].ToString() : "";
            slackAttachment.color = dict.ContainsKey("color") ? dict["color"].ToString() : "";
            slackAttachment.attachment_type = dict.ContainsKey("attachment_type") ? dict["attachment_type"].ToString() : "";
            slackAttachment.callback_id = dict.ContainsKey("callback_id") ? dict["callback_id"].ToString() : "";
            
            if (dict.ContainsKey("actions"))
            {
                List<object> actionsList = dict["actions"] as List<object>;
                if (actionsList != null)
                {
                    foreach (object actionObj in actionsList)
                    {
                        Dictionary<string, object> actionDict = actionObj as Dictionary<string, object>;
                        if (actionDict != null)
                        {
                            Action action = new Action();
                            action.name = actionDict.ContainsKey("name") ? actionDict["name"].ToString() : "";
                            action.text = actionDict.ContainsKey("text") ? actionDict["text"].ToString() : "";
                            action.type = actionDict.ContainsKey("type") ? actionDict["type"].ToString() : "";
                            action.data_source = actionDict.ContainsKey("data_source") ? actionDict["data_source"].ToString() : "";
                            slackAttachment.actions.Add(action);
                        }
                    }
                }
            }

            return slackAttachment;
        }

        /// <summary>
        /// Returns a copy with the user-facing text run through <see cref="Context.FormatString"/>, leaving this one untouched.
        /// </summary>
        public SlackAttachment Format(Context context)
        {
            SlackAttachment formatted = (SlackAttachment)MemberwiseClone();
            formatted.text = context.FormatString(text);
            formatted.fallback = context.FormatString(fallback);
            formatted.actions = new List<Action>();
            foreach (Action action in actions ?? new List<Action>())
            {
                formatted.actions.Add(new Action
                {
                    name = action.name,
                    text = context.FormatString(action.text),
                    type = action.type,
                    data_source = action.data_source
                });
            }

            return formatted;
        }
    }
}