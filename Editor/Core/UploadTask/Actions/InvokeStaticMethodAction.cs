using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Calls a static method from the project, such as an existing build script, so it can run before a build
    /// or after an upload.
    ///
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(InvokeStaticMethodAction), "actions",
        "Calls a static method in your project, such as an existing build script. Use the trigger On Task Started to run it before anything is built." +
        "\nThe method can be public or private, takes no parameters or one UploadTaskReport.StepResult to log into the report, and returns void, bool or int, a Task or UniTask of one of them, or an IEnumerator. Returning false, an int other than 1, or throwing fails the action, which stops the task unless Stop task if this fails is turned off." +
        "\nUniTask needs the UniTask package (2.0 or newer). An IEnumerator runs as a coroutine, one step per editor update, and can yield null, another IEnumerator (eg: WaitUntil or WaitForSecondsRealtime), an AsyncOperation or a Task.")]
    [UploadAction("Invoke Static Method")]
    public partial class InvokeStaticMethodAction : AUploadAction
    {
        [Wiki("Method", "The full name of the type, a dot, then the method. eg: MyCompany.BuildScripts.BuildContent, or LocalBuildScript.BuildAddressables for a type without a namespace. Nested types use +: MyCompany.Outer+Inner.Method. " +
                        "If two assemblies define the type, add the assembly after a comma: MyCompany.BuildScripts, Assembly-CSharp-Editor.BuildContent", 1)]
        private string m_MethodPath = "";

        // Resolving searches every loaded assembly and the GUI checks errors on every repaint, so keep the result.
        // A domain reload clears it, and only a domain reload changes the loaded types.
        private static readonly Dictionary<string, Resolution> s_resolutions = new Dictionary<string, Resolution>();

        // An int result succeeds when it is this, as a bool succeeds when it is true
        internal const int SuccessfulIntResult = 1;

        private const string UniTaskNamespace = "Cysharp.Threading.Tasks";

        public override bool DefaultStopTaskIfFailed => true;

        public string MethodPath
        {
            get => m_MethodPath;
            set => m_MethodPath = value ?? "";
        }

        public InvokeStaticMethodAction() : base()
        {
            // Required for reflection
        }

        public InvokeStaticMethodAction(string methodPath) : base()
        {
            MethodPath = methodPath;
        }

        public override async Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            Resolution resolution = Resolve(m_context.FormatString(m_MethodPath));
            if (!resolution.IsValid)
            {
                stepResult.SetFailed(resolution.Error);
                return false;
            }

            MethodInfo method = resolution.Method;
            stepResult.AddLog($"Invoking {resolution.DisplayName}");
            object[] arguments = resolution.TakesStepResult ? new object[] { stepResult } : new object[0];
            Stopwatch stopwatch = Stopwatch.StartNew();
            Type returnType = method.ReturnType;
            object returned;
            try
            {
                returned = method.Invoke(null, arguments);
                if (IsUniTask(returnType))
                {
                    returned = FindUniTaskAsTask(returnType).Invoke(null, new[] { returned });
                    returnType = TaskTypeFor(returnType);
                }

                if (IsTask(returnType))
                {
                    if (!(returned is Task task))
                    {
                        stepResult.SetFailed($"{resolution.DisplayName} returned null instead of a Task");
                        return false;
                    }

                    await task;
                }
                else if (returnType == typeof(IEnumerator))
                {
                    if (!(returned is IEnumerator routine))
                    {
                        stepResult.SetFailed($"{resolution.DisplayName} returned null instead of an IEnumerator");
                        return false;
                    }

                    string routineFailure = await RunCoroutine(routine);
                    if (routineFailure != null)
                    {
                        stepResult.SetFailed($"{resolution.DisplayName} {routineFailure}");
                        return false;
                    }
                }
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                return Failed(resolution, e.InnerException, stepResult);
            }
            catch (Exception e)
            {
                return Failed(resolution, e, stepResult);
            }

            string failure = ResultFailure(returnType, returned);
            if (failure != null)
            {
                stepResult.SetFailed($"{resolution.DisplayName} {failure}");
                return false;
            }

            stepResult.AddLog($"{resolution.DisplayName} completed in {stopwatch.ElapsedMilliseconds} ms");
            return true;
        }

        private static bool Failed(Resolution resolution, Exception exception, UploadTaskReport.StepResult stepResult)
        {
            stepResult.AddException(exception);
            stepResult.SetFailed($"{resolution.DisplayName} threw {exception.GetType().Name}: {exception.Message}");
            return false;
        }

        /// <summary>
        /// Runs a coroutine outside Play Mode, one step per editor update. Nested IEnumerators (including WaitUntil,
        /// WaitWhile and WaitForSecondsRealtime) run in place, and AsyncOperations and Tasks are waited for.
        /// Other yield instructions, such as WaitForSeconds, only work in Play Mode.
        /// </summary>
        /// <returns>Why the coroutine could not run, or null when it finished</returns>
        private static async Task<string> RunCoroutine(IEnumerator routine)
        {
            Stack<IEnumerator> running = new Stack<IEnumerator>();
            running.Push(routine);
            while (running.Count > 0)
            {
                IEnumerator current = running.Peek();
                if (!current.MoveNext())
                {
                    running.Pop();
                    continue;
                }

                object yielded = current.Current;
                if (yielded == null)
                {
                    await Task.Yield();
                }
                else if (yielded is IEnumerator nested)
                {
                    running.Push(nested);
                }
                else if (yielded is AsyncOperation operation)
                {
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }
                }
                else if (yielded is Task task)
                {
                    await task;
                }
                else
                {
                    return $"yielded {yielded.GetType().Name}, which only works in Play Mode. Yield null, an IEnumerator " +
                           "(eg: WaitUntil or WaitForSecondsRealtime), an AsyncOperation or a Task instead.";
                }
            }

            return null;
        }

        /// <summary>
        /// Why a finished method's result fails the action, or null if it succeeded. A Task is checked by the type
        /// of its result, and a coroutine has none. Resolve rejects every other return type, so they throw.
        /// </summary>
        private static string ResultFailure(Type returnType, object returned)
        {
            if (IsTask(returnType))
            {
                Type resultType = TaskResultType(returnType);
                if (resultType == typeof(void))
                {
                    return null;
                }
                if (resultType == typeof(bool))
                {
                    return ResultFailure(typeof(bool), ((Task<bool>)returned).Result);
                }
                if (resultType == typeof(int))
                {
                    return ResultFailure(typeof(int), ((Task<int>)returned).Result);
                }
            }
            else if (returnType == typeof(bool))
            {
                return (bool)returned ? null : "returned false";
            }
            else if (returnType == typeof(int))
            {
                return (int)returned == SuccessfulIntResult ? null : $"returned {returned} instead of {SuccessfulIntResult}";
            }
            else if (returnType == typeof(void) || returnType == typeof(IEnumerator))
            {
                return null;
            }

            throw new NotSupportedException($"Invoke Static Method cannot check a method that returns {TypeName(returnType)}.");
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            Resolution resolution = Resolve(m_context.FormatString(m_MethodPath));
            if (!resolution.IsValid)
            {
                errors.Add(new GUIContent(resolution.Error));
            }
        }

        public override void TryGetWarnings(List<GUIContent> warnings)
        {
            base.TryGetWarnings(warnings);

            warnings.Add(new GUIContent("A method that changes scripting defines or makes scripts recompile reloads the domain and loses this upload task.",
                "Unity reloads every script after a recompile, which stops a running upload task without a report. " +
                "See https://github.com/JamesVeug/UnityBuildUploader/issues/107"));
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "methodPath", m_MethodPath ?? "" },
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_MethodPath = data != null && data.TryGetValue("methodPath", out object methodPath) ? methodPath as string ?? "" : "";
        }

        /// <summary>
        /// The method a formatted path points at, or why it cannot be called.
        /// </summary>
        internal sealed class Resolution
        {
            public MethodInfo Method { get; private set; }
            public bool TakesStepResult { get; private set; }
            public string Error { get; private set; }

            public bool IsValid => Method != null;
            public string DisplayName => Method.DeclaringType.FullName + "." + Method.Name;
            public string Signature => $"static {TypeName(Method.ReturnType)} {DisplayName}({(TakesStepResult ? "StepResult" : "")})";
            public string AssemblyName => Method.DeclaringType.Assembly.GetName().Name;

            public static Resolution Found(MethodInfo method)
            {
                return new Resolution { Method = method, TakesStepResult = method.GetParameters().Length == 1 };
            }

            public static Resolution Failed(string error)
            {
                return new Resolution { Error = error };
            }
        }

        internal static Resolution Resolve(string path)
        {
            string key = path ?? "";
            if (!s_resolutions.TryGetValue(key, out Resolution resolution))
            {
                resolution = Resolve(key, AssemblyUtils.GetAssemblies());
                s_resolutions[key] = resolution;
            }
            return resolution;
        }

        /// <summary>
        /// Finds the method a path names among the given assemblies.
        /// Paths are Namespace.Type.Method, or Namespace.Type, AssemblyName.Method to pick the assembly.
        /// </summary>
        internal static Resolution Resolve(string path, IEnumerable<Assembly> assemblies)
        {
            path = (path ?? "").Trim();
            if (path.EndsWith("()"))
            {
                path = path.Substring(0, path.Length - 2).TrimEnd();
            }

            if (string.IsNullOrEmpty(path))
            {
                return Resolution.Failed("No method set. Enter the type and method, eg: MyNamespace.MyType.MyMethod");
            }

            // Method names cannot contain dots, so the method is everything after the last one
            int lastDot = path.LastIndexOf('.');
            if (lastDot <= 0 || lastDot == path.Length - 1)
            {
                return Resolution.Failed($"'{path}' is not a type and a method. Write the type's full name, a dot, then the method, eg: MyNamespace.MyType.MyMethod");
            }

            string typeName = path.Substring(0, lastDot).Trim();
            string methodName = path.Substring(lastDot + 1).Trim();
            string assemblyName = null;
            int comma = typeName.IndexOf(',');
            if (comma >= 0)
            {
                assemblyName = typeName.Substring(comma + 1).Trim();
                typeName = typeName.Substring(0, comma).Trim();
            }

            List<Type> types = new List<Type>();
            foreach (Assembly assembly in assemblies)
            {
                if (assemblyName != null && assembly.GetName().Name != assemblyName)
                {
                    continue;
                }

                Type type;
                try
                {
                    type = assembly.GetType(typeName, false);
                }
                catch (Exception)
                {
                    continue; // Assemblies that cannot be searched do not hold the user's script
                }

                if (type != null && !types.Contains(type))
                {
                    types.Add(type);
                }
            }

            if (types.Count == 0)
            {
                return Resolution.Failed(assemblyName != null
                    ? $"Type '{typeName}' not found in an assembly named '{assemblyName}'."
                    : $"Type '{typeName}' not found. Use its full name including the namespace, and + between nested types.");
            }

            if (types.Count > 1)
            {
                string names = string.Join(", ", types.Select(a => a.Assembly.GetName().Name));
                return Resolution.Failed($"Type '{typeName}' is defined in {types.Count} assemblies ({names}). " +
                                         $"Add the assembly after a comma, eg: {typeName}, {types[0].Assembly.GetName().Name}.{methodName}");
            }

            return ResolveMethod(types[0], methodName);
        }

        private static Resolution ResolveMethod(Type type, string methodName)
        {
            if (type.ContainsGenericParameters)
            {
                return Resolution.Failed($"'{type.FullName}' is a generic type. Call a method on a type that is not generic.");
            }

            MethodInfo[] named = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(a => a.Name == methodName).ToArray();
            if (named.Length == 0)
            {
                bool instanceMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(a => a.Name == methodName);
                return Resolution.Failed(instanceMethod
                    ? $"{type.FullName}.{methodName} is not static. Only static methods can be invoked."
                    : $"{type.FullName} has no static method named '{methodName}'.");
            }

            MethodInfo[] callable = named.Where(HasSupportedParameters).ToArray();
            if (callable.Length == 0)
            {
                return Resolution.Failed($"{type.FullName}.{methodName} must take no parameters or one UploadTaskReport.StepResult. Found: {Signatures(named)}");
            }

            if (callable.Length > 1)
            {
                return Resolution.Failed($"{type.FullName}.{methodName} has {callable.Length} overloads that could be invoked: {Signatures(callable)}. Keep one, or invoke a method with its own name.");
            }

            MethodInfo method = callable[0];
            if (IsUniTask(method.ReturnType) && FindUniTaskAsTask(method.ReturnType) == null)
            {
                return Resolution.Failed($"{type.FullName}.{methodName} returns {TypeName(method.ReturnType)}, but UniTaskExtensions.AsTask was not found next to it. Use UniTask 2.0 or newer.");
            }

            if (!HasSupportedReturnType(method))
            {
                return Resolution.Failed($"{type.FullName}.{methodName} returns {TypeName(method.ReturnType)}. Return void, bool or int, a Task or UniTask of one of them, " +
                                         "or an IEnumerator so its result is not ignored.");
            }

            return Resolution.Found(method);
        }

        private static bool HasSupportedParameters(MethodInfo method)
        {
            if (method.ContainsGenericParameters)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 0 ||
                   (parameters.Length == 1 && parameters[0].ParameterType == typeof(UploadTaskReport.StepResult));
        }

        /// <summary>
        /// void, bool or int, a Task or UniTask of one of them, or an IEnumerator. Execute handles exactly these, so
        /// TryGetErrors reports any other return type before the task starts.
        /// </summary>
        private static bool HasSupportedReturnType(MethodInfo method)
        {
            Type returnType = IsUniTask(method.ReturnType) ? TaskTypeFor(method.ReturnType) : method.ReturnType;
            if (returnType == typeof(IEnumerator))
            {
                return true;
            }

            Type resultType = IsTask(returnType) ? TaskResultType(returnType) : returnType;
            return resultType == typeof(void) || resultType == typeof(bool) || resultType == typeof(int);
        }

        /// <summary>
        /// UniTask (com.cysharp.unitask) is optional, so it is recognised by name rather than referenced
        /// </summary>
        private static bool IsUniTask(Type type)
        {
            Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return definition.Namespace == UniTaskNamespace && (definition.Name == "UniTask" || definition.Name == "UniTask`1");
        }

        /// <summary>
        /// The Task a UniTask becomes with AsTask: Task for UniTask, Task&lt;T&gt; for UniTask&lt;T&gt;
        /// </summary>
        private static Type TaskTypeFor(Type uniTaskType)
        {
            return uniTaskType.IsGenericType ? typeof(Task<>).MakeGenericType(uniTaskType.GetGenericArguments()) : typeof(Task);
        }

        /// <summary>
        /// UniTaskExtensions.AsTask for this UniTask type, from the assembly that defines it, or null if it has none
        /// </summary>
        private static MethodInfo FindUniTaskAsTask(Type uniTaskType)
        {
            Type extensions = uniTaskType.Assembly.GetType(UniTaskNamespace + ".UniTaskExtensions", false);
            if (extensions == null)
            {
                return null;
            }

            foreach (MethodInfo candidate in extensions.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name != "AsTask" || parameters.Length != 1)
                {
                    continue;
                }

                Type parameter = parameters[0].ParameterType;
                if (!uniTaskType.IsGenericType && parameter == uniTaskType)
                {
                    return candidate;
                }

                if (uniTaskType.IsGenericType && candidate.IsGenericMethodDefinition && parameter.IsGenericType &&
                    parameter.GetGenericTypeDefinition() == uniTaskType.GetGenericTypeDefinition())
                {
                    return candidate.MakeGenericMethod(uniTaskType.GetGenericArguments());
                }
            }

            return null;
        }

        private static bool IsTask(Type returnType)
        {
            return returnType == typeof(Task) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));
        }

        /// <summary>
        /// The type of a Task's result: void for Task, T for Task&lt;T&gt;
        /// </summary>
        private static Type TaskResultType(Type taskType)
        {
            return taskType == typeof(Task) ? typeof(void) : taskType.GetGenericArguments()[0];
        }

        private static string Signatures(IEnumerable<MethodInfo> methods)
        {
            return string.Join(", ", methods.Select(a => $"{TypeName(a.ReturnType)} {a.Name}({string.Join(", ", a.GetParameters().Select(p => TypeName(p.ParameterType)))})"));
        }

        private static string TypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (!type.IsGenericType) return type.Name;

            int arity = type.Name.IndexOf('`');
            string name = arity > 0 ? type.Name.Substring(0, arity) : type.Name;
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        }
    }
}
