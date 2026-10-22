using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Actions are steps in the upload process that can perform any kind of action - typically that do not modify files.
    /// Example: Send message to discord
    /// </summary>
    public abstract partial class AUploadAction
    {
        public Context Context => m_context;
        protected Context m_context;

        /// <summary>
        /// Whether "Stop task if this fails" starts on when the user picks this action.
        /// Override to return true for actions that later steps depend on, such as building content before a build.
        /// </summary>
        public virtual bool DefaultStopTaskIfFailed => false;

        public AUploadAction()
        {
            m_context = CreateContext();
        }


        protected virtual Context CreateContext()
        {
            Context context = new Context();
            
            return context;
        }
        /// <summary>
        /// Prepare the action to ensure it's ready to execute
        /// If this returns false then Execute is not... executed.
        /// </summary>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <returns>True if successfully prepared</returns>
        public virtual Task<bool> Prepare(UploadTaskReport.StepResult stepResult)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Runs the action
        /// </summary>
        /// <param name="stepResult">Information in the current upload step. Add logs to this or stop with SetFailed</param>
        /// <returns>True if successfully prepared</returns>
        public abstract Task<bool> Execute(UploadTaskReport.StepResult stepResult);
        
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
        /// Check for anything that is concerning that the user should be warned about but not prevent upload.
        /// </summary>
        /// <param name="warnings">Add to this list any warnings you need</param>
        public virtual void TryGetWarnings(List<GUIContent> warnings)
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

        /// <summary>
        /// Save your data as a Dictionary or List to persist it between sessions
        /// </summary>
        public abstract Dictionary<string, object> Serialize();
        
        /// <summary>
        /// Load your data from a Dictionary or List to restore it between sessions
        /// </summary>
        public abstract void Deserialize(Dictionary<string, object> data);
    }
}