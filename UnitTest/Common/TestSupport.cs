using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    public static class TestSupport
    {
        public static UploadTaskReport.StepResult Report()
        {
            return new UploadTaskReport.StepResult(new UploadTaskReport("unit-test", "UnitTest", false));
        }

        public static string RequiredEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                string message = "Configure " + name + " to run this integration test.";
                if (Environment.GetEnvironmentVariable("BUILDUPLOADER_TEST_REQUIRE_CONFIGURATION") == "1")
                    Assert.Fail(message);
                Assert.Ignore(message);
            }
            return value;
        }

        public static string OutputDirectory(string name)
        {
            string root = Path.GetFullPath(Path.Combine("TestResults", "BuildUploader", name, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            return root;
        }

        public static UploadTask FileUploadTask(string name, string sourceDirectory, AUploadDestination destination)
        {
            var config = new UploadConfig();
            config.AddSource(new FolderSource(sourceDirectory));
            config.AddDestination(destination);
            var task = new UploadTask(name, config);
            task.SetBuildDescription("Build Uploader automated dry run");
            return task;
        }

        public static string FixtureDirectory(string name, string contents = "Build Uploader integration fixture\n")
        {
            string directory = OutputDirectory(name);
            File.WriteAllText(Path.Combine(directory, "index.html"), contents);
            return directory;
        }

        public static IEnumerator Run(UploadTask task)
        {
            yield return Execute(task);
            Assert.IsTrue(task.IsSuccessful, task.Report == null ? "UploadTask did not create a report." : task.Report.GetReport());
        }

        /// <summary>Runs the task to completion without requiring success, for failure-path tests.</summary>
        public static IEnumerator Execute(UploadTask task, double timeoutSeconds = 0)
        {
            Task operation = task.StartAsync(false);
            yield return Await(operation, timeoutSeconds);
            Assert.IsTrue(task.IsComplete);
            Assert.IsNotNull(task.Report, "UploadTask did not create a report.");
        }

        // UnityTest coroutines work with the Test Framework versions shipped for Unity 2019 onward.
        public static IEnumerator Await(Task task, double timeoutSeconds = 0)
        {
            yield return WaitUntil(() => task.IsCompleted, timeoutSeconds, "Task did not complete");
            task.GetAwaiter().GetResult();
        }

        /// <summary>Yields editor frames until the condition holds. Zero waits without a limit.</summary>
        public static IEnumerator WaitUntil(Func<bool> condition, double timeoutSeconds, string message)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (!condition())
            {
                if (timeoutSeconds > 0 && DateTime.UtcNow > deadline)
                    Assert.Fail(message + " within " + timeoutSeconds + " seconds.");
                yield return null;
            }
        }

        public static string Quote(string argument)
        {
            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
