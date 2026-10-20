using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Opens a new project in a second editor of this version holding only Build Uploader and the engine modules, so
    /// none of the packages it can optionally use are installed: Unity Pipeline (the `unity` CLI), Test Framework
    /// (the UnitTest folder), uGUI and IDE packages. The package must compile there, and the assemblies that need
    /// those packages must be left out instead of failing.
    /// </summary>
    public class OptionalPackagesTests
    {
        private const string PackageName = "com.veugeljame.builduploader";
        // Importing a new project takes a few minutes, longer on Unity 6.
        private const int TimeoutMilliseconds = 20 * 60 * 1000;

        [UnityTest, Category("BuildUploader.Compile"), Timeout(TimeoutMilliseconds + 60 * 1000)]
        public IEnumerator CompilesWithoutOptionalPackages()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UploadTask).Assembly);
            Assert.IsNotNull(package, "Build Uploader must be installed as a package to copy it into a new project.");

            string root = TestSupport.OutputDirectory("NoOptionalPackages");
            // A short path: package files nested under TestResults pass Windows' 260 character limit, which Unity 2019's Mono cannot exceed.
            string project = Path.Combine(Path.GetTempPath(), "BuildUploaderNoOptional", Guid.NewGuid().ToString("N").Substring(0, 8));
            CopyDirectory(package.resolvedPath, Path.Combine(project, "Packages", PackageName));
            Directory.CreateDirectory(Path.Combine(project, "Assets"));
            Directory.CreateDirectory(Path.Combine(project, "ProjectSettings"));
            File.WriteAllText(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: " + Application.unityVersion + "\n");
            File.WriteAllText(Path.Combine(project, "Packages", "manifest.json"), JSON.SerializeObject(Manifest()));

            string log = Path.Combine(root, "editor.log");
            Task<int> editor = RunEditor(project, log);
            yield return TestSupport.Await(editor);

            Assert.AreNotEqual(-1, editor.Result, "The editor did not finish within " + TimeoutMilliseconds / 60000 + " minutes. See " + log);
            string output = ReadLog(log);
            string[] errors = Regex.Matches(output, @"^.*error CS\d+.*$", RegexOptions.Multiline).Cast<Match>()
                .Select(match => match.Value.Trim()).Distinct().ToArray();
            Assert.IsEmpty(errors, "Build Uploader does not compile without its optional packages (project kept at " + project + "):\n" +
                                   string.Join("\n", errors));
            Assert.AreEqual(0, editor.Result, "The editor exited with code " + editor.Result + ". See " + log);

            string scripts = Path.Combine(project, "Library", "ScriptAssemblies");
            Assert.IsTrue(File.Exists(Path.Combine(scripts, "VeugelJame.BuildUploader.Editor.dll")),
                "The package's editor assembly was not compiled. See " + log + " and " + project);
            string[] needOptionalPackages = Directory.GetFiles(scripts, "*.dll").Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => name.StartsWith("BuildUploader.UnitTest.") || name == "VeugelJame.BuildUploader.Editor.UnityPipeline")
                .ToArray();
            CollectionAssert.IsEmpty(needOptionalPackages, "These assemblies need an optional package and must be left out without it.");

            // The imported project is large and only the log explains a failure, so keep the project only when failing.
            try
            {
                Directory.Delete(project, true);
            }
            catch (IOException)
            {
                // A helper process the editor started may still hold a file; the temp folder is cleared eventually.
            }
        }

        /// <summary>Helper processes the editor started can keep the log open for a moment after the editor exits.</summary>
        private static string ReadLog(string log)
        {
            if (!File.Exists(log))
            {
                return "";
            }

            using (var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Every engine module this editor ships, as a new project has, and no other package.</summary>
        private static Dictionary<string, object> Manifest()
        {
            var dependencies = new Dictionary<string, object>();
            string builtInPackages = Path.Combine(EditorApplication.applicationContentsPath, "Resources", "PackageManager", "BuiltInPackages");
            foreach (string module in Directory.GetDirectories(builtInPackages, "com.unity.modules.*"))
            {
                var modulePackage = JSON.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(Path.Combine(module, "package.json")));
                dependencies[Path.GetFileName(module)] = modulePackage["version"];
            }
            Assert.IsNotEmpty(dependencies, "No engine modules found in " + builtInPackages);

#if !UNITY_2021_1_OR_NEWER
            // ZipUtils needs SharpZipLib before 2021.1, so there it is a required package, not an optional one.
            UnityEditor.PackageManager.PackageInfo sharpZipLib = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.sharp-zip-lib");
            Assert.IsNotNull(sharpZipLib, "Install com.unity.sharp-zip-lib; Build Uploader needs it before Unity 2021.1.");
            dependencies["com.unity.sharp-zip-lib"] = sharpZipLib.version;
#endif
            return new Dictionary<string, object> { { "dependencies", dependencies } };
        }

        /// <summary>Starts this editor on the project. The task returns its exit code, or -1 after killing it on timeout.</summary>
        private static Task<int> RunEditor(string project, string log)
        {
            string executable = EditorApplication.applicationPath;
#if UNITY_EDITOR_OSX
            executable = Path.Combine(executable, "Contents", "MacOS", "Unity");
#endif
            var startInfo = new ProcessStartInfo(executable,
                "-batchmode -nographics -quit -projectPath \"" + project + "\" -logFile \"" + log + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process process = Process.Start(startInfo);
            return Task.Run(() =>
            {
                using (process)
                {
                    if (process.WaitForExit(TimeoutMilliseconds))
                    {
                        return process.ExitCode;
                    }

                    process.Kill();
                    process.WaitForExit();
                    return -1;
                }
            });
        }

        /// <summary>Copies the package as a user would get it, without hidden entries such as .git.</summary>
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source).Where(path => !Path.GetFileName(path).StartsWith(".")))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }

            foreach (string directory in Directory.GetDirectories(source).Where(path => !Path.GetFileName(path).StartsWith(".")))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}
