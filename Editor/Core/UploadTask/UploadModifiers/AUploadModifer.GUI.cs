using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public abstract partial class AUploadModifer
    {
        /// <summary>
        /// Draw the full GUI of the source so everything can be modified
        /// Executed when the Upload Config is expanded
        /// </summary>
        /// <param name="isDirty">Set to true when something is changed to save the config</param>
        /// <param name="ctx">Context for formatting strings such as {version}</param>
        protected internal abstract void OnGUIExpanded(ref bool isDirty, Context ctx);

        /// <summary>
        /// Draw a one-line summary of the modifier's settings, eg: which folders it excludes
        /// Executed when the modifier's section is folded in the thin layout
        /// </summary>
        /// <param name="isDirty">Set to true when something is changed to save the config</param>
        /// <param name="maxWidth">The width the summary can fill</param>
        /// <param name="ctx">Context for formatting strings such as {version}</param>
        protected internal virtual void OnGUICollapsed(ref bool isDirty, float maxWidth, Context ctx)
        {
        }

        /// <summary>
        /// Draws text as a label cut to fit maxWidth, with the full text as its tooltip.
        /// </summary>
        protected static void DrawCollapsedLabel(string text, float maxWidth)
        {
            GUIContent content = new GUIContent(text, text);
            GUILayout.Label(content, EditorStyles.label, GUILayout.MaxWidth(maxWidth), GUILayout.MinWidth(0));
        }

        /// <summary>
        /// Returns a short summary of the different things for this that can be changed
        /// eg: Compress: Compressing 'C:/MyFiles/ThatImportantFile'
        /// Used for {taskStatus}
        /// </summary>
        public abstract string Summary();
    }
}