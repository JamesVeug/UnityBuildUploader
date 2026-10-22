using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadConfig
    {
        public Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["enabled"] = Enabled,
                ["guid"] = GUID,
                ["sources"] = m_buildSources.Select(a => a.Serialize()).ToList(),
                ["allModifiers"] = m_modifiers.Select(a => a.Serialize()).ToList(),
                ["destinations"] = m_buildDestinations.Select(a => a.Serialize()).ToList(),
                ["actions"] = m_postActions.Select(a => a.Serialize()).ToList()
            };

            return data;
        }

        public void Deserialize(Dictionary<string, object> data)
        {
            // Enabled
            if (data.TryGetValue("enabled", out object enabled))
            {
                Enabled = (bool)enabled;
            }

            // GUID
            if (data.TryGetValue("guid", out object guid))
            {
                GUID = (string)guid;
            }
            else
            {
                // Generate a new GUID (Introduced in v2.1.0
                GUID = Guid.NewGuid().ToString().Substring(0, 5);
            }

            m_buildSources = new List<SourceData>();
            m_modifiers = new List<ModifierData>();
            m_buildDestinations = new List<DestinationData>();
            m_postActions = new List<UploadActionData>();

            // Migrate any old data
            try
            {
                Migrate_v210_to_v220(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Sources
            try
            {
                if (data.TryGetValue("sources", out object sources))
                {
                    List<object> sourceList = (List<object>)sources;
                    foreach (object source in sourceList)
                    {
                        Dictionary<string, object> sourceDictionary = (Dictionary<string, object>)source;
                        SourceData sourceData = new SourceData();
                        sourceData.Deserialize(sourceDictionary);
                        AddSource(sourceData);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Modifiers
            try
            {
                if (data.TryGetValue("allModifiers", out object modifiers) && modifiers != null)
                {
                    List<object> modifierList = (List<object>)modifiers;
                    foreach (object modifier in modifierList)
                    {
                        Dictionary<string, object> modifierDictionary = (Dictionary<string, object>)modifier;
                        ModifierData modifierData = new ModifierData();
                        modifierData.Deserialize(modifierDictionary);
                        AddModifier(modifierData);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Destinations
            try
            {
                if (data.TryGetValue("destinations", out object destinations))
                {
                    List<object> destinationList = (List<object>)destinations;
                    foreach (object destination in destinationList)
                    {
                        Dictionary<string, object> destinationDictionary = (Dictionary<string, object>)destination;
                        DestinationData destinationData = new DestinationData();
                        destinationData.Deserialize(destinationDictionary);
                        AddDestination(destinationData);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            
            // Actions
            try
            {
                if (data.TryGetValue("actions", out object actions))
                {
                    List<object> actionList = (List<object>)actions;
                    foreach (object action in actionList)
                    {
                        Dictionary<string, object> actionDictionary = (Dictionary<string, object>)action;
                        UploadActionData actionData = new UploadActionData();
                        actionData.Deserialize(actionDictionary);
                        AddPostAction(actionData);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        
        /// <summary>
        /// v2.2.0 changes
        /// Adds multiple sources and destinations were introduced along with their Data wrapper types
        /// Modifiers are now a list of ModifierData instead of ABuildConfigModifer (Same as sources and destinations)
        /// Wireframe.ExcludeFilesByRegex_BuildModifier was renamed to Wireframe.ExcludeFilesModifier
        /// Enabled field on Steam DRM moved to the ModifierData
        /// </summary>
        /// <param name="data"></param>
        private void Migrate_v210_to_v220(Dictionary<string, object> data)
        {
            // Convert single source to a list
            try
            {
                // Source
                if (data.TryGetValue("sourceFullType", out object sourceFullType) && sourceFullType != null)
                {
                    string sourceFullPath = (string)sourceFullType;
                    Type sourceType = InternalUtils.FindType(sourceFullPath);
                    if (sourceType != null)
                    {
                        SourceData sourceData = new SourceData();
                        sourceData.Enabled = true;
                        sourceData.Source = Activator.CreateInstance(sourceType, new object[] { }) as AUploadSource;
                        if (sourceData.Source != null)
                        {
                            // Source
                            Dictionary<string, object> sourceDictionary = (Dictionary<string, object>)data["source"];
                            sourceData.Source.Deserialize(sourceDictionary);
                            sourceData.SourceType = UIHelpers.SourcesPopup.Values.FirstOrDefault(a => a.Type == sourceType);
                            AddSource(sourceData);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            
            // Convert modifiers to a list of ModifierData
            try
            {
                if (data.TryGetValue("modifiers", out object modifiers) && modifiers != null)
                {
                    m_modifiers = new List<ModifierData>(); // Clear so we know its empty

                    List<object> modifierList = (List<object>)modifiers;
                    foreach (object modifier in modifierList)
                    {
                        Dictionary<string, object> modifierDictionary = (Dictionary<string, object>)modifier;
                        if (modifierDictionary.TryGetValue("$type", out object modifierType))
                        {
                            string typeString = (string)modifierType;
                            if (typeString == "Wireframe.ExcludeFilesByRegex_BuildModifier")
                            {
                                // Wireframe.ExcludeFilesByRegex_BuildModifier was renamed to
                                // Wireframe.ExcludeFilesModifier
                                Debug.Log("Migrating ExcludeFilesByRegex_BuildModifier to ExcludeFilesModifier");
                                typeString = typeof(ExcludeFilesModifier).FullName;
                                if(modifierDictionary.TryGetValue("regexes", out object regexes))
                                {
                                    List<object> regexList = (List<object>)regexes;
                                    if (regexList.Count == 1)
                                    {
                                        Dictionary<string,object> regexDictionary = (Dictionary<string,object>)regexList[0];
                                        string s = (string)regexDictionary["Regex"];
                                        if (s == "*_DoNotShip")
                                        {
                                            Debug.Log("Migrating excluding *_DoNotShip files to ignore to ignore folders.");
                                            typeString = typeof(ExcludeFoldersModifier).FullName;
                                        }
                                    }
                                }
                            }
                            
                            Type type = InternalUtils.FindType(typeString);
                            if (type != null)
                            {
                                AUploadModifer uploadModifer = Activator.CreateInstance(type) as AUploadModifer;
                                if (uploadModifer != null)
                                {
                                    uploadModifer.Deserialize(modifierDictionary);
                                    
                                    // Steam DRM had its own 'enabled' field
                                    bool enabled = true;
                                    if (modifierDictionary.TryGetValue("enabled", out object enabledValue))
                                    {
                                        enabled = (bool)enabledValue;
                                    }
                                    ModifierData newModifierData = new ModifierData(uploadModifer, enabled);
                                    AddModifier(newModifierData);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Convert single destination to a list
            try
            {
                // Destination
                if (data.TryGetValue("destinationFullType", out object destinationFullType) && destinationFullType != null)
                {
                    string destinationFullPath = (string)destinationFullType;
                    Type type = InternalUtils.FindType(destinationFullPath);
                    if (type != null)
                    {
                        DestinationData destinationData = new DestinationData();
                        destinationData.Enabled = true;
                        destinationData.Destination = Activator.CreateInstance(type, new object[] { }) as AUploadDestination;
                        if (destinationData.Destination != null)
                        {
                            Dictionary<string, object> destinationDictionary = (Dictionary<string, object>)data["destination"];
                            destinationData.Destination.Deserialize(destinationDictionary);
                            destinationData.DestinationType = UIHelpers.DestinationsPopup.Values.FirstOrDefault(a => a.Type == type);
                            AddDestination(destinationData);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}