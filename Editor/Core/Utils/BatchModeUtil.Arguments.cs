using System;
using System.Collections.Generic;
namespace Wireframe
{
    public static partial class BatchModeUtil
    {
        /// <summary>
        /// The command line arguments that Execute reads
        /// </summary>
        internal class Arguments
        {
            /// <summary>
            /// GUIDs or names of the profiles to run. Without -uploadProfile this is every other argument, as older
            /// versions read them, and the ones that aren't profiles are skipped.
            /// </summary>
            public List<string> Profiles = new List<string>();

            /// <summary>
            /// True when the profiles were passed with -uploadProfile, so each one has to exist
            /// </summary>
            public bool ProfilesNamed;

            public bool Async;
            public bool DryRun;

            /// <summary>
            /// The upload description, or null to leave the task's default
            /// </summary>
            public string Description;

            /// <summary>
            /// Where to write each task's report, or null for nowhere
            /// </summary>
            public string ReportFile;

            /// <summary>
            /// Reads Environment.GetCommandLineArgs(). The first argument is the executable and is skipped.
            /// </summary>
            public static Arguments Parse(string[] arguments)
            {
                Arguments parsed = new Arguments();
                List<string> unnamed = new List<string>();
                for (int i = 1; i < arguments.Length; i++)
                {
                    string argument = arguments[i];
                    bool hasValue = i + 1 < arguments.Length;
                    if (Is(argument, ProfileArgument))
                    {
                        parsed.ProfilesNamed = true;
                        if (hasValue)
                        {
                            parsed.Profiles.Add(arguments[++i]);
                        }
                    }
                    else if (Is(argument, DescriptionArgument))
                    {
                        if (hasValue)
                        {
                            parsed.Description = arguments[++i];
                        }
                    }
                    else if (Is(argument, ReportFileArgument))
                    {
                        if (hasValue)
                        {
                            parsed.ReportFile = arguments[++i];
                        }
                    }
                    else if (Is(argument, AsyncArgument))
                    {
                        parsed.Async = true;
                    }
                    else if (Is(argument, DryRunArgument))
                    {
                        parsed.DryRun = true;
                    }
                    else
                    {
                        unnamed.Add(argument);
                    }
                }

                if (!parsed.ProfilesNamed)
                {
                    parsed.Profiles = unnamed;
                }

                return parsed;
            }

            private static bool Is(string argument, string name)
            {
                return argument.Equals(name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
