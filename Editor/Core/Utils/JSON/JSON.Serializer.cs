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
        private class JSONSerializer
        {
            public static string TOJSON(object o, Type type, int indents = 0)
            {
                if (o == null)
                {
                    return "null";
                }
                else if (type == typeof(object))
                {
                    return TOJSON(o, o.GetType(), indents);
                }
                
                if (type == typeof(string) || type == typeof(char))
                {
                    return QuoteString(o.ToString());
                }
                
                if (type.IsPrimitive)
                {
                    return Convert.ToString(o, CultureInfo.InvariantCulture).ToLowerInvariant();
                }
                else if (type == typeof(decimal))
                {
                    return o.ToString();
                }

                if (type.IsEnum)
                {
                    // Unboxing as int throws for enums backed by byte, long etc. (UploadTrigger is : byte)
                    object number = Convert.ChangeType(o, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
                    return Convert.ToString(number, CultureInfo.InvariantCulture);
                }
                
                // List
                if(o is IList list)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[");
                    if (list.Count > 0)
                    {
                        indents++;
                        sb.Append("\n");
                        sb.Append(new string('\t', indents));

                        for (var i = 0; i < list.Count; i++)
                        {
                            var item = list[i];
                            if (item == null)
                            {
                                sb.Append("null");
                            }
                            else
                            {
                                sb.Append(TOJSON(item, item.GetType(), indents));
                            }
                            
                            if (i < list.Count - 1)
                            {
                                sb.Append(",");
                                sb.Append("\n");
                                sb.Append(new string('\t', indents));
                            }
                        }

                        indents--;
                        sb.Append("\n");
                        sb.Append(new string('\t', indents));
                    }
                    sb.Append("]");
                    return sb.ToString();
                }
                
                // Dictionary
                if(o is IDictionary dict)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("{");
                    if (dict.Count > 0)
                    {
                        indents++;
                        sb.Append("\n");
                        sb.Append(new string('\t', indents));
                        int count = dict.Count;
                        foreach (DictionaryEntry entry in dict)
                        {
                            sb.Append(TOJSON(entry.Key, typeof(string), indents));
                            sb.Append(": ");
                            sb.Append(TOJSON(entry.Value, entry.Value?.GetType(), indents));
                            if (--count > 0)
                            {
                                sb.Append(",");
                                sb.Append("\n");
                                sb.Append(new string('\t', indents));
                            }
                        }
                        sb.Append("\n");
                        sb.Append(new string('\t', Mathf.Max(0, indents - 1)));
                        indents--;
                    }
                    sb.Append("}");
                    return sb.ToString();
                }

                if (type.IsClass)
                {
                    indents++;
                    
                    // Get all serializable fields
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    Dictionary<string, string> dataDict = new Dictionary<string, string>();
                    foreach (FieldInfo field in fields)
                    {
                        string fieldName = field.Name;
                        object fieldValue = field.GetValue(o);
                        string serializedField = TOJSON(fieldValue, field.FieldType, indents);
                        dataDict.Add(fieldName, serializedField);
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.Append("{");
                    sb.Append("\n");
                    sb.Append(new string('\t', indents));
                    int count = dataDict.Count;
                    foreach (KeyValuePair<string, string> entry in dataDict)
                    {
                        sb.Append("\"" + entry.Key + "\": ");
                        sb.Append(entry.Value);
                        if (--count > 0)
                        {
                            sb.Append(",");
                            sb.Append("\n");
                            sb.Append(new string('\t', indents));
                        }
                    }
                    indents--;
                    sb.Append("\n");
                    sb.Append(new string('\t', indents));
                    sb.Append("}");
                    return sb.ToString();
                }
                
                return null;
            }

            private static string QuoteString(string s)
            {
                StringBuilder sb = new StringBuilder(s.Length + 2);
                sb.Append('"');
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (c < ' ')
                            {
                                // Backspace and form feed go here too: the deserializer reads a bare \b or \f
                                // as an unescaped backslash from an older file, such as C:\builds.
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
                sb.Append('"');
                return sb.ToString();
            }
        }
    }
}