using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            var proxyForAttribute = ModuleDefinition.FindType("SexyProxy", "ProxyForAttribute", sexyProxy);
            var doNotProxyAttribute = ModuleDefinition.FindType("SexyProxy", "DoNotProxyAttribute", sexyProxy);
            var originalMethodAttributeConstructor = ModuleDefinition.FindConstructor(ModuleDefinition.FindType("SexyProxy", "OriginalMethodAttribute", sexyProxy));
            var reverseProxyInterface = ModuleDefinition.FindType("SexyProxy", "IReverseProxy", sexyProxy);
            var proxyInterface = ModuleDefinition.FindType("SexyProxy", "IProxy", sexyProxy);

            var targetTypes = ModuleDefinition.GetAllTypes().Where(x => x.IsDefined(proxyAttribute, true) || reverseProxyInterface.IsAssignableFrom(x) || proxyInterface.IsAssignableFrom(x)).ToArray();

            // Get external proxy references
//            Debugger.Launch();
            var proxyFors = ModuleDefinition.Assembly.GetCustomAttributes(proxyForAttribute).Select(x => (TypeReference)x.ConstructorArguments.Single().Value).Select(x => x.Resolve()).ToArray();
            targetTypes = targetTypes.Concat(proxyFors).ToArray();

            var methodInfoType = ModuleDefinition.Import(typeof(MethodInfo));
            var propertyInfoType = ModuleDefinition.Import(typeof(PropertyInfo));

            var func2Type = ModuleDefinition.Import(typeof(Func<,>));
            var action1Type = ModuleDefinition.Import(typeof(Action<>));
            var objectArrayType = ModuleDefinition.Import(typeof(object[]));
            var taskType = ModuleDefinition.Import(typeof(Task));
            var invocationTType = ModuleDefinition.FindType("SexyProxy", "InvocationT`1", sexyProxy, "T");
            var asyncInvocationTType = ModuleDefinition.FindType("SexyProxy", "AsyncInvocationT`1", sexyProxy, "T");
            var invocationHandlerType = ModuleDefinition.FindType("SexyProxy", "InvocationHandler", sexyProxy);
            var invocationHandlerIsHandlerActive = ModuleDefinition.FindMethod(invocationHandlerType, "IsHandlerActive");
            var voidInvocationType = ModuleDefinition.FindType("SexyProxy", "VoidInvocation", sexyProxy);
            var voidInvocationConstructor = ModuleDefinition.FindConstructor(voidInvocationType);
            var voidAsyncInvocationType = ModuleDefinition.FindType("SexyProxy", "VoidAsyncInvocation", sexyProxy);
            var voidAsyncInvocationConstructor = ModuleDefinition.FindConstructor(voidAsyncInvocationType);
            var voidInvokeMethod = ModuleDefinition.FindMethod(invocationHandlerType, "VoidInvoke");
            var asyncVoidInvokeMethod = ModuleDefinition.FindMethod(invocationHandlerType, "VoidAsyncInvoke");
            var invokeTMethod = ModuleDefinition.FindMethod(invocationHandlerType, "InvokeT");
            var asyncInvokeTMethod = ModuleDefinition.FindMethod(invocationHandlerType, "AsyncInvokeT");
            var objectType = ModuleDefinition.Import(typeof(object));
            var proxyGetInvocationHandlerMethod = ModuleDefinition.FindGetter(proxyInterface, "InvocationHandler");
            var reverseProxyGetInvocationHandlerMethod = ModuleDefinition.FindGetter(reverseProxyInterface, "InvocationHandler");
            var invocationType = ModuleDefinition.FindType("SexyProxy", "Invocation", sexyProxy);
            var invocationGetArguments = ModuleDefinition.FindGetter(invocationType, "Arguments");
            var invocationGetProxy = ModuleDefinition.FindGetter(invocationType, "Proxy");
            var asyncTaskMethodBuilder = ModuleDefinition.Import(typeof(AsyncTaskMethodBuilder<>));
            var methodFinder = ModuleDefinition.FindType("SexyProxy.Reflection", "MethodFinder`1", sexyProxy, "T");
            var findMethod = ModuleDefinition.FindMethod(methodFinder, "FindMethod");
            var findProperty = ModuleDefinition.FindMethod(methodFinder, "FindProperty");

            var context = new WeaverContext
            {
                ModuleDefinition = ModuleDefinition,
                LogWarning = LogWarning,
                LogError = LogError,
                LogInfo = LogInfo,
                SexyProxy = sexyProxy,
                MethodInfoType = methodInfoType,
                PropertyInfoType = propertyInfoType,
                Action1Type = action1Type,
                AsyncInvocationTType = asyncInvocationTType,
                Func2Type = func2Type,
                InvocationTType = invocationTType,
                ObjectArrayType = objectArrayType,
                TaskType = taskType,
                AsyncInvokeTMethod = asyncInvokeTMethod,
                AsyncVoidInvokeMethod = asyncVoidInvokeMethod,
                InvocationHandlerType = invocationHandlerType,
                InvocationHandlerIsHandlerActive = invocationHandlerIsHandlerActive,
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
                InvocationGetProxy = invocationGetProxy,
                AsyncTaskMethodBuilder = asyncTaskMethodBuilder,
                MethodFinder = methodFinder,
                FindMethod = findMethod,
                FindProperty = findProperty,
                DoNotProxyAttribute = doNotProxyAttribute,
                OriginalMethodAttributeConstructor = originalMethodAttributeConstructor
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