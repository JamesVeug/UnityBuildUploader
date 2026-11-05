using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public static class CustomFolderPathTextField
    {
        private static GUIStyle m_pathInputFieldExistsStyle;
        private static GUIStyle m_pathInputFieldDoesNotExistStyle;
        
        static CustomFolderPathTextField()
        {
            m_pathInputFieldExistsStyle = new GUIStyle(GUI.skin.textField);
            m_pathInputFieldDoesNotExistStyle = new GUIStyle(GUI.skin.textField);
            m_pathInputFieldDoesNotExistStyle.normal.textColor = Color.yellow;
        }

        public static bool OnGUI(string title, ref string unformattedPath, ref bool showFormatted, Context ctx)
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
                        string newPath = EditorUtility.OpenFolderPanel(title, unformattedPath, "");
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            unformattedPath = newPath; // This is an unformatted path
                            isDirty = true;
                        }
                    }

                    if (GUILayout.Button("Show", GUILayout.Width(50)))
                    {
                        Utils.OpenClosestExistingDirectory(ctx.FormatString(unformattedPath));
                    }
                }
            }
            
            return isDirty;
        }

        public static bool OnGUI(string title, ref string typedPath, string subPathString = null)
        {
            bool error = Utils.PathContainsInvalidCharacters(typedPath);
            
            string fullPath = string.IsNullOrEmpty(subPathString) ?  typedPath : Path.Combine(subPathString, typedPath);
            bool exists = !error && Utils.PathExists(fullPath);
            bool isDirty = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (TextFieldStyleScope scope = new TextFieldStyleScope(!exists, error))
                {
                    string newFormatString = GUILayout.TextField(typedPath, scope.style);
                    if (newFormatString != typedPath)
                    {
                        typedPath = newFormatString;
                        isDirty = true;
                    }

                    if (GUILayout.Button("...", GUILayout.Width(20)))
                    {
                        string newPath = EditorUtility.OpenFolderPanel(title, typedPath, "");
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            typedPath = newPath; // This is an unformatted path
                            isDirty = true;
                        }
                    }

                    if (GUILayout.Button("Show", GUILayout.Width(50)))
                    {
                        Utils.OpenClosestExistingDirectory(fullPath);
                    }
                }
            }
            
            return isDirty;
        }
    }
}