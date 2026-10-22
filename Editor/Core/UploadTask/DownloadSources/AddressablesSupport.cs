using System;

namespace Wireframe
{
    /// <summary>
    /// What build sources need from the Addressables package. Core cannot reference Addressables, so the optional
    /// Editor/Integrations/Addressables assembly sets AddressablesSupport.Implementation when the package is installed.
    /// </summary>
    internal interface IAddressablesSupport
    {
        /// <summary>
        /// True if the project has Addressables settings
        /// </summary>
        bool SettingsExist { get; }

        /// <summary>
        /// True if the project's settings already build Addressables content with every player build
        /// </summary>
        bool BuildsWithPlayerByDefault { get; }

        /// <summary>
        /// Deletes previously built Addressables content
        /// </summary>
        /// <returns>False if it could not be cleaned</returns>
        bool CleanContent(UploadTaskReport.StepResult stepResult);

        /// <summary>
        /// Makes player builds also build Addressables content until the scope is disposed.
        /// Addressables then builds the content inside BuildPipeline.BuildPlayer for the target being built.
        /// Disposing restores the project's setting and fails stepResult if the content build failed.
        /// </summary>
        IDisposable ForceBuildWithPlayer(UploadTaskReport.StepResult stepResult);
    }

    internal static class AddressablesSupport
    {
        public const string Requirement = "Requires the Addressables package (1.19.4 or newer) on Unity 2021.2 or newer";

        /// <summary>
        /// Set by the Addressables integration when it is compiled. Null without the package.
        /// </summary>
        public static IAddressablesSupport Implementation { get; set; }

        public static bool IsAvailable => Implementation != null;
    }
}
