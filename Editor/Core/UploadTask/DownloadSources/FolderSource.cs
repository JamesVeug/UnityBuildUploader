using UnityEditor;

namespace Wireframe
{
    /// <summary>
    /// User is able to select a folder to upload
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(FolderSource), "sources", "Choose a Folder to upload")]
    [UploadSource("Folder", "Choose Folder Upload...", false)]
    public class FolderSource : ABrowsePathSource
    {
        public FolderSource() : base()
        {
            // Required for reflection
        }
        
        public FolderSource(string path) : base(path)
        {
        }

        protected internal override string SelectFile()
        {
            return EditorUtility.OpenFolderPanel("Select Folder to Upload", m_enteredFilePath, "");
        }
    }
}