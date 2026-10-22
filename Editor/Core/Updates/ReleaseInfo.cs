using System;

namespace Wireframe
{
    /// <summary>
    /// One published Build Uploader release on GitHub.
    /// </summary>
    internal class ReleaseInfo
    {
        public string Tag = "";
        public SemanticVersion Version;
        public string Name = "";

        /// <summary>
        /// Markdown, written like a version in CHANGELOG.md but without the "# version" header.
        /// </summary>
        public string Notes = "";
        public DateTime PublishedUtc;
        public bool IsPreRelease;
        public string Url = "";
        public string UnityPackageName = "";
        public string UnityPackageUrl = "";

        public string Title => string.IsNullOrEmpty(Name) ? Tag : Name;
    }
}
