using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public abstract partial class ABuildSource<T>
    {
        /// <summary>
        /// Draws Build Addressables and Clean Addressables. Without the Addressables integration they are shown
        /// disabled, keeping their values so the saved profile is unchanged.
        /// </summary>
        protected void DrawAddressablesToggles(ref bool isDirty)
        {
            bool available = AddressablesSupport.IsAvailable;
            string requirement = available ? "" : "\n\n" + AddressablesSupport.Requirement;
            using (new EditorGUI.DisabledScope(!available))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent label = new GUIContent("Build Addressables:", "If enabled, Addressables content is built as part of this player build, " +
                                                                             "for the platform being built, and included in the player. " +
                                                                             "Use this when Addressables is set to not build with the player." + requirement);
                    GUILayout.Label(label, GUILayout.Width(120));
                    isDirty |= CustomToggle.DrawToggle(ref m_BuildAddressables, GUILayout.Width(20));
                }

                using (new EditorGUI.DisabledScope(!m_BuildAddressables))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUIContent label = new GUIContent("Clean Addressables:", "If enabled, previously built Addressables content is deleted " +
                                                                                 "before the build so it is rebuilt from scratch." + requirement);
                        GUILayout.Label(label, GUILayout.Width(120));
                        isDirty |= CustomToggle.DrawToggle(ref m_CleanAddressables, GUILayout.Width(20));
                    }
                }
            }
        }

        /// <summary>
        /// Short note for the collapsed view when Build Addressables is enabled.
        /// </summary>
        protected void DrawAddressablesCollapsed()
        {
            if (m_BuildAddressables)
            {
                GUILayout.Label(new GUIContent("+ Addressables", "Builds Addressables content with the player"), GUILayout.ExpandWidth(false));
            }
        }
    }
}
