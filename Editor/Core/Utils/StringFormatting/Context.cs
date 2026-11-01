using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wireframe
{
    public partial class Context
    {
        public List<Command> LocalCommands => m_localCommands;
        
        private Context m_parent;
        private List<Command> m_localCommands = new List<Command>(); // List because the keys can be dynamic
        private Dictionary<string, string> m_cachedValues = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public Context(Context parent = null)
        {
            SetParent(parent);
        }

        public void SetParent(Context context)
        {
            m_parent = context;
        }

        public string FormatString(string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                return format;
            }

            int index = 0;
                
            try {
                while (index < format.Length)
                {
                    int nextIndex = format.IndexOf('{', index);
                    if (nextIndex == -1)
                    {
                        break;
                    }

                    int endIndex = Utils.GetClosingBracketIndex(format, nextIndex, '{', '}');
                    if (endIndex == -1)
                    {
                        break;
                    }
                        
                    string key = format.Substring(nextIndex, endIndex - nextIndex + 1);
                    if (FormatKey(key, out string formattedKey))
                    {
                        format = Utils.Replace(format, key, formattedKey, StringComparison.OrdinalIgnoreCase);
                        // index += formattedKey.Length - (key.Length + 2);
                    }
                    else
                    {
                        format = Utils.Replace(format, key, "???", StringComparison.OrdinalIgnoreCase);
                        // index += key.Length - (key.Length + 2);
                    }

                    index = nextIndex;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to format string: " + format);
                Debug.LogException(e);
            }

            return format;
        }

        internal bool FormatKey(string key, out string formattedValue)
        {
            if (TryFormatKeyLocally(key, out formattedValue))
            {
                return true;
            }

            // Check parents' formatting first.
            // eg: Source -> UploadConfig -> UploadTask
            if (m_parent != null)
            {
                return m_parent.FormatKey(key, out formattedValue);
            }

            // Check static commands since there are no parents and its end of the line
            if (FormatToCommand.TryGetValue(key, out Command command) && command.Formatter != null)
            {
                formattedValue = command.Formatter();
                return true;
            }
                
            formattedValue = null;
            return false;
        }

        internal void CacheCallbacks()
        {
            // Use reflection to get all fields of type Func<string> and invoke them to cache their values in a dictionary
            // Yes, this is really gross, but in time a better solution will be made
            foreach (KeyValuePair<string, Command> command in FormatToCommand)
            {
                var func = command.Value.Formatter;
                if (command.Value.CanBeCached && func != null)
                {
                    string value = func();
                    m_cachedValues[command.Key] = value;
                }
            }

            foreach (Command localCommand in m_localCommands)
            {
                var func = localCommand.Formatter;
                if (localCommand.CanBeCached && func != null && !m_cachedValues.ContainsKey(localCommand.Key))
                {
                    string value = func();
                    m_cachedValues[localCommand.Key] = value;
                }
            }
        }

        /// <summary>
        /// Saved with an in-progress task, so a resumed task formats {uploadNumber}, {buildNumber} and the git keys
        /// the same as before the script reload instead of caching new values
        /// </summary>
        internal Dictionary<string, object> GetCachedValues()
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> pair in m_cachedValues)
            {
                values[pair.Key] = pair.Value;
            }

            return values;
        }

        internal void RestoreCachedValues(Dictionary<string, object> values)
        {
            m_cachedValues.Clear();
            foreach (KeyValuePair<string, object> pair in values)
            {
                m_cachedValues[pair.Key] = pair.Value as string;
            }
        }

        public Command AddCommand(string key, Func<string> formatter, string tooltip = "", bool canBeCached = false)
        {
            if (FormatToCommand.TryGetValue(key, out Command staticCommand))
            {
                if (string.IsNullOrEmpty(tooltip))
                {
                    tooltip = staticCommand.Tooltip;
                }
                
                canBeCached = staticCommand.CanBeCached;
            }

            
            Command command = new Command(key, formatter, tooltip, canBeCached);
            m_localCommands.Add(command);
            return command;
        }

        public virtual bool TryFormatKeyLocally(string key, out string value)
        {
            if (m_cachedValues.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (Command command in m_localCommands)
            {
                if (command.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && command.Key.Length > 2 && command.Formatter != null)
                {
                    value = command.Formatter();
                    return true;
                }
            }

            value = "";
            return false;
        }
    }
}