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

            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
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
