using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("veugeljame.builduploader.editor.services.apple")]
[assembly: InternalsVisibleTo("VeugelJame.BuildUploader.Editor.UnityPipeline")]
[assembly: InternalsVisibleTo("BuildUploader.UnitTest.Discord")]
[assembly: InternalsVisibleTo("BuildUploader.UnitTest.Itchio")]
[assembly: InternalsVisibleTo("BuildUploader.UnitTest.Common")]

namespace Wireframe
{
    internal static class InternalUtils
    {
        private static List<AService> allServices;
        private static List<Type> allBuildSources;
        private static List<Type> allBuildModifiers;
        private static List<Type> allBuildDestinations;
        private static List<Type> allBuildActions;
        
        public static List<AService> AllServices()
        {
            if (allServices == null)
            {
                FetchAllTypes();
            }
            
            return allServices;
        }
        
        public static T GetService<T>() where T : AService
        {
            foreach (AService service in AllServices())
            {
                if (service is T t)
                {
                    return t;
                }
            }
            
            return null;
        }
        
        public static List<Type> AllBuildSources()
        {
            if (allBuildSources == null)
            {
                FetchAllTypes();
            }
            
            return allBuildSources;
        }
        
        public static List<Type> AllBuildModifiers()
        {
            if (allBuildModifiers == null)
            {
                FetchAllTypes();
            }
            
            return allBuildModifiers;
        }
        
        public static List<Type> AllBuildDestinations()
        {
            if (allBuildDestinations == null)
            {
                FetchAllTypes();
            }
            
            return allBuildDestinations;
        }
        
        public static List<Type> AllBuildActions()
        {
            if (allBuildActions == null)
            {
                FetchAllTypes();
            }
            
            return allBuildActions;
        }
        
        private static void FetchAllTypes()
        {
            allServices = new List<AService>();
            allBuildSources = new List<Type>();
            allBuildModifiers = new List<Type>();
            allBuildDestinations = new List<Type>();
            allBuildActions = new List<Type>();
            
            Type sourceType = typeof(AUploadSource);
            Type serviceType = typeof(AService);
            Type modifierType = typeof(AUploadModifer);
            Type destinationType = typeof(AUploadDestination);
            Type actionType = typeof(AUploadAction);

            // Slow but only done once per compilation
            foreach (Assembly assembly in AssemblyUtils.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue;
                    if (type.IsAbstract) continue;

                    if (sourceType.IsAssignableFrom(type))
                    {
                        allBuildSources.Add(type);
                    }
                    else if (serviceType.IsAssignableFrom(type))
                    {
                        allServices.Add((AService)Activator.CreateInstance(type));
                    }
                    else if (destinationType.IsAssignableFrom(type))
                    {
                        allBuildDestinations.Add(type);
                    }
                    else if (modifierType.IsAssignableFrom(type))
                    {
                        allBuildModifiers.Add(type);
                    }
                    else if (actionType.IsAssignableFrom(type))
                    {
                        allBuildActions.Add(type);
                    }
                }
            }
        }
    }
}
