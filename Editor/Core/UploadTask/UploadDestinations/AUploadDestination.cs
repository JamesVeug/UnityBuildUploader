using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload destinations are the end points where files are uploaded to.
    /// Example: Upload files to Steamworks or copy them to a folder on disk.
    /// </summary>
    public abstract partial class AUploadDestination
    {
        public Context Context => m_context;
        
        protected Context m_context;
        protected string m_taskContentsFolder;

        public AUploadDestination()
        {
            m_context = CreateContext();
        }


        protected virtual Context CreateContext()
        {
            return new Context();
        }
        
        /// <summary>
        /// Returns a short summary of the different things for this that can be changed
        /// eg: LocalPathDestination: 'C:/MyBuilds/MyNewBuildFolder'
        /// eg: SteamWorks: Branch: alpha, Depots: Windows
        /// Used for {taskStatus}
        /// </summary>
        public abstract string Summary();

        /// <summary>
        /// Prepare the action to ensure it's ready to execute
        /// If this returns False, then Execute is not... executed.
        /// </summary>
        /// <param name="taskGUID">Unique ID of the Task</param>
        /// <param name="configIndex">Index of the upload config that contains this destination</param>
        /// <param name="destinationIndex">Index of the destination in the config to upload</param>
        /// <param name="taskContentsFolder">The files we want to upload</param>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <returns>True if successfully prepared</returns>
        public virtual Task<bool> Prepare(string taskGUID, int configIndex, int destinationIndex,
            string taskContentsFolder, UploadTaskReport.StepResult stepResult)
        {
            m_taskContentsFolder = taskContentsFolder;
            stepResult.AddLog("No preparation needed for destination");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Executes the upload to the destination
        /// </summary>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <returns>True if the upload was successful</returns>
        public abstract Task<bool> Upload(UploadTaskReport.StepResult stepResult);
        
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
            return new Dictionary<string, object>
            {
                ["taskContentsFolder"] = m_taskContentsFolder,
            };
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
        /// Executed at the end of the upload process regardless if it was successful or not.
        /// Reset any temporary data or delete files.
        /// </summary>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        public virtual Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executed after all destinations are 
        /// </summary>
        /// <param name="stepResult"></param>
        /// <returns>True if successfully post uploaded</returns>
        public virtual Task<bool> PostUpload(UploadTaskReport.StepResult stepResult)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Check for anything that is concerning that the user should be warned about but not prevent upload.
        /// </summary>
        /// <param name="warnings">Add to this list any warnings you need</param>
        /// <param name="ctx">Context for formatting strings such as {version}</param>
        public virtual void TryGetWarnings(List<GUIContent> warnings, Context ctx)
        {
            if (GetType().GetCustomAttribute(typeof(ExperimentalAttribute)) != null)
            {
                warnings.Add(new GUIContent("Experimental", $"{GetType().Name} is Experimental and may contain bugs. Report any issues to: https://github.com/JamesVeug/UnityBuildUploader/issues"));
            }
        }

        /// <summary>
        /// Executed during GUI and before an upload starts to check for any warnings in the configuration of this source
        /// </summary>
        /// <param name="errors">Errors found in this method</param>
        public virtual void TryGetErrors(List<GUIContent> errors)
        {
            
        }
    }
}