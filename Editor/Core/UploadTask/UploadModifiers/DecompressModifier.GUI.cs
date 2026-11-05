using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class DecompressModifier
    {
        private bool m_showFormattedFilePath;
        private bool m_showFormattedTargetPathToCompress;
        
        protected internal override void OnGUIExpanded(ref bool isDirty, Context ctx)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Decompression Type", GUILayout.Width(120));
                var newCompressionType = (DecompressionType)EditorGUILayout.EnumPopup(m_decompressionType);
                if (m_decompressionType != newCompressionType)
                {
                    m_decompressionType = newCompressionType;
                    isDirty = true;
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("File Path", GUILayout.Width(120));
                if (EditorUtils.FormatStringTextField(ref m_filePath, ref m_showFormattedFilePath, ctx))
                {
                    isDirty = true;
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Target Path", GUILayout.Width(120));
                if (EditorUtils.FormatStringTextField(ref m_targetPathToCompress, ref m_showFormattedTargetPathToCompress, ctx))
                {
                    isDirty = true;
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Remove old file", GUILayout.Width(120));
                var newRemoveContent = EditorGUILayout.Toggle(m_removeCompressedFile, GUILayout.Width(20));
                if (m_removeCompressedFile != newRemoveContent)
                {
                    m_removeCompressedFile = newRemoveContent;
                    isDirty = true;
                }
            }
        }

        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth, Context ctx)
        {
            DrawCollapsedLabel($"{m_decompressionType}: '{ctx.FormatString(m_filePath)}' → '{ctx.FormatString(m_targetPathToCompress)}'", maxWidth);
        }

        public override string Summary()
        {
            return $"Decompressing '{m_targetPathToCompress}'";
        }
    }
}