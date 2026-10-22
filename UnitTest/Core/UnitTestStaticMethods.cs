using System;
using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Methods for InvokeStaticMethodAction to call, one per supported or unsupported shape.
    /// Calls counts every invocation; tests reset it.
    /// </summary>
    public static class UnitTestStaticMethods
    {
        public const string Path = "Wireframe.UnitTest.UnitTestStaticMethods.";
        public const string ExceptionMessage = "UnitTestStaticMethods threw";
        public const string ReportLog = "Logged by UnitTestStaticMethods";

        public static int Calls;

        public static void PublicVoid() { Calls++; }

        // ReSharper disable once UnusedMember.Local
        private static void PrivateVoid() { Calls++; }

        public static bool ReturnsTrue() { Calls++; return true; }

        public static bool ReturnsFalse() { Calls++; return false; }

        public static async Task ReturnsTask()
        {
            await Task.Yield();
            Calls++;
        }

        public static async Task<bool> ReturnsTaskTrue()
        {
            await Task.Yield();
            Calls++;
            return true;
        }

        public static async Task<bool> ReturnsTaskFalse()
        {
            await Task.Yield();
            Calls++;
            return false;
        }

        public static void LogsToReport(UploadTaskReport.StepResult stepResult)
        {
            Calls++;
            stepResult.AddLog(ReportLog);
        }

        public static void Throws()
        {
            Calls++;
            throw new InvalidOperationException(ExceptionMessage);
        }

        public static void Overloaded() { Calls++; }

        public static void Overloaded(UploadTaskReport.StepResult stepResult) { Calls++; }

        public static int ReturnsIntOne() { Calls++; return 1; }

        public static int ReturnsIntZero() { Calls++; return 0; }

        public static async Task<int> ReturnsTaskIntOne()
        {
            await Task.Yield();
            Calls++;
            return 1;
        }

        public static async Task<int> ReturnsTaskIntZero()
        {
            await Task.Yield();
            Calls++;
            return 0;
        }

        public static void TakesString(string text) { Calls++; }

        public static string ReturnsString() { Calls++; return "unsupported"; }

        public static Task<string> ReturnsTaskString() { Calls++; return Task.FromResult("unsupported"); }

        public static UniTask ReturnsUniTask() => new UniTask(ReturnsTask());

        public static UniTask<bool> ReturnsUniTaskFalse() => new UniTask<bool>(ReturnsTaskFalse());

        public static UniTask<int> ReturnsUniTaskIntOne() => new UniTask<int>(ReturnsTaskIntOne());

        public static UniTask<string> ReturnsUniTaskString() => new UniTask<string>(ReturnsTaskString());

        /// <summary>Counts once per editor update it is resumed in.</summary>
        public static IEnumerator RoutineYieldsFrames()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Calls++;
            }
        }

        public static IEnumerator RoutineWithNestedRoutineWaitAndTask()
        {
            yield return RoutineYieldsFrames();
            int frames = 0;
            yield return new WaitUntil(() => ++frames >= 3);
            yield return Task.Delay(1);
            Calls++;
        }

        public static IEnumerator RoutineYieldsWaitForSeconds()
        {
            yield return new WaitForSeconds(0.01f);
            Calls++;
        }

        public static IEnumerator RoutineThrows()
        {
            yield return null;
            Calls++;
            throw new InvalidOperationException(ExceptionMessage);
        }

        public class Nested
        {
            public static void Method() { Calls++; }

            public void InstanceMethod() { Calls++; }
        }
    }
}
