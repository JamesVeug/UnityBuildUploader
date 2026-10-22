using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class BuildAddressablesAction
    {
        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            string text = $"Build Addressables for {EditorUserBuildSettings.activeBuildTarget}";
            if (m_Clean)
            {
                text += " (clean)";
            }

            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        public override void OnGUIExpanded(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Clean:", "If enabled, previously built Addressables content is deleted first so the content is rebuilt from scratch.");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_Clean);
            }

            EditorGUILayout.HelpBox($"Builds Addressables content for the active build target ({EditorUserBuildSettings.activeBuildTarget}), " +
                                    "because actions run before any source switches platform.\n" +
                                    "To include Addressables content in a player, enable Build Addressables on the Build Config or Build Profile source instead.",
                MessageType.Info);
        }
    }
}
