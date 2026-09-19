using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadConfig
    {
        [Wiki("Destinations", "Specify where you want to upload your build. All destinations are executed at the same time.")]
        public class DestinationData
        {
            [Wiki("Enabled", "When enabled, this destination will upload or copy all source content. Nowhere does not move any files.")]
            public bool Enabled = true;
            public AUploadDestination Destination;
            public UIHelpers.BuildDestinationsPopup.DestinationData DestinationType;

            public DestinationData()
            {
                
            }
            
            public DestinationData(AUploadDestination destination, bool enabled=true)
            {
                Enabled = enabled;
                Destination = destination;
                DestinationType = UIHelpers.DestinationsPopup.Values.FirstOrDefault(a => a.Type == destination.GetType());
            }

            public Dictionary<string,object> Serialize()
            {
                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    ["enabled"] = Enabled,
                    ["destinationType"] = DestinationType?.Type?.FullName,
                    ["destination"] = Destination?.Serialize(),
                };

                return data;
            }

            public void Deserialize(Dictionary<string,object> data)
            {
                Enabled = data.ContainsKey("enabled") ? (bool)data["enabled"] : true;
                if (data.TryGetValue("destinationType", out object destinationType) && destinationType != null)
                {
                    Type type = Type.GetType(destinationType as string);
                    if (UIHelpers.DestinationsPopup.TryGetValueFromType(type, out var newDestinationType))
                    {
                        if (Utils.CreateInstance(newDestinationType.Type, out Destination))
                        {
                            DestinationType = newDestinationType;
                            Destination.Deserialize(data["destination"] as Dictionary<string, object>);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Destination type `{destinationType}` not found");
                    }
                }
            }
        }
    }
}