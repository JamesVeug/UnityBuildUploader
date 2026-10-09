using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
    public static class ProcessUtils
    {
        // SteamCMD on Linux colours its output even when it is piped, e.g. "\e[0mLogging in user ...".
        private static readonly Regex AnsiEscapeCodes = new Regex(@"\x1B\[[0-9;?]*[A-Za-z]");

        private static string StripAnsiCodes(string text)
        {
            return string.IsNullOrEmpty(text) ? text : AnsiEscapeCodes.Replace(text, "");
        }

        /// <summary>Opens an interactive terminal. Arguments are individual values, not shell code.</summary>
        public static void ShowConsole(string path, params string[] arguments)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    throw new FileNotFoundException("Console executable was not found.", path);

                string fullPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(fullPath);
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    WorkingDirectory = directory,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                };
#if UNITY_EDITOR_WIN
                // Create a visible console when called from the Unity GUI process.
                startInfo.UseShellExecute = true;
                startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                startInfo.Arguments = "/d /s /k \"" + QuoteArgument(fullPath) + " " +
                                      string.Join(" ", arguments.Select(QuoteArgument)) + "\"";
#elif UNITY_EDITOR_OSX
                string command = "cd " + QuoteShellArgument(directory) + " && " +
                                 QuoteShellArgument(fullPath) + " " +
                                 string.Join(" ", arguments.Select(QuoteShellArgument));
                string script = "tell application \"Terminal\"\nactivate\ndo script \"" +
                                command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"\nend tell";
                startInfo.FileName = "/usr/bin/osascript";
                startInfo.Arguments = "-e " + QuoteArgument(script);
#elif UNITY_EDITOR_LINUX
                // These terminals all accept an executable and separate arguments after their separator.
                string[] terminals = { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" };
                foreach (string terminal in terminals)
                {
                    string executable = FindExecutable(terminal);
                    if (executable == null) continue;
                    startInfo.FileName = executable;
                    string separator = terminal == "gnome-terminal" || terminal == "xfce4-terminal" ? "--" : "-e";
                    // bash receives the SDK and its arguments as positional values, so shell characters
                    // in a path or argument cannot become commands. Leave a shell open when it exits.
                    startInfo.Arguments = separator + " /bin/bash -c " +
                        QuoteArgument("\"$@\"; exec /bin/bash -i") + " builduploader " +
                        QuoteArgument(fullPath) + " " + string.Join(" ", arguments.Select(QuoteArgument));
                    break;
                }
                if (string.IsNullOrEmpty(startInfo.FileName))
                    throw new InvalidOperationException("No supported terminal was found. Install x-terminal-emulator, gnome-terminal, konsole, xfce4-terminal or xterm.");
#else
                throw new PlatformNotSupportedException("Opening an SDK console is not supported on this editor platform.");
#endif
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Could not start the SDK console.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // ProcessStartInfo.Arguments uses command-line quoting, not shell quoting.
        internal static string QuoteArgument(string value)
        {
            var quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                quoted.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                quoted.Append(character);
                backslashes = 0;
            }
            quoted.Append('\\', backslashes * 2);
            return quoted.Append('"').ToString();
        }

        private static string QuoteShellArgument(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";

        private static string FindExecutable(string name)
        {
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            return null;
        }

        /// <summary>
        /// Makes sure the current user can execute path. Archives and copies from other drives often drop
        /// the exec bit, and Process.Start then fails with "Access denied". Always true on Windows.
        /// </summary>
        public static bool EnsureExecutable(string path, out string error)
        {
            error = null;
#if UNITY_EDITOR_WIN
            return true;
#else
            // Pass the path as $1 rather than splicing it into the script so spaces or quotes can't break it.
            ProcessResult test = RunSync("/bin/sh", "-c " + QuoteArgument("test -x \"$1\"") + " sh " + QuoteArgument(path), null);
            if (test.IsSuccessful)
            {
                return true;
            }

            // u+x keeps the existing bits, a numeric 755 would also widen group/other.
            ProcessResult chmod = RunSync("/bin/chmod", "u+x " + QuoteArgument(path), null);
            if (chmod.IsSuccessful)
            {
                return true;
            }

            error = $"{path} is not executable and chmod failed ({chmod.Errors.Trim()}). Run: chmod +x \"{path}\"";
            return false;
#endif
        }

        public readonly struct ProcessResult
        {
            public readonly bool IsSuccessful;
            public readonly string Output;
            public readonly string Errors;
            public readonly int ExitCode;
            
            private ProcessResult(bool isSuccessful, string output, string errors, int exitCode)
            {
                IsSuccessful = isSuccessful;
                Output = output;
                Errors = errors;
                ExitCode = exitCode;
            }
            
            public static ProcessResult Successful(string text)
            {
                return new ProcessResult(true, text, "", 0);
            }
            
            public static ProcessResult Failed(string reason)
            {
                return new ProcessResult(false, "", reason, -1);
            }
            
            /// <summary>
            /// The process ran and told us it failed. Keeps whatever it wrote - some tools explain
            /// themselves on stdout and still exit non-zero.
            /// </summary>
            public static ProcessResult Failed(string reason, string output, int exitCode)
            {
                return new ProcessResult(false, output, reason, exitCode);
            }
        }
        
        public static async Task<ProcessResult> RunTask(UploadTaskReport.StepResult result, string path, string args, Dictionary<string,string> environment, params string[] hideText)
        {
            hideText = hideText?.Where(value => !string.IsNullOrEmpty(value)).ToArray();
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = path;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.EnableRaisingEvents = true;
                
                    if (environment != null)
                    {
                        foreach (var keyValuePair in environment)
                        {
                            process.StartInfo.EnvironmentVariables[keyValuePair.Key] = keyValuePair.Value;
                        }
                    }
            
                    if (!process.Start())
                    {
                        string reason = "Could not start process: " + path;
                        result?.SetFailed(reason);
                        return ProcessResult.Failed(reason);
                    }

                    // Drain both pipes concurrently so a full stderr pipe cannot block stdout.
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    await Task.WhenAll(outputTask, errorTask);
                    string output = StripAnsiCodes(outputTask.Result).HideText(hideText);
                    string errors = StripAnsiCodes(errorTask.Result).HideText(hideText);
                
                    process.WaitForExit();
                    
                    result?.AddLog(output);
                    if (!string.IsNullOrEmpty(errors))
                    {
                        result?.AddError(errors);
                    }

                    if (process.ExitCode != 0)
                    {
                        string reason = string.IsNullOrEmpty(errors)
                            ? $"Process exited with code {process.ExitCode}: {path}"
                            : errors;
                        result?.SetFailed(reason);
                        return ProcessResult.Failed(reason, output, process.ExitCode);
                    }

                    return ProcessResult.Successful(output);
                }
            }
            catch (Exception ex)
            {
                result?.AddException(ex, hideText);
                result?.SetFailed(ex.Message.HideText(hideText));
                return ProcessResult.Failed(ex.Message.HideText(hideText));
            }
        }

        public static ProcessResult RunSync(string path, string args, string workingDirectory, int timeoutMs = 5000, Dictionary<string, string> environment = null, bool closeStandardInput = false)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = path;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.WorkingDirectory = workingDirectory ?? "";
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardInput = closeStandardInput;

                    if (environment != null)
                    {
                        foreach (KeyValuePair<string, string> variable in environment)
                        {
                            process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
                        }
                    }

                    if (!process.Start())
                    {
                        return ProcessResult.Failed("Could not start process: " + path);
                    }

                    // Some tools (steamcmd) prompt on stdin when they need input. Closing it makes them
                    // fail immediately with a parsable message instead of blocking until the timeout.
                    if (closeStandardInput)
                    {
                        process.StandardInput.Close();
                    }

                    // Read both streams as they arrive. ReadToEnd on one of them would only return when the
                    // process closes it, which never happens if the process hangs - the timeout below would
                    // never be reached.
                    StringBuilder outputBuilder = new StringBuilder();
                    StringBuilder errorBuilder = new StringBuilder();
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // Joined with a newline rather than AppendLine - callers parse tool
                            // output that uses '\n' itself.
                            lock (outputBuilder) outputBuilder.Append(e.Data).Append('\n');
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (errorBuilder) errorBuilder.Append(e.Data).Append('\n');
                        }
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        process.Kill();
                        return ProcessResult.Failed($"Timed out after {timeoutMs}ms: {path} {args}");
                    }

                    // Parameterless overload waits for the async readers to drain what is left.
                    process.WaitForExit();

                    string output;
                    string errors;
                    lock (outputBuilder) output = StripAnsiCodes(outputBuilder.ToString());
                    lock (errorBuilder) errors = StripAnsiCodes(errorBuilder.ToString());

                    if (process.ExitCode != 0)
                    {
                        return ProcessResult.Failed(string.IsNullOrEmpty(errors)
                            ? $"Exited with code {process.ExitCode}: {path} {args}"
                            : errors, output, process.ExitCode);
                    }

                    return ProcessResult.Successful(output);
                }
            }
            catch (Exception ex)
            {
                return ProcessResult.Failed(ex.Message);
            }
        }
    }
}
