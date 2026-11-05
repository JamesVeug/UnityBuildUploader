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

        [Wiki("Dry Run", "Check the action without sending anything: what is checked depends on the action. " +
                         "Upload Tasks started as a dry run turn this on. A dry run upload will make local changes but nothing will leave the local PC.", 100)]
        protected bool m_dryRun;

        /// <summary>
        /// Whether "Stop task if this fails" starts on when the user picks this action.
        /// Override to return true for actions that later steps depend on, such as building content before a build.
        /// </summary>
        public virtual bool DefaultStopTaskIfFailed => false;

        /// <summary>
        /// Whether a dry run task (UploadTask.SetDryRun) skips this action. A dry run upload will make local changes but
        /// nothing will leave the local PC, so override to return false only when that holds: either it reads DryRun and
        /// then only checks what it would send, or it only works locally.
        /// </summary>
        public virtual bool SkippedDuringDryRun => true;

        /// <summary>
        /// True when this action only checks what it would send instead of sending it. See SetDryRun.
        /// </summary>
        public bool DryRun => m_dryRun;

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
        /// Turns the Dry Run setting on or off. Actions that a dry run doesn't skip read it while executing;
        /// ones that only work locally ignore it. A dry run task calls it with true on its own copy of the action.
        /// </summary>
        public void SetDryRun(bool dryRun)
        {
            m_dryRun = dryRun;
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
        }
    }
}