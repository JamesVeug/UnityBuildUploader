using System.Collections.Generic;
using System.Threading;
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

    /// <summary>Action that fails in a chosen way, without calling SetFailed itself.</summary>
    public class UnitTestFailingAction : AUploadAction
    {
        public enum FailureMode
        {
            ExecuteReturnsFalse,
            ExecuteThrows,
            PrepareReturnsFalse,
        }

        public const string ExceptionMessage = "UnitTestFailingAction threw";

        private readonly FailureMode m_mode;

        public int ExecuteCount { get; private set; }
        public int CleanUpCount { get; private set; }

        public UnitTestFailingAction() : this(FailureMode.ExecuteReturnsFalse) { }

        public UnitTestFailingAction(FailureMode mode)
        {
            m_mode = mode;
        }

        public override Task<bool> Prepare(UploadTaskReport.StepResult stepResult)
        {
            return Task.FromResult(m_mode != FailureMode.PrepareReturnsFalse);
        }

        public override Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            ExecuteCount++;
            if (m_mode == FailureMode.ExecuteThrows)
            {
                throw new System.InvalidOperationException(ExceptionMessage);
            }
            return Task.FromResult(false);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            CleanUpCount++;
            return Task.CompletedTask;
        }

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }

    /// <summary>Stands in for the Addressables integration so build sources can be checked without the package.</summary>
    internal class UnitTestAddressablesSupport : IAddressablesSupport
    {
        public bool SettingsExist { get; set; } = true;
        public bool BuildsWithPlayerByDefault { get; set; }

        public bool CleanContent(UploadTaskReport.StepResult stepResult) => true;

        public System.IDisposable ForceBuildWithPlayer(UploadTaskReport.StepResult stepResult) => new Scope();

        private sealed class Scope : System.IDisposable
        {
            public void Dispose() { }
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

    /// <summary>
    /// Source that blocks inside GetSource until the test releases it, so a test can stop a task mid-step as a
    /// script reload would. Its files come from Folder. Takes no build lock.
    /// </summary>
    public class UnitTestGateSource : AUploadSource
    {
        private readonly TaskCompletionSource<bool> m_release = new TaskCompletionSource<bool>();

        public string Folder { get; set; } = "";
        public bool HasEntered { get; private set; }

        public UnitTestGateSource() { }

        public UnitTestGateSource(string folder)
        {
            Folder = folder;
        }

        public void Release()
        {
            m_release.TrySetResult(true);
        }

        public override async Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig, UploadTaskReport.StepResult stepResult, CancellationTokenSource token)
        {
            HasEntered = true;
            return await m_release.Task;
        }

        public override string SourceFilePath() => Folder;
        public override string Summary() => "Waits for the test";
        public override void OnGUIExpanded(ref bool isDirty, UploadConfig.SourceData data) { }
        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object> { ["folder"] = Folder };

        public override void Deserialize(Dictionary<string, object> data)
        {
            Folder = data != null && data.TryGetValue("folder", out object folder) ? folder as string ?? "" : "";
        }
    }

    /// <summary>
    /// Source that counts how often it runs, across instances with the same Id. It saves no run state, so a task
    /// resumed after a script reload runs it again. Its files come from Folder.
    /// </summary>
    public class UnitTestCountingSource : AUploadSource
    {
        private static readonly Dictionary<string, int> s_runs = new Dictionary<string, int>();

        public string Id { get; set; } = "";
        public string Folder { get; set; } = "";

        public UnitTestCountingSource() { }

        public UnitTestCountingSource(string id, string folder)
        {
            Id = id;
            Folder = folder;
        }

        public static int Runs(string id)
        {
            return s_runs.TryGetValue(id, out int runs) ? runs : 0;
        }

        public override Task<bool> GetSource(bool doNotCache, UploadConfig uploadConfig, UploadTaskReport.StepResult stepResult, CancellationTokenSource token)
        {
            s_runs[Id] = Runs(Id) + 1;
            return Task.FromResult(true);
        }

        public override string SourceFilePath() => Folder;
        public override string Summary() => "Counts its runs";
        public override void OnGUIExpanded(ref bool isDirty, UploadConfig.SourceData data) { }
        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object> { ["id"] = Id, ["folder"] = Folder };

        public override void Deserialize(Dictionary<string, object> data)
        {
            Id = data != null && data.TryGetValue("id", out object id) ? id as string ?? "" : "";
            Folder = data != null && data.TryGetValue("folder", out object folder) ? folder as string ?? "" : "";
        }
    }

    /// <summary>
    /// Destination that reads its Dry Run setting and saves it like a real destination. Records the setting it uploaded with.
    /// </summary>
    public class UnitTestDryRunDestination : AUploadDestination
    {
        public int UploadCount { get; private set; }
        public bool UploadedAsDryRun { get; private set; }

        public override bool SkippedDuringDryRun => false;

        public override Task<bool> Upload(UploadTaskReport.StepResult stepResult)
        {
            UploadCount++;
            UploadedAsDryRun = m_dryRun;
            return Task.FromResult(true);
        }

        public override string Summary() => "Has a dry run";
        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        protected internal override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object> { ["dryRun"] = m_dryRun };

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_dryRun = data != null && data.TryGetValue("dryRun", out object dryRun) && dryRun is bool value && value;
        }
    }

    /// <summary>Destination without a dry run that counts its uploads.</summary>
    public class UnitTestRecordingDestination : AUploadDestination
    {
        public int UploadCount { get; private set; }
        public int CleanUpCount { get; private set; }

        public override Task<bool> Upload(UploadTaskReport.StepResult stepResult)
        {
            UploadCount++;
            return Task.FromResult(true);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            CleanUpCount++;
            return Task.CompletedTask;
        }

        public override string Summary() => "Has no dry run";
        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        protected internal override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }

    /// <summary>
    /// Action that reads its Dry Run setting and saves it like a real action. Records the setting it executed with.
    /// </summary>
    public class UnitTestDryRunAction : AUploadAction
    {
        public int ExecuteCount { get; private set; }
        public bool ExecutedAsDryRun { get; private set; }

        public override bool SkippedDuringDryRun => false;

        public override Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            ExecuteCount++;
            ExecutedAsDryRun = m_dryRun;
            return Task.FromResult(true);
        }

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object> { ["dryRun"] = m_dryRun };

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_dryRun = data != null && data.TryGetValue("dryRun", out object dryRun) && dryRun is bool value && value;
        }
    }

    /// <summary>
    /// Destination that reads its Dry Run setting but doesn't save it, so only the task can keep it on through a reload.
    /// Records the setting it uploaded with.
    /// </summary>
    public class UnitTestUnsavedDryRunDestination : AUploadDestination
    {
        public int UploadCount { get; private set; }
        public bool UploadedAsDryRun { get; private set; }

        public override bool SkippedDuringDryRun => false;

        public override Task<bool> Upload(UploadTaskReport.StepResult stepResult)
        {
            UploadCount++;
            UploadedAsDryRun = m_dryRun;
            return Task.FromResult(true);
        }

        public override string Summary() => "Has a dry run it doesn't save";
        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        protected internal override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }

    /// <summary>
    /// Action that reads its Dry Run setting but doesn't save it, so only the task can keep it on through a reload.
    /// Records the setting it executed with.
    /// </summary>
    public class UnitTestUnsavedDryRunAction : AUploadAction
    {
        public int ExecuteCount { get; private set; }
        public bool ExecutedAsDryRun { get; private set; }

        public override bool SkippedDuringDryRun => false;

        public override Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            ExecuteCount++;
            ExecutedAsDryRun = m_dryRun;
            return Task.FromResult(true);
        }

        public override void OnGUICollapsed(ref bool isDirty, float maxWidth) { }
        public override void OnGUIExpanded(ref bool isDirty) { }
        public override Dictionary<string, object> Serialize() => new Dictionary<string, object>();
        public override void Deserialize(Dictionary<string, object> data) { }
    }
}
