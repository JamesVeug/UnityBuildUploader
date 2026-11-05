using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public static class CustomPathButton
    {
        private static readonly GUIStyle pathButtonDoesNotExistStyle;
        private static readonly GUIStyle pathButtonExistsStyle;

        static CustomPathButton()
        {
            pathButtonExistsStyle = new GUIStyle(GUI.skin.button);
            pathButtonExistsStyle.alignment = TextAnchor.MiddleLeft;
            
            pathButtonDoesNotExistStyle = new GUIStyle(GUI.skin.button);
            pathButtonDoesNotExistStyle.normal.textColor = Color.yellow;
            pathButtonDoesNotExistStyle.alignment = TextAnchor.MiddleLeft;
        }
        
        public static bool OnGUI(ref string fullPath, string emptyPathText, float maxWidth)
        {
            string displayedPath = fullPath;
            if (!string.IsNullOrEmpty(displayedPath))
            {
                float characterWidth = 8f;
                int characters = displayedPath.Length;
                float expectedWidth = characterWidth * characters;
                if (expectedWidth >= maxWidth)
                {
                    int charactersToRemove = (int)((expectedWidth - maxWidth) / characterWidth);
                    if (charactersToRemove < displayedPath.Length)
                    {
                        displayedPath = displayedPath.Substring(charactersToRemove);
                    }
                    else
                    {
                        displayedPath = "";
                    }
                }
                
                if(displayedPath.Length < fullPath.Length)
                {
                    displayedPath = "..." + displayedPath;
                }
            }
            else
            {
                displayedPath = emptyPathText;
            }
            
            bool warning = !Utils.PathExists(displayedPath);
            bool error = string.IsNullOrEmpty(displayedPath) || Utils.PathContainsInvalidCharacters(displayedPath);
            using(ButtonStyleScope scope = new ButtonStyleScope(warning, error)){
                GUIContent content = new GUIContent(displayedPath);
                Rect rect = GUILayoutUtility.GetRect(content, scope.style);
                Event current = Event.current;
                if (rect.Contains(current.mousePosition))
                {
                    if (current.type == EventType.MouseDown && current.button == 1)
                    {
                        ShowContextMenu(fullPath);
                        current.Use();
                    }
                    else if ((current.type == EventType.MouseUp && current.button == 1) || current.type == EventType.ContextClick)
                    {
                        current.Use();
                    }
                }

                if (GUI.Button(rect, content, scope.style))
                {
                    string newPath = EditorUtility.OpenFolderPanel("Select Folder", fullPath, "");
                    if (string.IsNullOrEmpty(newPath))
                    {
                        return false;
                    }

                    fullPath = newPath;
                    return true;
                }
            }

            return false;
        }

        private static void ShowContextMenu(string fullPath)
        {
            GenericMenu menu = new GenericMenu();
            GUIContent openLabel = new GUIContent(OpenInFileBrowserLabel());
            if (Utils.GetClosestExistingDirectory(fullPath) != null)
            {
                menu.AddItem(openLabel, false, () => Utils.OpenClosestExistingDirectory(fullPath));
            }
            else
            {
                menu.AddDisabledItem(openLabel);
            }

            menu.ShowAsContext();
        }

        private static string OpenInFileBrowserLabel()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return "Open in Finder";
                case RuntimePlatform.LinuxEditor:
                    return "Open in File Manager";
                default:
                    return "Open in Explorer";
            }
        }
    }
}