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
            var proxyInterface = ModuleDefinition.FindType("SexyProxy", "IProxy", sexyProxy);

            var targetTypes = ModuleDefinition.GetAllTypes().Where(x => x.IsDefined(proxyAttribute, true) || x.Interfaces.Any(y => y.CompareTo(proxyInterface))).ToArray();
            var methodInfoType = ModuleDefinition.Import(typeof(MethodInfo));

            var func2Type = ModuleDefinition.Import(typeof(Func<,>));
            var action1Type = ModuleDefinition.Import(typeof(Action<>));
            var objectArrayType = ModuleDefinition.Import(typeof(object[]));
            var taskType = ModuleDefinition.Import(typeof(Task));
            var invocationTType = ModuleDefinition.FindType("SexyProxy", "InvocationT`1", sexyProxy, "T");
            var asyncInvocationTType = ModuleDefinition.FindType("SexyProxy", "AsyncInvocationT`1", sexyProxy, "T").Resolve();
            var invocationHandlerType = ModuleDefinition.FindType("SexyProxy", "InvocationHandler", sexyProxy);
            var voidInvocationConstructor = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "VoidInvocation", sexyProxy).Resolve().GetConstructors().Single());
            var voidAsyncInvocationConstructor = ModuleDefinition.Import(ModuleDefinition.FindType("SexyProxy", "VoidAsyncInvocation", sexyProxy).Resolve().GetConstructors().Single());
            var voidInvokeMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "VoidInvoke"));
            var asyncVoidInvokeMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "VoidAsyncInvoke"));
            var invokeTMethod = ModuleDefinition.Import(invocationHandlerType.Resolve().Methods.Single(x => x.Name == "InvokeT"));
            var asyncInvokeTMethod = invocationHandlerType.Resolve().Methods.Single(x => x.Name == "AsyncInvokeT");
            var objectType = ModuleDefinition.Import(typeof(object));

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
                VoidInvokeMethod = voidInvokeMethod
            };

            foreach (var sourceType in targetTypes)
            {
                ClassWeaver classWeaver;

                bool isIntf = sourceType.IsInterface;
                if (isIntf)
                    classWeaver = new InterfaceClassWeaver(context, sourceType);
                else
                    classWeaver = new NonInterfaceClassWeaver(context, sourceType);

                classWeaver.Execute();
            }
        }
    }
}