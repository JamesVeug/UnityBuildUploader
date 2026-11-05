using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// The format string dropdown edits the text field through EditorGUI's recycled text editor, which
    /// it reaches by reflection.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class FormatStringAutocompleteTests
    {
        [Test]
        public void FindsEditorGUIsRecycledTextEditor()
        {
            Assert.IsNotNull(FormatStringAutocompleteTextArea.GetActiveTextEditor(),
                "EditorGUI.s_RecycledEditor was not found, so picking a suggestion can't move the cursor after it");
        }
    }
}
