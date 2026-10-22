using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Methods = Wireframe.UnitTest.UnitTestStaticMethods;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// InvokeStaticMethodAction finds a static method by its path, calls it, and fails on false, exceptions and
    /// paths it cannot call. The end-to-end run with a task is in UploadTaskTests.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class InvokeStaticMethodActionTests
    {
        private const double Timeout = 30;

        [SetUp]
        public void SetUp()
        {
            Methods.Calls = 0;
        }

        [Test]
        public void PublicVoidMethodIsInvoked()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();

            Assert.IsTrue(Invoke(Methods.Path + "PublicVoid", result), result.FailReason);
            Assert.AreEqual(1, Methods.Calls);
            Assert.IsTrue(result.Logs.Any(log => log.Message == "Invoking " + Methods.Path + "PublicVoid"), "The report says which method ran.");
        }

        [Test]
        public void PrivateMethodIsInvoked()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();

            Assert.IsTrue(Invoke(Methods.Path + "PrivateVoid", result), result.FailReason);
            Assert.AreEqual(1, Methods.Calls);
        }

        [Test]
        public void BoolResultDecidesSuccess()
        {
            Assert.IsTrue(Invoke(Methods.Path + "ReturnsTrue", TestSupport.Report()));

            UploadTaskReport.StepResult result = TestSupport.Report();
            Assert.IsFalse(Invoke(Methods.Path + "ReturnsFalse", result));
            Assert.IsFalse(result.Successful);
            StringAssert.Contains("ReturnsFalse returned false", result.FailReason);
            Assert.AreEqual(2, Methods.Calls);
        }

        [UnityTest]
        public IEnumerator TaskIsAwaited()
        {
            var action = new InvokeStaticMethodAction(Methods.Path + "ReturnsTask");
            UploadTaskReport.StepResult result = TestSupport.Report();

            Task<bool> execute = action.Execute(result);
            yield return TestSupport.Await(execute, Timeout);

            Assert.IsTrue(execute.Result, result.FailReason);
            Assert.AreEqual(1, Methods.Calls, "The action must finish only after the task does.");
        }

        [UnityTest]
        public IEnumerator TaskBoolResultDecidesSuccess()
        {
            UploadTaskReport.StepResult succeeded = TestSupport.Report();
            Task<bool> trueTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsTaskTrue").Execute(succeeded);
            yield return TestSupport.Await(trueTask, Timeout);
            Assert.IsTrue(trueTask.Result, succeeded.FailReason);

            UploadTaskReport.StepResult failed = TestSupport.Report();
            Task<bool> falseTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsTaskFalse").Execute(failed);
            yield return TestSupport.Await(falseTask, Timeout);
            Assert.IsFalse(falseTask.Result);
            StringAssert.Contains("ReturnsTaskFalse returned false", failed.FailReason);
            Assert.AreEqual(2, Methods.Calls);
        }

        [UnityTest]
        public IEnumerator UniTaskIsAwaitedAndItsResultChecked()
        {
            UploadTaskReport.StepResult succeeded = TestSupport.Report();
            Task<bool> uniTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsUniTask").Execute(succeeded);
            yield return TestSupport.Await(uniTask, Timeout);
            Assert.IsTrue(uniTask.Result, succeeded.FailReason);
            Assert.AreEqual(1, Methods.Calls, "The action must finish only after the UniTask does.");

            UploadTaskReport.StepResult one = TestSupport.Report();
            Task<bool> intTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsUniTaskIntOne").Execute(one);
            yield return TestSupport.Await(intTask, Timeout);
            Assert.IsTrue(intTask.Result, one.FailReason);

            UploadTaskReport.StepResult failed = TestSupport.Report();
            Task<bool> falseTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsUniTaskFalse").Execute(failed);
            yield return TestSupport.Await(falseTask, Timeout);
            Assert.IsFalse(falseTask.Result);
            StringAssert.Contains("ReturnsUniTaskFalse returned false", failed.FailReason);
        }

        [UnityTest]
        public IEnumerator CoroutineRunsOneStepPerEditorUpdate()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();
            Task<bool> execute = new InvokeStaticMethodAction(Methods.Path + "RoutineYieldsFrames").Execute(result);
            Assert.IsFalse(execute.IsCompleted, "A coroutine that yields must not finish straight away.");
            Assert.AreEqual(0, Methods.Calls);

            yield return TestSupport.Await(execute, Timeout);

            Assert.IsTrue(execute.Result, result.FailReason);
            Assert.AreEqual(3, Methods.Calls);
        }

        [UnityTest]
        public IEnumerator CoroutineWaitsForNestedRoutinesInstructionsAndTasks()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();
            Task<bool> execute = new InvokeStaticMethodAction(Methods.Path + "RoutineWithNestedRoutineWaitAndTask").Execute(result);
            yield return TestSupport.Await(execute, Timeout);

            Assert.IsTrue(execute.Result, result.FailReason);
            Assert.AreEqual(4, Methods.Calls, "The nested routine counts 3, then the routine continues after WaitUntil and the Task.");
        }

        [UnityTest]
        public IEnumerator CoroutineYieldingAPlayModeInstructionFails()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();
            Task<bool> execute = new InvokeStaticMethodAction(Methods.Path + "RoutineYieldsWaitForSeconds").Execute(result);
            yield return TestSupport.Await(execute, Timeout);

            Assert.IsFalse(execute.Result);
            StringAssert.Contains("RoutineYieldsWaitForSeconds yielded WaitForSeconds, which only works in Play Mode", result.FailReason);
            Assert.AreEqual(0, Methods.Calls, "The coroutine must stop at the instruction it cannot run.");
        }

        [UnityTest]
        public IEnumerator CoroutineExceptionFailsTheAction()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();
            Task<bool> execute = new InvokeStaticMethodAction(Methods.Path + "RoutineThrows").Execute(result);
            yield return TestSupport.Await(execute, Timeout);

            Assert.IsFalse(execute.Result);
            StringAssert.Contains("InvalidOperationException", result.FailReason);
            StringAssert.Contains(Methods.ExceptionMessage, result.FailReason);
            Assert.AreEqual(1, Methods.Calls);
        }

        [Test]
        public void UniTaskWithoutAsTaskIsAValidationError()
        {
            // An assembly with a UniTask type but no UniTaskExtensions, as an old or unusual UniTask would be
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BuildUploaderUnitTestOldUniTask"), AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule("BuildUploaderUnitTestOldUniTask");
            System.Type uniTask = module.DefineType("Cysharp.Threading.Tasks.UniTask",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, typeof(System.ValueType)).CreateType();
            TypeBuilder holder = module.DefineType("Wireframe.UnitTest.Dynamic.OldUniTask", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            ILGenerator il = holder.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, uniTask, System.Type.EmptyTypes).GetILGenerator();
            LocalBuilder value = il.DeclareLocal(uniTask);
            il.Emit(OpCodes.Ldloca_S, value);
            il.Emit(OpCodes.Initobj, uniTask);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            holder.CreateType();

            InvokeStaticMethodAction.Resolution resolution = InvokeStaticMethodAction.Resolve("Wireframe.UnitTest.Dynamic.OldUniTask.Run", new Assembly[] { assembly });

            Assert.IsFalse(resolution.IsValid);
            StringAssert.Contains("UniTaskExtensions.AsTask was not found", resolution.Error);
        }

        [Test]
        public void IntResultSucceedsOnlyWhenOne()
        {
            Assert.IsTrue(Invoke(Methods.Path + "ReturnsIntOne", TestSupport.Report()));

            UploadTaskReport.StepResult result = TestSupport.Report();
            Assert.IsFalse(Invoke(Methods.Path + "ReturnsIntZero", result));
            StringAssert.Contains("ReturnsIntZero returned 0 instead of 1", result.FailReason);
            Assert.AreEqual(2, Methods.Calls);
        }

        [UnityTest]
        public IEnumerator TaskIntResultSucceedsOnlyWhenOne()
        {
            UploadTaskReport.StepResult succeeded = TestSupport.Report();
            Task<bool> oneTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsTaskIntOne").Execute(succeeded);
            yield return TestSupport.Await(oneTask, Timeout);
            Assert.IsTrue(oneTask.Result, succeeded.FailReason);

            UploadTaskReport.StepResult failed = TestSupport.Report();
            Task<bool> zeroTask = new InvokeStaticMethodAction(Methods.Path + "ReturnsTaskIntZero").Execute(failed);
            yield return TestSupport.Await(zeroTask, Timeout);
            Assert.IsFalse(zeroTask.Result);
            StringAssert.Contains("ReturnsTaskIntZero returned 0 instead of 1", failed.FailReason);
            Assert.AreEqual(2, Methods.Calls);
        }

        [Test]
        public void StepResultParameterGetsTheActionsReport()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();

            Assert.IsTrue(Invoke(Methods.Path + "LogsToReport", result), result.FailReason);
            Assert.IsTrue(result.Logs.Any(log => log.Message == Methods.ReportLog));
        }

        [Test]
        public void ExceptionFailsTheActionWithTheMethodsException()
        {
            UploadTaskReport.StepResult result = TestSupport.Report();

            Assert.IsFalse(Invoke(Methods.Path + "Throws", result));
            StringAssert.Contains("InvalidOperationException", result.FailReason, "TargetInvocationException must be unwrapped.");
            StringAssert.Contains(Methods.ExceptionMessage, result.FailReason);
            Assert.IsTrue(result.Logs.Any(log => log.Type == UploadTaskReport.StepResult.Log.LogType.Exception));
        }

        [Test]
        public void NestedTypesAndNamedAssembliesResolve()
        {
            Assert.IsTrue(Invoke("Wireframe.UnitTest.UnitTestStaticMethods+Nested.Method", TestSupport.Report()));
            Assert.IsTrue(Invoke("Wireframe.UnitTest.UnitTestStaticMethods, BuildUploader.UnitTest.Core.PublicVoid", TestSupport.Report()));
            Assert.IsTrue(Invoke(Methods.Path + "PublicVoid()", TestSupport.Report()), "A trailing () is allowed.");
            Assert.AreEqual(3, Methods.Calls);
        }

        [Test]
        public void PathsThatCannotBeInvokedAreValidationErrors()
        {
            AssertError("", "No method set");
            AssertError("PublicVoid", "is not a type and a method");
            AssertError("Wireframe.UnitTest.NoSuchType.Method", "Type 'Wireframe.UnitTest.NoSuchType' not found");
            AssertError("Wireframe.UnitTest.UnitTestStaticMethods, NoSuchAssembly.PublicVoid", "not found in an assembly named 'NoSuchAssembly'");
            AssertError(Methods.Path + "Missing", "has no static method named 'Missing'");
            AssertError("Wireframe.UnitTest.UnitTestStaticMethods+Nested.InstanceMethod", "is not static");
            AssertError(Methods.Path + "TakesString", "must take no parameters or one UploadTaskReport.StepResult");
            AssertError(Methods.Path + "Overloaded", "has 2 overloads");
            AssertError(Methods.Path + "ReturnsString", "returns string");
            AssertError(Methods.Path + "ReturnsTaskString", "returns Task<string>");
            AssertError(Methods.Path + "ReturnsUniTaskString", "returns UniTask<string>");
            Assert.AreEqual(0, Methods.Calls, "Validation must never invoke the method.");
        }

        [Test]
        public void EverySupportedReturnTypePassesValidation()
        {
            foreach (string method in new[] { "PublicVoid", "ReturnsTrue", "ReturnsIntOne", "ReturnsTask", "ReturnsTaskTrue", "ReturnsTaskIntOne",
                         "ReturnsUniTask", "ReturnsUniTaskFalse", "ReturnsUniTaskIntOne", "RoutineYieldsFrames" })
            {
                var errors = new List<GUIContent>();
                new InvokeStaticMethodAction(Methods.Path + method).TryGetErrors(errors);
                CollectionAssert.IsEmpty(errors.Select(e => e.text), method + " must be accepted before the task starts.");
            }
            Assert.AreEqual(0, Methods.Calls, "Validation must never invoke the method.");
        }

        [Test]
        public void TypeInTwoAssembliesNeedsTheAssemblyName()
        {
            const string typeName = "Wireframe.UnitTest.Dynamic.DefinedTwice";
            Assembly first = DefineAssemblyWithType("BuildUploaderUnitTestFirst", typeName);
            Assembly second = DefineAssemblyWithType("BuildUploaderUnitTestSecond", typeName);
            var assemblies = new[] { first, second };

            InvokeStaticMethodAction.Resolution ambiguous = InvokeStaticMethodAction.Resolve(typeName + ".Run", assemblies);
            Assert.IsFalse(ambiguous.IsValid);
            StringAssert.Contains("defined in 2 assemblies (BuildUploaderUnitTestFirst, BuildUploaderUnitTestSecond)", ambiguous.Error);

            InvokeStaticMethodAction.Resolution named = InvokeStaticMethodAction.Resolve(typeName + ", BuildUploaderUnitTestSecond.Run", assemblies);
            Assert.IsTrue(named.IsValid, named.Error);
            Assert.AreEqual(second, named.Method.DeclaringType.Assembly);
        }

        [Test]
        public void MethodPathIsSavedAndStopsTheTaskByDefault()
        {
            var action = new InvokeStaticMethodAction(Methods.Path + "PublicVoid");
            var reloaded = new InvokeStaticMethodAction();
            reloaded.Deserialize(JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(action.Serialize())));

            Assert.AreEqual(Methods.Path + "PublicVoid", reloaded.MethodPath);
            Assert.IsTrue(new UploadConfig.UploadActionData(reloaded).StopTaskIfFailed,
                "A failed method usually means the build would be wrong, so it stops the task unless the user says otherwise.");
        }

        private static bool Invoke(string path, UploadTaskReport.StepResult result)
        {
            Task<bool> execute = new InvokeStaticMethodAction(path).Execute(result);
            Assert.IsTrue(execute.IsCompleted, "A method that does not return a Task completes straight away.");
            return execute.Result;
        }

        private static void AssertError(string path, string expected)
        {
            var errors = new List<GUIContent>();
            new InvokeStaticMethodAction(path).TryGetErrors(errors);

            Assert.AreEqual(1, errors.Count, "Expected one error for '" + path + "': " + string.Join(" | ", errors.Select(e => e.text)));
            StringAssert.Contains(expected, errors[0].text, "Path: " + path);
        }

        private static Assembly DefineAssemblyWithType(string assemblyName, string typeName)
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule(assemblyName);
            TypeBuilder type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            MethodBuilder method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), System.Type.EmptyTypes);
            method.GetILGenerator().Emit(OpCodes.Ret);
            type.CreateType();
            return assembly;
        }
    }
}
