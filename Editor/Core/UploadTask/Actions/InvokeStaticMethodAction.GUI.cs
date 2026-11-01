using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class InvokeStaticMethodAction
    {
        private bool m_showFormattedMethodPath = Preferences.DefaultShowFormattedTextToggle;

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            Resolution resolution = Resolve(m_context.FormatString(m_MethodPath));
            string text;
            if (resolution.IsValid)
            {
                text = resolution.Method.DeclaringType.Name + "." + resolution.Method.Name + "()";
            }
            else if (string.IsNullOrEmpty(m_MethodPath))
            {
                text = "No method set";
            }
            else
            {
                text = m_context.FormatString(m_MethodPath) + "()";
            }

            // Not EditorGUILayout.LabelField, which is never narrower than a label and a field (about 200px) and so
            // doesn't fit beside the action's type in a thin Upload tab
            GUIContent content = new GUIContent(Utils.TruncateText(text, maxWidth, text), text);
            GUILayout.Label(content, EditorStyles.boldLabel, GUILayout.MaxWidth(maxWidth));
        }

        public override void OnGUIExpanded(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Method:", "The type's full name, a dot, then the static method. eg: MyNamespace.MyType.MyMethod" +
                                                             "\nNested types use +. If two assemblies define the type, add the assembly after a comma: MyNamespace.MyType, Assembly-CSharp-Editor.MyMethod");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextField(ref m_MethodPath, ref m_showFormattedMethodPath, m_context);
            }

            Resolution resolution = Resolve(m_context.FormatString(m_MethodPath));
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(124);
                string status = resolution.IsValid
                    ? $"{resolution.Signature} in {resolution.AssemblyName}"
                    : resolution.Error;
                GUILayout.Label(status, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
