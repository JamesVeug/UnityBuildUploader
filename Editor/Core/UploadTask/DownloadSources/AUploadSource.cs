using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Sources are where files come from to be uploaded.
    /// Example: Let the user select a folder somewhere on their PC to be included and copied to the upload staging area.
    /// </summary>
    public abstract partial class AUploadSource
    {
        public bool CanCacheContents => GetType().GetCustomAttribute<UploadSourceAttribute>()?.CanCacheContents ?? false;

        public Context Context => m_context;
        
        protected Context m_context;
        protected string m_taskContentsFolder;

        public AUploadSource() : base()
        {
            m_context = CreateContext();
        }


        protected virtual Context CreateContext()
        {
            Context context = new Context();
            
            return context;
        }
        
        /// <summary>
        /// Returns a short summary of the different things for this that can be changed
        /// eg: FileSource: Copying 'C:/MyFiles/ThatImportantFile.json'
        /// eg: BuildConfig: Release Build (Windows64)
        /// Used for {taskStatus}
        /// </summary>
        public abstract string Summary();

        /// <summary>
        /// Preparation step before any sources are started.
        /// If any return false then the upload is stopped.
        /// </summary>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <param name="token">Check this if the task has been told to stop mid-execution</param>
        /// <returns></returns>
        public virtual Task<bool> Prepare(string taskContentsFolder, UploadTaskReport.StepResult stepResult, CancellationTokenSource token)
        {
            m_taskContentsFolder = taskContentsFolder;
            return Task.FromResult(true);
        }

        /// <summary>
        /// Retrieves all the content needed for this source to be included in the upload.
        /// When all sources have been retrieved, SourceFilePath() will be invoked to get the content that will be copied.
        /// </summary>
        /// <param name="doNotCache">When true save to a temporary location before it gets copied over to the upload task cache</param>
        /// <param name="uploadConfig">Which config this source is owned by</param>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <param name="token">Check this if the task has been told to stop mid-execution</param>
        /// <returns></returns>
        public abstract Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig, UploadTaskReport.StepResult stepResult,
            CancellationTokenSource token);
        /// <summary>
        /// Invoked after all sources are successful and will copy them to a cached folder location ready for upload.
        /// Return the path to the file or folder that is required to be included in your upload
        /// </summary>
        /// <returns>File or folder that is required</returns>
        public abstract string SourceFilePath();

        /// <summary>
        /// Executed at the end of the upload process regardless if it was successful or not.
        /// Reset any temporary data or delete files.
        /// </summary>
        /// <param name="configIndex"></param>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <returns></returns>
        public virtual Task CleanUp(int configIndex, UploadTaskReport.StepResult stepResult)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Check for anything that is concerning that the user should be warned about but not prevent upload.
        /// </summary>
        /// <param name="warnings">Add to this list any warnings you need</param>
        /// <param name="ctx">Context for formatting strings such as {version}</param>
        public virtual void TryGetWarnings(List<GUIContent> warnings)
        {
            
        }

        /// <summary>
        /// Executed during GUI and before an upload starts to check for any warnings in the configuration of this source
        /// </summary>
        /// <param name="errors">Errors found in this method</param>
        public virtual void TryGetErrors(List<GUIContent> errors)
        {
            
        }

        /// <summary>
        /// Save your data as a Dictionary or List to persist it between sessions
        /// </summary>
        public abstract Dictionary<string, object> Serialize();
        
        /// <summary>
        /// Load your data from a Dictionary or List to restore it between sessions
        /// </summary>
        public abstract void Deserialize(Dictionary<string, object> data);

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public virtual Dictionary<string, object> CaptureRunState()
        {
            return null;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public virtual void RestoreRunState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("taskContentsFolder", out object folder))
            {
                m_taskContentsFolder = folder as string;
            }
        }

        /// <summary>
        /// The run state every source has, for CaptureRunState overrides to add to
        /// </summary>
        protected Dictionary<string, object> NewRunState()
        {
            return new Dictionary<string, object>
            {
                ["taskContentsFolder"] = m_taskContentsFolder,
            };
        }

        /// <summary>
        /// When starting an Upload Task we cache all fields so they can be shared everywhere
        /// So bump any versions we need before we do so
        /// </summary>
        public virtual void PrepareContextForCaching()
        {
            
        }
    }
}