using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public class SexyProxyWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }

        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public Action<string> LogInfo { get; set; }

        // Will log an error message to MSBuild. OPTIONAL
        public Action<string> LogError { get; set; }

        public Action<string> LogWarning { get; set; }

        public void Execute()
        {
            var sexyProxy = ModuleDefinition.FindAssembly("SexyProxy");
            if (sexyProxy == null)
            {
                LogError("Could not find assembly: SexyProxy (" + string.Join(", ", ModuleDefinition.AssemblyReferences.Select(x => x.Name)) + ")");
                return;
            }

            var proxyAttribute = ModuleDefinition.FindType("SexyProxy", "ProxyAttribute", sexyProxy);
            if (proxyAttribute == null)
                throw new Exception($"{nameof(proxyAttribute)} is null");
            var reverseProxyInterface = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "IReverseProxy", sexyProxy));
            var proxyInterface = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "IProxy", sexyProxy));

            var targetTypes = ModuleDefinition.GetAllTypes().Where(x => x.IsDefined(proxyAttribute, true) || reverseProxyInterface.IsAssignableFrom(x) || proxyInterface.IsAssignableFrom(x)).ToArray();
            var methodInfoType = ModuleDefinition.Import(typeof(MethodInfo));

            var func2Type = ModuleDefinition.Import(typeof(Func<,>));
            var action1Type = ModuleDefinition.Import(typeof(Action<>));
            var objectArrayType = ModuleDefinition.Import(typeof(object[]));
            var taskType = ModuleDefinition.Import(typeof(Task));
            var invocationTType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "InvocationT`1", sexyProxy, "T"));
            var asyncInvocationTType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "AsyncInvocationT`1", sexyProxy, "T"));
            var invocationHandlerType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "InvocationHandler", sexyProxy));
            var voidInvocationType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "VoidInvocation", sexyProxy));
            var voidInvocationConstructor = ModuleDefinition.Import(voidInvocationType.Resolve().GetConstructors().Single());
            var voidAsyncInvocationType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "VoidAsyncInvocation", sexyProxy));
            var voidAsyncInvocationConstructor = ModuleDefinition.Import(voidAsyncInvocationType.Resolve().GetConstructors().Single());
            var voidInvokeMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "VoidInvoke"));
            var asyncVoidInvokeMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "VoidAsyncInvoke"));
            var invokeTMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "InvokeT"));
            var asyncInvokeTMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "AsyncInvokeT"));
            var objectType = ModuleDefinition.Import(typeof(object));
            var proxyGetInvocationHandlerMethod = ModuleDefinition.Import(proxyInterface.Resolve().Properties.Single(x => x.Name == "InvocationHandler").GetMethod);
            var reverseProxyGetInvocationHandlerMethod = ModuleDefinition.Import(reverseProxyInterface.Resolve().Properties.Single(x => x.Name == "InvocationHandler").GetMethod);
            var invocationType = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "Invocation", sexyProxy));
            var invocationGetArguments = ModuleDefinition.Import(invocationType.Resolve().Properties.Single(x => x.Name == "Arguments").GetMethod);
            var asyncTaskMethodBuilder = ModuleDefinition.Import(typeof(System.Runtime.CompilerServices.AsyncTaskMethodBuilder<>));

            var context = new WeaverContext
            {
                ModuleDefinition = ModuleDefinition,
                LogWarning = LogWarning,
                LogError = LogError,
                LogInfo = LogInfo,
                SexyProxy = sexyProxy,
                MethodInfoType = methodInfoType,
                Action1Type = action1Type,
                AsyncInvocationTType = asyncInvocationTType,
                Func2Type = func2Type,
                InvocationTType = invocationTType,
                ObjectArrayType = objectArrayType,
                TaskType = taskType,
                AsyncInvokeTMethod = asyncInvokeTMethod,
                AsyncVoidInvokeMethod = asyncVoidInvokeMethod,
                InvocationHandlerType = invocationHandlerType,
                InvokeTMethod = invokeTMethod,
                ObjectType = objectType,
                VoidAsyncInvocationConstructor = voidAsyncInvocationConstructor,
                VoidInvocationConstructor = voidInvocationConstructor,
                VoidInvokeMethod = voidInvokeMethod,
                ProxyGetInvocationHandlerMethod = proxyGetInvocationHandlerMethod,
                ReverseProxyGetInvocationHandlerMethod = reverseProxyGetInvocationHandlerMethod,
                InvocationType = invocationType,
                VoidInvocationType = voidInvocationType,
                VoidAsyncInvocationType = voidAsyncInvocationType,
                InvocationGetArguments = invocationGetArguments,
                AsyncTaskMethodBuilder = asyncTaskMethodBuilder
            };

            foreach (var sourceType in targetTypes)
            {
                LogInfo($"Emitting proxy for {sourceType.FullName}");
                ClassWeaver classWeaver;

                if (sourceType.IsInterface)
                    classWeaver = new InterfaceClassWeaver(context, sourceType);
                else if (proxyInterface.IsAssignableFrom(sourceType))
                    classWeaver = new InPlaceClassWeaver(context, sourceType);
                else if (reverseProxyInterface.IsAssignableFrom(sourceType))
                    classWeaver = new ReverseProxyClassWeaver(context, sourceType);
                else
                    classWeaver = new NonInterfaceClassWeaver(context, sourceType);

                classWeaver.Execute();
            }
        }
    }
}