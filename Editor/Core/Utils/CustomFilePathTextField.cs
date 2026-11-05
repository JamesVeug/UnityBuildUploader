using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public static class CustomFilePathTextField
    {
        private static GUIStyle m_pathInputFieldExistsStyle;
        private static GUIStyle m_pathInputFieldDoesNotExistStyle;
        
        static CustomFilePathTextField()
        {
            m_pathInputFieldExistsStyle = new GUIStyle(GUI.skin.textField);
            m_pathInputFieldDoesNotExistStyle = new GUIStyle(GUI.skin.textField);
            m_pathInputFieldDoesNotExistStyle.normal.textColor = Color.yellow;
        }

        public static bool OnGUI(ref string unformattedPath, ref bool showFormatted, Context ctx, string fileTypes="*")
        {
            string formatString = ctx.FormatString(unformattedPath);
            bool error = Utils.PathContainsInvalidCharacters(formatString);
            bool exists = !error && Utils.PathExists(formatString);
            bool isDirty = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (TextFieldStyleScope scope = new TextFieldStyleScope(!exists, error))
                {
                    if (EditorUtils.FormatStringTextField(ref unformattedPath, ref showFormatted, ctx, scope.style))
                    {
                        isDirty = true;
                    }

                    if (GUILayout.Button("...", GUILayout.Width(20)))
                    {
                        string newPath = EditorUtility.OpenFilePanel("Select Folder", unformattedPath, fileTypes);
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            unformattedPath = newPath; // This is an unformatted path
                            isDirty = true;
                        }
                    }

                    if (GUILayout.Button("Show", GUILayout.Width(50)))
                    {
                        EditorUtility.RevealInFinder(ctx.FormatString(unformattedPath));
                    }
                }
            }

            if (isDirty)
            {
                // Make sure there are no invalid characters copied over like double quotes at the start or end
                string directory = "";
                int lastSlash = unformattedPath.LastIndexOfAny(new char[] {'/', '\\'});
                if (lastSlash > 0)
                {
                    directory = unformattedPath.Substring(0, lastSlash);
                    foreach (char invalidChar in Path.GetInvalidPathChars())
                    {
                        directory = directory.Replace(invalidChar.ToString(), "");
                    }
                }
                
                string fileName = lastSlash >= 0 ? unformattedPath.Substring(lastSlash + 1) : unformattedPath;
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(invalidChar.ToString(), "");
                }
                
                unformattedPath = Path.Combine(directory, fileName);
            }
            
            return isDirty;
        }
    }
}