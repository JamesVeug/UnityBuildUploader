using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// ProcessUtils fixes from PR #118: colour codes stripped from captured output (SteamCMD colours piped
    /// output on Linux) and EnsureExecutable restoring the exec bit SDK archives often lose.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class ProcessUtilsTests
    {
        private const string Escape = "\u001b";

        [Test]
        public void CapturedOutputHasAnsiCodesStripped()
        {
            string file = WriteColouredText();

            ProcessUtils.ProcessResult result = ProcessUtils.RunSync(PrintCommand(), PrintArguments(file),
                Path.GetDirectoryName(file), 30000);

            Assert.IsTrue(result.IsSuccessful, result.Errors);
            AssertStripped(result.Output);
        }

        [UnityTest]
        public IEnumerator CapturedTaskOutputHasAnsiCodesStripped()
        {
            string file = WriteColouredText();
            Task<ProcessUtils.ProcessResult> run = ProcessUtils.RunTask(TestSupport.Report(), PrintCommand(), PrintArguments(file), null);

            yield return TestSupport.Await(run, 30);

            Assert.IsTrue(run.Result.IsSuccessful, run.Result.Errors);
            AssertStripped(run.Result.Output);
        }

        [Test]
        public void EnsureExecutableMakesToolRunnable()
        {
            string tool = Path.Combine(TestSupport.OutputDirectory("Executable"), "build uploader's tool");
            File.WriteAllText(tool, "#!/bin/sh\nprintf runnable\n");
#if !UNITY_EDITOR_WIN
            Assert.IsFalse(IsExecutable(tool), "Precondition: a freshly written file has no exec bit.");
#endif

            Assert.IsTrue(ProcessUtils.EnsureExecutable(tool, out string error), error);
            Assert.IsNull(error);
#if !UNITY_EDITOR_WIN
            Assert.IsTrue(IsExecutable(tool), "EnsureExecutable must set the exec bit.");
            ProcessUtils.ProcessResult run = ProcessUtils.RunSync(tool, "", Path.GetDirectoryName(tool), 30000);
            Assert.IsTrue(run.IsSuccessful, run.Errors);
            Assert.AreEqual("runnable", run.Output.Trim());
#endif
        }

        [Test]
        public void EnsureExecutableExplainsMissingTool()
        {
            string missing = Path.Combine(TestSupport.OutputDirectory("Executable"), "missing-tool");
            bool executable = ProcessUtils.EnsureExecutable(missing, out string error);
#if UNITY_EDITOR_WIN
            Assert.IsTrue(executable, "Windows has no exec bit, so EnsureExecutable always succeeds.");
            Assert.IsNull(error);
#else
            Assert.IsFalse(executable);
            StringAssert.Contains("chmod +x", error, "The error should tell the user how to fix it.");
#endif
        }

        /// <summary>True when the current user may execute path (always true on Windows).</summary>
        public static bool IsExecutable(string path)
        {
#if UNITY_EDITOR_WIN
            return File.Exists(path);
#else
            // Double-quote like ProcessUtils does: every host's argument splitting understands it.
            return ProcessUtils.RunSync("/bin/sh", "-c " + TestSupport.Quote("test -x \"$1\"") + " sh " + TestSupport.Quote(path),
                null, 30000).IsSuccessful;
#endif
        }

        private static string WriteColouredText()
        {
            string file = Path.Combine(TestSupport.OutputDirectory("Ansi"), "coloured.txt");
            File.WriteAllText(file, Escape + "[0mLogging in user 'build' to Steam Public..." + Escape + "[1;32mOK" + Escape + "[0m\n" +
                                    Escape + "[?25lUnloading Steam API..." + Escape + "[0mOK\n");
            return file;
        }

        private static void AssertStripped(string output)
        {
            Assert.IsFalse(output.Contains(Escape), "Escape characters survived: " + output.Replace(Escape, "\\e"));
            StringAssert.Contains("Logging in user 'build' to Steam Public...OK", output);
            StringAssert.Contains("Unloading Steam API...OK", output);
        }

        private static string PrintCommand()
        {
#if UNITY_EDITOR_WIN
            return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
#else
            return "/bin/cat";
#endif
        }

        private static string PrintArguments(string file)
        {
#if UNITY_EDITOR_WIN
            return "/d /s /c \"type \"" + file + "\"\"";
#else
            return TestSupport.Quote(file);
#endif
        }
    }
}
