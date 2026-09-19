using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadConfig
    {
        [Wiki("Sources", "Specify what data you want to include in your upload. All Sources are executed at the same time then copied to the cached folder 1 by 1.")]
        public class SourceData
        {
            [NonSerialized]
            public bool ShowFormattedExportFolder = false;
            
            [Wiki("Enabled", "When enabled, this source will gather the content from a source and get it ready to be uploaded")]
            public bool Enabled = true;
            
            [Wiki("Sub Folder", "A sub-path in the cached directory of which this source will be saved to before being modified and uploaded. Leave empty to save to the root folder.")]
            public string SubFolder;
            
            [Wiki("Do Not Cache", "If enabled, all files will be saved directly to the folder for the upload task and not cached for future reference. This will speed up uploading at the cost of having to re-get the source contents every upload." +
                                  "\nExample: It will make a full new build every upload")]
            public bool DoNotCache = true;
            
            [Wiki("Duplicate Files", "When copying files over and there already being the same file, what should we do with the new file?")]
            public Utils.FileExistHandling DuplicateFileHandling = Utils.FileExistHandling.Error;
            
            public AUploadSource Source;
            public UIHelpers.BuildSourcesPopup.SourceData SourceType;

            public SourceData()
            {
                
            }
            
            public SourceData(AUploadSource source, bool enabled=true)
            {
                Enabled = enabled;
                Source = source;
                SourceType = UIHelpers.SourcesPopup.Values.FirstOrDefault(a => a.Type == source.GetType());
            }

            public Dictionary<string,object> Serialize()
            {
                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    ["enabled"] = Enabled,
                    ["DoNotCache"] = DoNotCache,
                    ["duplicateFileHandling"] = DuplicateFileHandling.ToString(),
                    ["subFolderPath"] = SubFolder,
                    ["sourceType"] = SourceType?.Type?.FullName,
                    ["source"] = Source?.Serialize()
                };

                return data;
            }

            public void Deserialize(Dictionary<string,object> data)
            {
                Enabled = data.ContainsKey("enabled") ? (bool)data["enabled"] : true;
                DoNotCache = data.ContainsKey("DoNotCache") ? (bool)data["DoNotCache"] : true;
                DuplicateFileHandling = data.ContainsKey("duplicateFileHandling") ? (Utils.FileExistHandling)System.Enum.Parse(typeof(Utils.FileExistHandling), data["duplicateFileHandling"] as string) : Utils.FileExistHandling.Error;
                SubFolder = data.ContainsKey("subFolderPath") ? data["subFolderPath"] as string : "";
                if (data.TryGetValue("sourceType", out object sourceType) && sourceType != null)
                {
                    Type type = Type.GetType(sourceType as string);
                    if (UIHelpers.SourcesPopup.TryGetValueFromType(type, out var newSourceType))
                    {
                        if (Utils.CreateInstance(newSourceType.Type, out Source))
                        {
                            SourceType = newSourceType;
                            Source.Deserialize(data["source"] as Dictionary<string, object>);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Cannot find type '{sourceType}'");
                    }
                }
            }
        }
    }
}