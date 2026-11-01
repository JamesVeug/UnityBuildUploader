using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Base class to allow the user to select a path of some sort to upload
    /// </summary>
    public abstract partial class ABrowsePathSource : AUploadSource
    {
        protected enum PathType
        {
            [Wiki(nameof(Absolute), "Specify the full path of a file or folder.")]
            Absolute,
            
            [Wiki(nameof(PathToAssets), "Specify the path starting from the projects Assets folder.")]
            PathToAssets,

            [Wiki(nameof(RelativeToRootPath), "Specify the path starting from the Root Path set in Preferences.")]
            RelativeToRootPath,
        }
        
        [Wiki("Path", "The path to the file or folder to upload.")]
        protected string m_enteredFilePath = "";
        
        [Wiki("Path Type", "Specify which directory to select the content from.")]
        private PathType m_pathType;

        private string m_finalSourcePath = "";

        public ABrowsePathSource() : base()
        {
            // Required for reflection
        }

        public ABrowsePathSource(string path) : base()
        {
            m_enteredFilePath = path;
        }

        public bool SetNewPath(string newPath)
        {
            if (newPath == m_enteredFilePath || string.IsNullOrEmpty(newPath))
            {
                return false;
            }
            
            m_enteredFilePath = newPath;
            return true;
        }

        public bool PathExists()
        {
            string path = GetFullPath();
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }
            
            return File.Exists(path) || Directory.Exists(path);
        }

        public override Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig, UploadTaskReport.StepResult stepResult,
            CancellationTokenSource token)
        {
            // Make copy to avoid sharing conflicts
            // If it's a directory, copy the whole thing to a folder with the same name
            // If it's a file, copy it to the directory
            string sourcePath = GetFullPath();
            bool isDirectory = Utils.IsPathADirectory(sourcePath);
            if (!isDirectory && sourcePath.EndsWith(".exe"))
            {
                // Given a .exe. Use the Folder because they likely want to upload the entire folder - not just the .exe
                sourcePath = Path.GetDirectoryName(sourcePath);
            }
            
            m_finalSourcePath = sourcePath;
            stepResult.AddLog($"Using source path: {m_finalSourcePath}");
            return Task.FromResult(true);
        }

        public string GetFullPath()
        {
            string path = GetSubPath();
            string enteredPath = m_context.FormatString(m_enteredFilePath);
            if (string.IsNullOrEmpty(path))
            {
                return enteredPath;
            }

            // Trim so Path.Combine doesn't discard the sub path when the entered path starts with a separator
            return Path.Combine(path, enteredPath.TrimStart('/', '\\'));
        }

        private string GetSubPath()
        {
            switch (m_pathType)
            {
                case PathType.Absolute:
                    return "";
                case PathType.PathToAssets:
                    return Application.dataPath;
                case PathType.RelativeToRootPath:
                    return m_context.FormatString(Context.RootPath());
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string SourceFilePath()
        {
            return m_finalSourcePath;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            string path = GetFullPath();
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                errors.Add(new GUIContent("Path does not exist"));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>()
            {
                { "enteredFilePath", m_enteredFilePath },
                { "pathType", (long)m_pathType }
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            if (data.TryGetValue("enteredFilePath", out object p))
            {
                m_enteredFilePath = (string)p;
            }
            
            if (data.TryGetValue("pathType", out object pt))
            {
                m_pathType = (PathType)(long)pt;
            }
            else
            {
                m_pathType = PathType.Absolute;
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            Dictionary<string, object> state = NewRunState();
            state["finalSourcePath"] = m_finalSourcePath;
            return state;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            base.RestoreRunState(state);
            m_finalSourcePath = state["finalSourcePath"] as string ?? "";
        }
    }
}