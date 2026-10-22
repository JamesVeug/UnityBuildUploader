using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// After a .unitypackage update is imported, files the new version no longer has are deleted and files that
    /// moved go to their new path, which Unity's importer doesn't do. Uses fake project folders, not the AssetDatabase.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UnityPackageCleanupTests
    {
        private const string Root = "Packages/com.test.cleanup";
        private const string ManifestGuid = "00000000000000000000000000000001";
        private const string EditorGuid = "00000000000000000000000000000002";
        private const string KeptGuid = "00000000000000000000000000000003";
        private const string OldHelperGuid = "00000000000000000000000000000004";
        private const string MovedGuid = "00000000000000000000000000000005";
        private const string NewHelperGuid = "00000000000000000000000000000006";
        private const string IntegrationsGuid = "00000000000000000000000000000007";
        private const string AsmdefGuid = "00000000000000000000000000000008";
        private const string OldFolderGuid = "00000000000000000000000000000009";
        private const string GoneGuid = "0000000000000000000000000000000a";
        private const string DocsGuid = "0000000000000000000000000000000b";

        private static void AddFile(string projectRoot, string path, string guid, string content = "// file")
        {
            string full = Path.Combine(projectRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
            File.WriteAllText(full + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
        }

        private static void AddFolder(string projectRoot, string path, string guid)
        {
            string full = Path.Combine(projectRoot, path);
            Directory.CreateDirectory(full);
            File.WriteAllText(full + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nfolderAsset: yes\n");
        }

        /// <summary>
        /// The package folder as Unity's importer leaves it after importing v2 over v1: v2's new files added, v1's
        /// removed files still there and Moved.cs updated at its old path.
        /// </summary>
        private static string ProjectAfterImport()
        {
            string project = TestSupport.OutputDirectory("UnityPackageCleanup");
            AddFile(project, Root + "/package.json", ManifestGuid, "{ \"version\": \"2.0.0\" }");
            AddFolder(project, Root + "/Editor", EditorGuid);
            AddFile(project, Root + "/Editor/Kept.cs", KeptGuid);
            AddFile(project, Root + "/Editor/OldHelper.cs", OldHelperGuid);
            AddFile(project, Root + "/Editor/Moved.cs", MovedGuid, "// v2 content at the v1 path");
            AddFile(project, Root + "/Editor/NewHelper.cs", NewHelperGuid);
            AddFolder(project, Root + "/Integrations", IntegrationsGuid);
            AddFile(project, Root + "/Integrations/Integrations.asmdef", AsmdefGuid);
            AddFolder(project, Root + "/OldFolder", OldFolderGuid);
            AddFile(project, Root + "/OldFolder/Gone.cs", GoneGuid);
            AddFolder(project, Root + "/Docs", DocsGuid);

            // Someone's own files without a .meta are never touched.
            File.WriteAllText(Path.Combine(project, Root, "Editor", "notes.txt"), "mine");
            File.WriteAllText(Path.Combine(project, Root, "Docs", "readme.txt"), "mine");
            return project;
        }

        private static string Version2Package(string folder)
        {
            string path = Path.Combine(folder, "cleanup-v2.unitypackage");
            FakeUnityPackage.Write(path,
                new FakeUnityPackage.Entry(ManifestGuid, Root + "/package.json", "{ \"version\": \"2.0.0\" }"),
                new FakeUnityPackage.Entry(EditorGuid, Root + "/Editor"),
                new FakeUnityPackage.Entry(KeptGuid, Root + "/Editor/Kept.cs", "// file"),
                new FakeUnityPackage.Entry(NewHelperGuid, Root + "/Editor/NewHelper.cs", "// file"),
                new FakeUnityPackage.Entry(IntegrationsGuid, Root + "/Integrations"),
                new FakeUnityPackage.Entry(AsmdefGuid, Root + "/Integrations/Integrations.asmdef", "{}"),
                new FakeUnityPackage.Entry(MovedGuid, Root + "/Integrations/Moved.cs", "// v2 content"));
            return path;
        }

        private static bool Exists(string project, string path)
        {
            return Utils.PathExists(Path.Combine(project, path));
        }

        [Test]
        public void UnityPackageFileListsEveryGuidAndPath()
        {
            string path = Path.Combine(TestSupport.OutputDirectory("UnityPackageFile"), "contents.unitypackage");
            FakeUnityPackage.Write(path,
                new FakeUnityPackage.Entry(EditorGuid, Root + "/Editor"),
                new FakeUnityPackage.Entry(KeptGuid, Root + "/Editor/Kept.cs", "// a file\nwith lines"),
                new FakeUnityPackage.Entry(MovedGuid, Root + "/Integrations/Moved.cs", "// file") { DotPrefix = true },
                new FakeUnityPackage.Entry(NewHelperGuid, Root + "/Editor/Long Name.cs", "// file") { LongName = true });

            Dictionary<string, string> contents = UnityPackageFile.ReadContents(path);

            Assert.AreEqual(4, contents.Count);
            Assert.AreEqual(Root + "/Editor", contents[EditorGuid]);
            Assert.AreEqual(Root + "/Editor/Kept.cs", contents[KeptGuid], "The \\n00 Unity writes after the path is not part of it.");
            Assert.AreEqual(Root + "/Integrations/Moved.cs", contents[MovedGuid], "./<guid>/pathname");
            Assert.AreEqual(Root + "/Editor/Long Name.cs", contents[NewHelperGuid], "Name from a GNU long name record.");
        }

        [Test]
        public void ImportedFileIsFoundFromWhatUnityPasses()
        {
            string folder = TestSupport.OutputDirectory("UnityPackageFind");
            string downloaded = Path.Combine(folder, "BuildUploader-v9_9_9.unitypackage");
            File.WriteAllText(downloaded, "");
            try
            {
                Assert.AreEqual(downloaded, UnityPackageCleanup.FindImportedFile(Path.Combine(folder, "BuildUploader-v9_9_9")),
                    "Unity passes the full path without .unitypackage.");

                UnityPackageCleanup.ExpectImport(downloaded);
                Assert.AreEqual(downloaded, UnityPackageCleanup.FindImportedFile("BuildUploader-v9_9_9"),
                    "Or only the name, which the GitHub update button's download matches.");
                Assert.IsNull(UnityPackageCleanup.FindImportedFile("SomeOtherAsset"));
            }
            finally
            {
                SessionState.EraseString(UnityPackageCleanup.PendingImportKey);
            }
        }

        [Test]
        public void CleanupDeletesRemovedFilesAndMovesMovedOnes()
        {
            string project = ProjectAfterImport();

            UnityPackageCleanup.Plan plan = UnityPackageCleanup.CleanUp(Version2Package(project), project, Root);

            Assert.IsNotNull(plan);
            Assert.IsNull(plan.Problem, plan.Problem);
            Assert.IsFalse(Exists(project, Root + "/Editor/OldHelper.cs"), "Removed in v2.");
            Assert.IsFalse(Exists(project, Root + "/Editor/OldHelper.cs.meta"));
            Assert.IsFalse(Exists(project, Root + "/OldFolder"), "Removed in v2, with everything in it.");
            Assert.IsFalse(Exists(project, Root + "/OldFolder.meta"));

            Assert.IsFalse(Exists(project, Root + "/Editor/Moved.cs"), "Moved in v2.");
            Assert.AreEqual("// v2 content at the v1 path", File.ReadAllText(Path.Combine(project, Root, "Integrations", "Moved.cs")));
            StringAssert.Contains(MovedGuid, File.ReadAllText(Path.Combine(project, Root, "Integrations", "Moved.cs.meta")), "The GUID moves with it.");

            Assert.IsTrue(Exists(project, Root + "/Editor/Kept.cs"));
            Assert.IsTrue(Exists(project, Root + "/Editor/NewHelper.cs"));
            Assert.IsTrue(Exists(project, Root + "/package.json"));
            Assert.IsTrue(Exists(project, Root + "/Editor/notes.txt"), "Files without a .meta aren't the package's.");
            Assert.IsTrue(Exists(project, Root + "/Docs/readme.txt"));
            Assert.IsTrue(Exists(project, Root + "/Docs"), "A removed folder that still holds someone's files stays.");
            Assert.AreEqual(1, plan.Skipped.Count, string.Join(", ", plan.Skipped));
        }

        [Test]
        public void CleanupIgnoresOtherPackages()
        {
            string project = ProjectAfterImport();
            string other = Path.Combine(project, "other.unitypackage");
            FakeUnityPackage.Write(other,
                new FakeUnityPackage.Entry("ffffffffffffffffffffffffffffffff", "Assets/Other/package.json", "{}"),
                new FakeUnityPackage.Entry(KeptGuid, "Assets/Other/Kept.cs", "// same GUID by chance"));

            Assert.IsNull(UnityPackageCleanup.CleanUp(other, project, Root), "No package.json with our GUID at our path.");
            Assert.IsTrue(Exists(project, Root + "/Editor/OldHelper.cs"));
            Assert.IsTrue(Exists(project, Root + "/Editor/Moved.cs"));
        }

        [Test]
        public void CleanupRefusesToDeleteMostOfThePackage()
        {
            string project = ProjectAfterImport();
            string partial = Path.Combine(project, "partial.unitypackage");
            FakeUnityPackage.Write(partial, new FakeUnityPackage.Entry(ManifestGuid, Root + "/package.json", "{}"));

            UnityPackageCleanup.Plan plan = UnityPackageCleanup.CleanUp(partial, project, Root);

            Assert.IsNotNull(plan.Problem);
            Assert.IsTrue(Exists(project, Root + "/Editor/Kept.cs"));
            Assert.IsTrue(Exists(project, Root + "/Editor/OldHelper.cs"));
        }

        [Test]
        public void CleanupLeavesGitCheckoutsAlone()
        {
            string project = ProjectAfterImport();
            Directory.CreateDirectory(Path.Combine(project, Root, ".git"));

            Assert.IsNull(UnityPackageCleanup.CleanUp(Version2Package(project), project, Root));
            Assert.IsTrue(Exists(project, Root + "/Editor/OldHelper.cs"));
        }
    }
}
