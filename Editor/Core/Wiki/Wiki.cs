#if BUILD_UPLOADER_WIKI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace Wireframe
{
    internal class Wiki
    {
        /// <summary>Optional integrations append their documentation during wiki export.</summary>
        internal static event Action ExportAdditionalPages;

        private class Data
        {
            public Type DataClass;
            public string MDFilePath; // Path to the markdown file in your unity project (same level as Assets)
            public string StartOfHeader; // Search for this to start inserting the generated text
            public string WikiSubPath; // Url where this goes to
            public List<Type> Types = new List<Type>();
        }
        
        [MenuItem("Tools/Build Uploader/Open Wiki Export Folder")]
        public static void OpenWikiExportFolder()
        {
            string wikiPath = Path.Combine(Application.dataPath, "../Wiki");
            if (!Directory.Exists(wikiPath))
            {
                Directory.CreateDirectory(wikiPath);
            }
            
            EditorUtility.RevealInFinder(wikiPath);
        }
        
        [MenuItem("Tools/Build Uploader/Export Wiki Data")]
        public static void ExportWikiData()
        {
            List<Data> allData = new List<Data>();
            allData.Add(new Data()
            {
                DataClass = typeof(UploadConfig.SourceData),
                MDFilePath = Path.Combine(Application.dataPath, "../Wiki/Sources.md"),
                StartOfHeader = "## Sources",
                WikiSubPath = "sources",
            });
            allData.Add(new Data()
            {
                DataClass = typeof(UploadConfig.ModifierData),
                MDFilePath = Path.Combine(Application.dataPath, "../Wiki/Modifiers.md"),
                StartOfHeader = "## Modifiers",
                WikiSubPath = "modifiers",
            });
            allData.Add(new Data()
            {
                DataClass = typeof(UploadConfig.DestinationData),
                MDFilePath = Path.Combine(Application.dataPath, "../Wiki/Destinations.md"),
                StartOfHeader = "## Destinations",
                WikiSubPath = "destinations",
            });
            allData.Add(new Data()
            {
                DataClass = typeof(UploadConfig.UploadActionData),
                MDFilePath = Path.Combine(Application.dataPath, "../Wiki/Actions.md"),
                StartOfHeader = "## Actions",
                WikiSubPath = "actions",
            });
            
            
            // Get every type matching the WikiSubPath
            List<Type> types = GetAllWikiTypes();
            types.Sort(SortTypesByWikiAttribute);
            
            foreach (var type in types)
            {
                var wikiAttribute = (WikiAttribute)Attribute.GetCustomAttribute(type, typeof(WikiAttribute));
                Data d = allData.FirstOrDefault(a => a.WikiSubPath == wikiAttribute.SubPath);
                if (d == null)
                {
                    if (!string.IsNullOrEmpty(wikiAttribute.SubPath))
                    {
                        Debug.LogErrorFormat("Could not find data for type {0} and path: {1}", type, wikiAttribute.SubPath);
                    }

                    continue;
                }
                
                d.Types.Add(type);
                Debug.Log($"Type: {type.Name}, Wiki Link: {wikiAttribute.Text}");
            }

            // Write each data type (source,modifier,destination,actions)
            foreach (Data data in allData)
            {
                string mdFilePath = data.MDFilePath;
                if (!TryGetHandAuthoredPreamble(mdFilePath, data.StartOfHeader, "TODO", out string preamble))
                {
                    continue;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(preamble);
                sb.AppendLine();


                WikiAttribute dataWikiAttribute = (WikiAttribute)data.DataClass.GetCustomAttribute(typeof(WikiAttribute));
                sb.AppendLine($"## {dataWikiAttribute.Name}");
                if(!string.IsNullOrEmpty(dataWikiAttribute.Text))sb.AppendLine($"{dataWikiAttribute.Text}");
                WriteFields(data.DataClass, sb, 1);
                sb.AppendLine();
                

                foreach (Type type in data.Types)
                {
                    WriteTypeData(type, sb, 3);
                }
                
                File.WriteAllText(mdFilePath, sb.ToString());
            }
            
            
            // Write the String Formatter commands
            StringBuilder stringFormatWikiBuilder = new StringBuilder();
            stringFormatWikiBuilder.AppendLine("## String Formatter Commands");
            stringFormatWikiBuilder.AppendLine("The String Formatter is used to format strings in the build task. It supports commands that can be used to insert values into the string.");
            stringFormatWikiBuilder.AppendLine();
            stringFormatWikiBuilder.AppendLine("The following commands are available:");
            foreach (Command command in Context.FormatToCommand.Values.OrderBy(a=>a.Key))
            {
                stringFormatWikiBuilder.AppendLine($"- **{command.Key}**: {command.Tooltip}");
            }
            
            string filePath = Path.Combine(Application.dataPath, "../Wiki/StringFormatter.md");
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }

            File.WriteAllText(filePath, stringFormatWikiBuilder.ToString());

            ExportAdditionalPages?.Invoke();
        }



        /// <summary>
        /// Everything above the generated header is hand-authored and kept as-is. Creates the file seeded
        /// with the default preamble when it is missing, and returns false - leaving the file untouched
        /// rather than emptying it - when the header cannot be found.
        /// </summary>
        private static bool TryGetHandAuthoredPreamble(string filePath, string header, string defaultPreamble, out string preamble)
        {
            preamble = null;

            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, defaultPreamble + "\n\n" + header + "\n\n");
            }

            string text = File.ReadAllText(filePath);
            int startIndex = text.IndexOf(header, StringComparison.Ordinal);
            if (startIndex == -1)
            {
                Debug.LogError($"Could not find header: {header} in {filePath}");
                return false;
            }

            // Trim the blank lines before the header - the generated text puts its own back, and keeping
            // them would grow the gap by two lines on every export.
            while (startIndex > 0 && (text[startIndex - 1] == '\n' || text[startIndex - 1] == '\r'))
            {
                startIndex--;
            }

            preamble = text.Substring(0, startIndex);
            return true;
        }

        /// <summary>
        /// Every [Wiki] type in the project, not just this assembly's - services with their own asmdef
        /// (Apple, for one) would otherwise be missing from the exported pages.
        /// </summary>
        private static List<Type> GetAllWikiTypes()
        {
            List<Type> types = new List<Type>();
            foreach (Assembly assembly in AssemblyUtils.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    // An assembly we cannot fully load still tells us about the types that did load.
                    assemblyTypes = e.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in assemblyTypes)
                {
                    if (type.IsDefined(typeof(WikiAttribute)))
                    {
                        types.Add(type);
                    }
                }
            }

            return types;
        }

        private static int SortTypesByWikiAttribute(Type a, Type b)
        {
            WikiAttribute aW = (WikiAttribute)a.GetCustomAttribute(typeof(WikiAttribute));
            WikiAttribute bW = (WikiAttribute)b.GetCustomAttribute(typeof(WikiAttribute));
            if (aW.Order != bW.Order)
            {
                return aW.Order - bW.Order;
            }

            return string.Compare(aW.Name, bW.Name, StringComparison.Ordinal);
        }

        private static void WriteTypeData(Type type, StringBuilder sb, int headerIndent)
        {
            var wikiAttribute = (WikiAttribute)Attribute.GetCustomAttribute(type, typeof(WikiAttribute));
            sb.AppendLine(new string('#', headerIndent) + " " + wikiAttribute.Name);
            sb.AppendLine(wikiAttribute.Text);
            
            WriteFields(type, sb, 0);
            sb.AppendLine();
        }

        private static void WriteFields(Type type, StringBuilder sb, int headerIndent)
        {
            var fields = ReflectionUtils.GetAllFields(type)
                .Where(a=> a.IsDefined(typeof(WikiAttribute)))
                .OrderBy(a=>a.Name)
                .ToList();
            fields.Sort((a,b)=>
            {
                WikiAttribute aW = (WikiAttribute)a.GetCustomAttribute(typeof(WikiAttribute));
                WikiAttribute bW = (WikiAttribute)b.GetCustomAttribute(typeof(WikiAttribute));
                if (aW.Order != bW.Order)
                {
                    return aW.Order - bW.Order;
                }

                return string.Compare(aW.Name, bW.Name, StringComparison.Ordinal);
            });

            foreach (FieldInfo field in fields)
            {
                WikiAttribute wikiAttribute = field.GetCustomAttribute<WikiAttribute>();
                string indent = new string(' ', headerIndent*2);
                sb.AppendLine($"{indent}- **{wikiAttribute.Name}**: {wikiAttribute.Text}");
                if (field.FieldType.IsEnum && (!field.TryGetCustomAttribute(out WikiEnumAttribute we) || we.ListEnumValues))
                {
                    foreach (object e in Enum.GetValues(field.FieldType))
                    {
                        WikiAttribute enumWikiAttribute = ((Enum)e).GetAttributeOfType<WikiAttribute>();
                        string enumName = e.ToString();
                        if (enumWikiAttribute != null)
                        {
                            enumName = $"{enumWikiAttribute.Name}: {enumWikiAttribute.Text}";
                        }
                        sb.AppendLine($"  - {enumName}");
                    }
                }
                else if (field.FieldType.GetCustomAttribute(typeof(WikiAttribute)) != null)
                {
                    WriteFields(field.FieldType, sb, headerIndent + 1);
                }
                else if (field.FieldType.GenericTypeArguments.Length > 0 && field.FieldType.GenericTypeArguments[0].GetCustomAttribute(typeof(WikiAttribute)) != null)
                {
                    WriteFields(field.FieldType.GenericTypeArguments[0], sb, headerIndent + 1);
                }
            }
        }
    }
}
#endif
