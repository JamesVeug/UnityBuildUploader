using System.IO;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// A shell script that stands in for a console SDK's upload tool (Nintendo AuthoringTool, PlayStation
    /// publishing tool). It records its arguments, the command file it was given and a copy of the files at the
    /// command file's source_path, prints its arguments, and reports success. Needs /bin/sh, so macOS and Linux only.
    /// </summary>
    internal static class FakeUploadTool
    {
        public const string ArgumentsFile = "arguments.txt";
        public const string CommandFile = "command.txt";
        public const string UploadedFolder = "uploaded";

        /// <param name="commandFlag">The argument that precedes the command file path, e.g. --command.</param>
        /// <param name="recordDirectory">Where the script writes what it was asked to upload.</param>
        public static string Script(string commandFlag, string recordDirectory)
        {
            return "#!/bin/sh\n" +
                   "record=" + TestSupport.Quote(recordDirectory) + "\n" +
                   "printf '%s\\n' \"$@\" > \"$record/" + ArgumentsFile + "\"\n" +
                   "command=''\n" +
                   "previous=''\n" +
                   "for argument in \"$@\"; do\n" +
                   "  if [ \"$previous\" = '" + commandFlag + "' ]; then command=\"$argument\"; fi\n" +
                   "  previous=\"$argument\"\n" +
                   "done\n" +
                   "cp \"$command\" \"$record/" + CommandFile + "\"\n" +
                   "content=$(sed -n 's/^source_path=//p' \"$command\")\n" +
                   "mkdir -p \"$record/" + UploadedFolder + "\"\n" +
                   "cp -R \"$content/.\" \"$record/" + UploadedFolder + "/\"\n" +
                   "printf '%s\\n' \"$@\"\n" +
                   "echo 'Upload complete'\n";
        }

        /// <summary>The tool must have been handed exactly the dummy file the task uploaded.</summary>
        public static void AssertUploadedDummyFile(string recordDirectory)
        {
            string[] files = Directory.GetFiles(Path.Combine(recordDirectory, UploadedFolder), "*", SearchOption.AllDirectories);
            Assert.AreEqual(1, files.Length, "The tool must be given only the dummy file: " + string.Join(", ", files));
            Assert.AreEqual(DestinationTestSupport.DummyFileName + ".txt", Path.GetFileName(files[0]));
            Assert.AreEqual(DestinationTestSupport.DummyContents, File.ReadAllText(files[0]));
        }
    }
}
