using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wireframe.UnitTest
{
    // InternalUtils discovers every non-abstract pipeline type, so these doubles also appear in the
    // pickers of a project that lists the package in testables. The UnitTest prefix keeps them obvious.

    /// <summary>Counts how often UploadTask prepares, executes and cleans up an action.</summary>
    public class UnitTestRecordingAction : AUploadAction
    {
        public int ExecuteCount { get; private set; }
        public int CleanUpCount { get; private set; }

        /// <summary>Saved with the action, so tests can check its data reloads.</summary>
        public string Label { get; set; } = "";

        public override Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            ExecuteCount++;
            stepResult.AddLog("UnitTestRecordingAction executed");
            return Task.FromResult(true);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            CleanUpCount++;
            return Task.CompletedTask;
        }

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object> { ["label"] = Label };

        public override void Deserialize(Dictionary<string, object> data)
        {
            Label = data != null && data.TryGetValue("label", out object label) ? label as string ?? "" : "";
        }
    }

    /// <summary>Destination whose upload always fails, optionally without recording a fail reason.</summary>
    public class UnitTestFailingDestination : AUploadDestination
    {
        public const string FailReason = "UnitTestFailingDestination refused the upload";

        private readonly bool m_setFailReason;

        public int UploadCount { get; private set; }
        public int CleanUpCount { get; private set; }

        public UnitTestFailingDestination() : this(true) { }

        public UnitTestFailingDestination(bool setFailReason)
        {
            m_setFailReason = setFailReason;
        }

        public override Task<bool> Upload(UploadTaskReport.StepResult stepResult)
        {
            UploadCount++;
            if (m_setFailReason)
            {
                stepResult.SetFailed(FailReason);
            }
            return Task.FromResult(false);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            CleanUpCount++;
            return Task.CompletedTask;
        }

        public override string Summary() => "Always fails";
        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        protected internal override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }

    /// <summary>Destination that blocks inside Upload until the test releases it.</summary>
    public class UnitTestGateDestination : AUploadDestination
    {
        private readonly TaskCompletionSource<bool> m_release = new TaskCompletionSource<bool>();

        public bool HasEntered { get; private set; }
        public int CleanUpCount { get; private set; }

        public void Release(bool uploadSucceeds)
        {
            m_release.TrySetResult(uploadSucceeds);
        }

        public override Task<bool> Upload(UploadTaskReport.StepResult stepResult)
        {
            HasEntered = true;
            return m_release.Task;
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            CleanUpCount++;
            return Task.CompletedTask;
        }

        public override string Summary() => "Waits for the test";
        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        protected internal override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }
}
