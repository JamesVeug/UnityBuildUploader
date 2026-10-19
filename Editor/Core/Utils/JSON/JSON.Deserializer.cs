using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Wireframe
{
    public static partial class JSON
    {
        public class JSONDeserializer
        {
            public static T FromJSON<T>(string json)
            {
                return (T)FromJSON(json, typeof(T));
            }

            public static object FromJSON(string json, Type type)
            {
                if (json == null)
                {
                    return null;
                }
                
                if (type == typeof(string))
                {
                    if (json.Length > 1 && json[0] == '"' && json[json.Length - 1] == '"')
                    {
                        json = json.Substring(1, json.Length - 2);
                    }
                    return Unescape(json);
                }

                if (type.IsPrimitive)
                {
                    if (type == typeof(bool))
                    {
                        return bool.Parse(json);
                    }

                    if (type == typeof(int))
                    {
                        return int.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(float))
                    {
                        return float.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(double))
                    {
                        return double.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(long))
                    {
                        return long.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(short))
                    {
                        return short.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(byte))
                    {
                        return byte.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(char))
                    {
                        return char.Parse((string)FromJSON(json, typeof(string))); // "a" -> a, "\\" -> \
                    }

                    if (type == typeof(decimal))
                    {
                        return decimal.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(uint))
                    {
                        return uint.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(ulong))
                    {
                        return ulong.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(ushort))
                    {
                        return ushort.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(sbyte))
                    {
                        return sbyte.Parse(json, CultureInfo.InvariantCulture);
                    }
                }
                
                if(type.IsEnum)
                {
                    if(json.Length > 2 && json[0] == '"' && json[json.Length - 1] == '"')
                        return Enum.Parse(type, json.Substring(1, json.Length - 2));
                    return Enum.Parse(type, json);
                }

                if (type == typeof(object))
                {
                    // Try convert to primitive because we don't know the type
                    if (bool.TryParse(json, out bool boolValue))
                    {
                        return boolValue;
                    }

                    if (long.TryParse(json, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intValue))
                    {
                        return intValue;
                    }

                    if (float.TryParse(json, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                    {
                        return floatValue;
                    }

                    if (json.Length > 1 && json[0] == '"' && json[json.Length - 1] == '"')
                    {
                        return FromJSON(json, typeof(string));
                    }

                    if (json == "null" || json == "")
                    {
                        return null;
                    }

                    if (json[0] == '{')
                    {
                        return FromJSON<Dictionary<string, object>>(json);
                    }

                    if (json[0] == '[')
                    {
                        return FromJSON<List<object>>(json);
                    }
                }

                if (type.IsClass)
                {
                    if (type == typeof(string))
                    {
                        return json;
                    }

                    if (type == typeof(Uri))
                    {
                        return new Uri(json);
                    }

                    if (type == typeof(Version))
                    {
                        return new Version(json);
                    }

                    if (type == typeof(byte[]))
                    {
                        return Convert.FromBase64String(json);
                    }

                    // List
                    if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
                    {
                        int startIndex = json.IndexOf("[") + 1;
                        int endIndex = json.LastIndexOf("]");

                        // "value",

                        List<string> listData = new List<string>();
                        int entryStart = startIndex;
                        int entryEnd = startIndex;
                        int depth = 0;
                        bool inString = false;
                        for (int i = startIndex; i < endIndex; i++)
                        {
                            char c = json[i];
                            if (c == '\\')
                            {
                                i++;
                                continue;
                            }

                            if (inString)
                            {
                                if (c == '"')
                                {
                                    inString = false;
                                }
                                continue;
                            }
                            else if (c == '"')
                            {
                                inString = true;
                                continue;
                            }
                            
                            if (c == '{' || c == '[')
                            {
                                depth++;
                            }

                            if (c == '}' || c == ']')
                            {
                                depth--;
                            }

                            if (c == '"')
                            {
                                inString = true;
                            }
                            
                            if (c == ',' && depth == 0)
                            {
                                entryEnd = i;
                                listData.Add(json.Substring(entryStart, entryEnd - entryStart).Trim());
                                entryStart = i + 1;
                            }
                        }

                        listData.Add(json.Substring(entryStart, endIndex - entryStart).Trim());
                        listData.RemoveAll(string.IsNullOrEmpty);

                        if (type.IsArray)
                        {
                            Array list = (Array)Activator.CreateInstance(type, listData.Count);
                            Type listType = type.GetElementType();
                            for (int i = 0; i < listData.Count; i++)
                            {
                                string item = listData[i];
                                list.SetValue(FromJSON(item, listType), i);
                            }
                            return list;
                        }
                        else
                        {
                            Type listType = type.GetGenericArguments()[0];
                            IList list = (IList)Activator.CreateInstance(type);
                            foreach (string item in listData)
                            {
                                list.Add(FromJSON(item, listType));
                            }
                            return list;
                        }
                    }

                    // Dictionary
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        int startIndex = json.IndexOf("{") + 1;
                        int endIndex = FindClosingBracket(json, startIndex - 1);

                        List<string> entries = new List<string>();
                        int entryStart = startIndex;
                        int entryEnd = startIndex;
                        int depth = 0;
                        bool inString = false;
                        for (int i = startIndex; i < endIndex; i++)
                        {
                            char c = json[i];
                            if (c == '\\')
                            {
                                i++;
                                continue;
                            }

                            if (inString)
                            {
                                if (c == '"')
                                {
                                    inString = false;
                                }
                                continue;
                            }
                            else if (c == '"')
                            {
                                inString = true;
                                continue;
                            }

                            if (c == '{' || c == '[')
                            {
                                depth++;
                            }

                            if (c == '}' || c == ']')
                            {
                                depth--;
                            }

                            if (c == ',' && depth == 0)
                            {
                                entryEnd = i;
                                entries.Add(json.Substring(entryStart, entryEnd - entryStart).Trim());
                                entryStart = i + 1;
                            }
                        }

                        if (entryStart == 1 && endIndex == 0)
                        {
                            // {}
                        }
                        else
                        {
                            entries.Add(json.Substring(entryStart, endIndex - entryStart + 1).Trim());
                        }
                        entries.RemoveAll(string.IsNullOrEmpty);


                        Type keyType = type.GetGenericArguments()[0];
                        Type valueType = type.GetGenericArguments()[1];
                        IDictionary dict = (IDictionary)Activator.CreateInstance(type);

                        foreach (string entry in entries)
                        {
                            int colonIndex = entry.IndexOf(":");
                            int nextDoubleQuoteIndex = entry.IndexOf("\"", 1);
                            if (nextDoubleQuoteIndex > colonIndex)
                            {
                                colonIndex = entry.IndexOf(":", nextDoubleQuoteIndex);
                            }
                            
                            string key = entry.Substring(1, colonIndex - 1).Trim();
                            key = key.Substring(0, key.Length - 1);
                            
                            object parsedKey = FromJSON(key, keyType);
                            string value = entry.Substring(colonIndex + 1).Trim();
                            object parsedValue = FromJSON(value, valueType);
                            dict.Add(parsedKey, parsedValue);
                        }

                        return dict;
                    }

                    // Class
                    Dictionary<string, object> dataDict = FromJSON<Dictionary<string, object>>(json);
                    object convertedValue = ConvertType(dataDict, type);
                    return convertedValue;
                }
                else
                {
                    if (type == typeof(DateTime))
                    {
                        return DateTime.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(DateTimeOffset))
                    {
                        return DateTimeOffset.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(TimeSpan))
                    {
                        return TimeSpan.Parse(json, CultureInfo.InvariantCulture);
                    }

                    if (type == typeof(Guid))
                    {
                        return Guid.Parse(json);
                    }
                }

                Debug.LogError("Type not supported: " + type.Name);
                return null;
            }

            /// <summary>
            /// Decodes the escapes in a string's contents. Before backslashes were escaped, the serializer
            /// wrote paths raw ("C:\Builds\new_build"), and those files are still on disk. A string holding an
            /// escape the serializer never writes is one of them: its backslashes stay as they are and only
            /// \" is decoded. \b and \f count, because the serializer writes those characters as \u0008 and \u000c.
            /// </summary>
            private static string Unescape(string text)
            {
                if (text.IndexOf('\\') < 0)
                {
                    return text;
                }

                bool unescapedBackslashes = HasUnwrittenEscape(text);
                StringBuilder sb = new StringBuilder(text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c != '\\' || i == text.Length - 1)
                    {
                        sb.Append(c);
                        continue;
                    }

                    char next = text[i + 1];
                    if (unescapedBackslashes)
                    {
                        if (next == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        continue;
                    }

                    i++;
                    switch (next)
                    {
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            sb.Append((char)int.Parse(text.Substring(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default:
                            sb.Append(next); // \" \\ \/
                            break;
                    }
                }

                return sb.ToString();
            }

            private static bool HasUnwrittenEscape(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] != '\\')
                    {
                        continue;
                    }

                    if (i == text.Length - 1)
                    {
                        return true;
                    }

                    char next = text[i + 1];
                    if (next == 'u' && IsHex(text, i + 2, 4))
                    {
                        i += 5;
                    }
                    else if (next == '"' || next == '\\' || next == '/' || next == 'n' || next == 'r' || next == 't')
                    {
                        i++;
                    }
                    else
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsHex(string text, int start, int count)
            {
                if (start + count > text.Length)
                {
                    return false;
                }

                for (int i = start; i < start + count; i++)
                {
                    if (!Uri.IsHexDigit(text[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static int FindClosingBracket(string text, int startIndexInclusive)
            {
                int depth = 1;
                bool inString = false;

                for (int i = startIndexInclusive + 1; i < text.Length; i++)
                {
                    char c = text[i];

                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (inString)
                    {
                        if (c == '"')
                        {
                            inString = false;
                        }

                        continue;
                    }

                    switch (c)
                    {
                        case '"':
                            inString = true;
                            break;

                        case '{':
                        case '[':
                            depth++;
                            break;

                        case '}':
                        case ']':
                            depth--;
                            if (depth == 0)
                            {
                                return i - 1;
                            }
                            break;
                    }
                }

                return -1;
            }

            public static T ConvertType<T>(object data)
            {
                return (T)ConvertType(data, typeof(T));
            }

            public static object ConvertType(object obj, Type type)
            {
                if (obj == null)
                {
                    return null;
                }

                if (obj.GetType() == type)
                {
                    return obj;
                }

                if (type.IsPrimitive)
                {
                    if (type == typeof(bool))
                    {
                        return bool.Parse(obj.ToString());
                    }

                    if (type == typeof(int))
                    {
                        return int.Parse(obj.ToString());
                    }

                    if (type == typeof(float))
                    {
                        return float.Parse(obj.ToString());
                    }

                    if (type == typeof(double))
                    {
                        return double.Parse(obj.ToString());
                    }

                    if (type == typeof(long))
                    {
                        return long.Parse(obj.ToString());
                    }

                    if (type == typeof(short))
                    {
                        return short.Parse(obj.ToString());
                    }

                    if (type == typeof(byte))
                    {
                        return byte.Parse(obj.ToString());
                    }

                    if (type == typeof(char))
                    {
                        return char.Parse(obj.ToString());
                    }

                    if (type == typeof(decimal))
                    {
                        return decimal.Parse(obj.ToString());
                    }

                    if (type == typeof(uint))
                    {
                        return uint.Parse(obj.ToString());
                    }

                    if (type == typeof(ulong))
                    {
                        return ulong.Parse(obj.ToString());
                    }

                    if (type == typeof(ushort))
                    {
                        return ushort.Parse(obj.ToString());
                    }

                    if (type == typeof(sbyte))
                    {
                        return sbyte.Parse(obj.ToString());
                    }

                    Debug.LogError("Primitive Type not supported: " + type.Name);
                    return null;
                }

                if (type.IsEnum)
                {
                    return Enum.Parse(type, obj.ToString());
                }

                // List
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    IList convertedList = (IList)Activator.CreateInstance(type);
                    Type genericArgument = type.GetGenericArguments()[0];
                    foreach (object item in (List<object>)obj)
                    {
                        convertedList.Add(ConvertType(item, genericArgument));
                    }

                    return convertedList;
                }
                
                // Array
                if (type.IsArray)
                {
                    List<object> objects = (List<object>)obj;
                    Array convertedList = (Array)Activator.CreateInstance(type, objects.Count);
                    Type genericArgument = type.GetElementType();
                    
                    for (int i = 0; i < objects.Count; i++)
                    {
                        object item = objects[i];
                        convertedList.SetValue(ConvertType(item, genericArgument), i);
                    }

                    return convertedList;
                }

                // Dictionary
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    IDictionary convertedDict = (IDictionary)Activator.CreateInstance(type);
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    foreach (KeyValuePair<string, object> entry in (Dictionary<string, object>)obj)
                    {
                        convertedDict.Add(ConvertType(entry.Key, keyType), ConvertType(entry.Value, valueType));
                    }

                    return convertedDict;
                }

                // Class
                if (type.IsClass && obj is Dictionary<string, object> dataDict)
                {
                    object instance = Activator.CreateInstance(type);
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    foreach (FieldInfo field in fields)
                    {
                        if(dataDict.TryGetValue(field.Name, out object fieldValue))
                        {
                            field.SetValue(instance, ConvertType(fieldValue, field.FieldType));
                        }
                    }

                    return instance;
                }

                Debug.LogError("Type not supported: " + type.Name);
                return null;
            }
        }
    }
}