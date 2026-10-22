using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadConfig
    {
        [Wiki("Actions", "Specify what actions you want to perform when the upload completes.")]
        public class UploadActionData
        {
            internal bool Collapsed { get; set; } = true;
            
            public enum UploadCompleteStatus : byte
            {
                Always = 0,
                Never = 1,
                IfSuccessful = 2,
                IfFailed = 3,
            }
            
            public enum UploadTrigger : byte
            {
                Never = 0,
                OnTaskStarted = 1,
                AfterEachStepCompletes = 2,
                OnTaskFinished = 3,
            }

            [Wiki("When To Execute", "Choose what the condition is for the action to execute.")]
            public UploadCompleteStatus WhenToExecute;

            [Wiki("Triggers", "Choose what triggers the action to execute.")]
            public List<UploadTrigger> Triggers = new List<UploadTrigger>();
            
            public AUploadAction UploadAction;
            public UIHelpers.BuildActionPopup.ActionData ActionType;

            public UploadActionData()
            {
                
            }
            
            public UploadActionData(AUploadAction action, UploadCompleteStatus whenToExecute = UploadCompleteStatus.Always, params UploadTrigger[] triggers)
            {
                WhenToExecute = whenToExecute;
                UploadAction = action;
                ActionType = UIHelpers.ActionsPopup.Values.FirstOrDefault(a => a.Type == action.GetType());
                if (triggers != null)
                    Triggers.AddRange(triggers);
            }
            
            public UploadActionData(AUploadAction action, UploadCompleteStatus whenToExecute = UploadCompleteStatus.Always, List<UploadTrigger> triggers = null)
            {
                WhenToExecute = whenToExecute;
                UploadAction = action;
                ActionType = UIHelpers.ActionsPopup.Values.FirstOrDefault(a => a.Type == action.GetType());
                if (triggers != null)
                    Triggers.AddRange(triggers);
            }
            
            public void SetupDefaults()
            {
                ActionType = new UIHelpers.BuildActionPopup.ActionData();
                Triggers = new List<UploadTrigger>();
            }
            

            private const int Version = 2;
            public Dictionary<string,object> Serialize()
            {
                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    ["whenToExecute"] = (int)WhenToExecute,
                    ["triggers"] = Triggers.Select(t => (int)t).ToList(),
                    ["actionType"] = ActionType?.Type?.FullName,
                    ["action"] = UploadAction?.Serialize(),
                    ["version"] = Version,
                };

                return data;
            }

            public void Deserialize(Dictionary<string,object> data)
            {
                if (data == null)
                {
                    return;
                }

                // Migrate data if needed
                if (!data.TryGetValue("version", out object versionData))
                {
                    MigrateVersion(data, 1);
                }
                else
                {
                    long dataVersion = (long)versionData;
                    if (dataVersion < Version)
                    {
                        MigrateVersion(data, dataVersion);
                    }
                }

                // Load the data from the Dict
                WhenToExecute = data.TryGetValue("whenToExecute", out var value) ? (UploadCompleteStatus)(long)value : UploadCompleteStatus.Always;
                
                Triggers = new List<UploadTrigger>();
                if (data.TryGetValue("triggers", out var triggersData) && triggersData is List<object> triggers)
                {
                    Triggers.AddRange(triggers.Select(t => (UploadTrigger)(long)t));
                }

                ActionType = new UIHelpers.BuildActionPopup.ActionData();
                if (data.TryGetValue("actionType", out var actionTypeData) && actionTypeData != null)
                {
                    var type = InternalUtils.FindType(actionTypeData as string);
                    if (UIHelpers.ActionsPopup.TryGetValueFromType(type, out ActionType))
                    {
                        if (Utils.CreateInstance(ActionType.Type, out UploadAction))
                        {
                            UploadAction.Deserialize(data["action"] as Dictionary<string, object>);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Action type `{actionTypeData}` not found");
                    }
                }
            }

            private void MigrateVersion(Dictionary<string, object> data, long oldVersion)
            {
                if (oldVersion < 2)
                {
                    // We added triggers in v3.2.0
                    List<int> oldTriggers = new List<int>();
                    oldTriggers.Add((int)UploadTrigger.OnTaskFinished);
                    data["triggers"] = oldTriggers;
                }
            }

            public bool CanStartBuild(out string reason, Context ctx)
            {
                if (WhenToExecute == UploadCompleteStatus.Never)
                {
                    reason = ""; // Will never execute
                    return true;
                }
                
                if (Triggers.Count(a=>a != UploadTrigger.Never) == 0)
                {
                    reason = ""; // Will never execute
                    return true;
                }
                
                if (ActionType == null)
                {
                    reason = "No action type selected";
                    return false;
                }
                
                if (UploadAction == null)
                {
                    reason = "No action selected";
                    return false;
                }
                
                List<GUIContent> errors = new List<GUIContent>();
                UploadAction.TryGetErrors(errors);
                if (errors.Count > 0)
                {
                    reason = string.Join(", ", errors.ConvertAll(e => e.text));
                    return false;
                }
                
                reason = "";
                return true;
            }
        }
    }
}
