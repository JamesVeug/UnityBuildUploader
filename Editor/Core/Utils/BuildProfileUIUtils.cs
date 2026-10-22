#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Wireframe
{
    internal static class BuildProfileUIUtils
    {
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ListenForNewBuildProfiles()
        {
            MethodInfo method = typeof(BuildProfile).GetMethod("AddOnBuildProfileCreated", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                Action<BuildProfile> callback = OnBuildProfileCreated;
                method.Invoke(null, new object[] { callback });
            }
        }

        private static void OnBuildProfileCreated(BuildProfile profile)
        {
            LoadAll();
        }
        
        public class BuildProfilePopup : CustomDropdown<BuildProfileWrapper>
        {
            public override string FirstEntryText => "Choose Build Profile";

            protected override List<BuildProfileWrapper> FetchAllData()
            {
                GetBuildProfiles();
                return data;
            }
        }

        private static List<BuildProfileWrapper> data;

        public static List<BuildProfileWrapper> GetBuildProfiles()
        {
            if (data == null)
            {
                LoadAll();
            }
            return data;
        }

		[MenuItem("Tools/Build Uploader/Other/Reload Build Profiles")]
        private static void LoadAll()
        {
            data = new List<BuildProfileWrapper>();

            List<BuildProfile> profiles = BuildUtils.GetAllCustomBuildProfiles();
            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                BuildProfileWrapper wrapper = new BuildProfileWrapper(profile, i + 1);
                data.Add(wrapper);
            }
            BuildProfilesPopup.Refresh();
        }

        public static void Clear()
        {
            data.Clear();
        }

        public static BuildProfilePopup BuildProfilesPopup => m_buildProfilePopup ?? (m_buildProfilePopup = new BuildProfilePopup());
        private static BuildProfilePopup m_buildProfilePopup;
    }
}
#endif