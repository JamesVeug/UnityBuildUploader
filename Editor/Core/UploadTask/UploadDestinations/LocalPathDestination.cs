using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Move the modified build to a local path on the users computer
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki("LocalPathDestination", "destinations", "Copy the build to a location on your local pc.")]
    [UploadDestination("Local Path")]
    public partial class LocalPathDestination : AUploadDestination
    {
        public bool IsZippingContents => m_zipContent;

        /// <summary>
        /// Only copies to this machine, so a dry run runs it as normal
        /// </summary>
        public override bool SkippedDuringDryRun => false;

        [Wiki("Directory", "The absolute path of the folder to copy the files to. eg: C:/MyBuilds/TodaysBuild" +
                           "\n\nSee docs for formatting options like {version} and {time} to use in the path.")]
        private string m_localPath = "";
        
        [Wiki("Duplicate Files", "When copying files over and there already being the same file, what should we do with the new file?")]
        private Utils.FileExistHandling m_duplicateFileHandling = Utils.FileExistHandling.Overwrite;
        
        [Wiki("Zip Content", "If true, the content will be zipped into a single file.")]
        private bool m_zipContent;
        
        [Wiki("Name", "If Zip Content is true, This is the name of the zipped file.")]
        private string m_zippedFilesName = "";

        public LocalPathDestination() : base()
        {
            // Required for reflection
        }
        
        public LocalPathDestination(string localPath, Utils.FileExistHandling duplicateFileHandling = Utils.FileExistHandling.Overwrite) : base()
        {
            m_localPath = localPath;
            m_duplicateFileHandling = duplicateFileHandling;
        }

        public void ZipContents(string zippedFileName)
        {
            m_zipContent = true;
            m_zippedFilesName = zippedFileName;
        }

        public string FullPath()
        {
            string path = m_context.FormatString(m_localPath);
            if (m_zipContent)
            {
                path += m_context.FormatString(m_zippedFilesName) + ".zip";
            }
            
            return path;
        }

        private bool SetNewPath(string newPath)
        {
            if (newPath == m_localPath || string.IsNullOrEmpty(newPath))
            {
                return false;
            }
            
            m_localPath = newPath;
            return true;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            string fullPath = FullPath();
            if (Preferences.UseLocalDestinationIfAvailable && Utils.ComparePaths(m_taskContentsFolder, fullPath))
            {
                result.AddLog($"Skipping coping LocalDestination because 'Save contents to LocalDestination if valid' is on and its the same path that we got.");
                return true;
            }
            
            string directory = m_zipContent ? Path.GetDirectoryName(fullPath) : fullPath;

            // Delete existing content
            if (Directory.Exists(fullPath))
            {
                result.AddLog($"Deleting existing directory: {fullPath}");
                if (!await IOUtils.DeleteDirectory(fullPath, true, result))
                {
                    result.SetFailed($"Unable to delete existing directory: {fullPath}");
                    return false;
                }
            }
            else if (File.Exists(fullPath))
            {
                result.AddLog($"Deleting existing file: {fullPath}");
                File.Delete(fullPath);
            }
            
            // Create directory
            if (!Directory.Exists(directory))
            {
                result.AddLog($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }
            
            // Copy contents
            if (m_zipContent)
            {
                result.AddLog($"Zipping context to: {fullPath}");
                if (!await ZipUtils.Zip(m_taskContentsFolder, fullPath, result))
                {
                    return false;
                }
            }
            else if (Utils.IsPathADirectory(m_taskContentsFolder))
            {
                result.AddLog($"Copying directory to: {fullPath}");
                if (!await Utils.CopyDirectoryAsync(m_taskContentsFolder, fullPath, m_duplicateFileHandling, result))
                {
                    return false;
                }
            }
            else
            {
                result.AddLog($"Copying file to: {fullPath}");
                if (!await Utils.CopyFileAsync(m_taskContentsFolder, fullPath, m_duplicateFileHandling, result))
                {
                    return false;
                }
            }
            
            return true;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            if (string.IsNullOrEmpty(m_localPath))
            {
                errors.Add(new GUIContent("No local path selected"));
            }
            else if (Utils.PathContainsInvalidCharacters(FullPath()))
            {
                errors.Add(new GUIContent("Path contains invalid characters"));
            }

            if (m_zipContent)
            {
                if (string.IsNullOrEmpty(m_zippedFilesName))
                {
                    errors.Add(new GUIContent("No Zipped Name specified"));
                }
            }
        }

        public override void TryGetWarnings(List<GUIContent> warnings, Context ctx)
        {
            base.TryGetWarnings(warnings, ctx);

            string fullPath = FullPath();
            if (!Utils.PathExists(fullPath))
            {
                warnings.Add(new GUIContent("Path does not exist and will be created", "Path does not exist but will be created during upload: " + fullPath));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["m_localPath"] = m_localPath;
            data["m_fileName"] = m_zippedFilesName;
            data["m_zipContent"] = m_zipContent;
            data["m_duplicateFileHandling"] = (int)m_duplicateFileHandling;
            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_localPath = (string)data["m_localPath"];
            m_zippedFilesName = (string)data["m_fileName"];
            m_zipContent = (bool)data["m_zipContent"];

            if (data.TryGetValue("m_duplicateFileHandling", out object handling) && handling is long)
            {
                m_duplicateFileHandling = (Utils.FileExistHandling)(long)handling;
            }
            else
            {
                m_duplicateFileHandling = Utils.FileExistHandling.Error;
            }
        }
    }
}