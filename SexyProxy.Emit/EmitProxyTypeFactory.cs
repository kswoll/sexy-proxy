using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using SexyProxy.Reflection;

namespace SexyProxy.Emit
{
    public class EmitProxyTypeFactory : IProxyTypeFactory
    {
        private static ConstructorInfo voidInvocationConstructor = typeof(VoidInvocation).GetConstructors()[0];
        private static ConstructorInfo voidAsyncInvocationConstructor = typeof(VoidAsyncInvocation).GetConstructors()[0];
        private static MethodInfo voidInvokeMethod = typeof(InvocationHandler).GetMethod("VoidInvoke");
        private static MethodInfo asyncVoidInvokeMethod = typeof(InvocationHandler).GetMethod("VoidAsyncInvoke");
        private static MethodInfo invokeTMethod = typeof(InvocationHandler).GetMethod("InvokeT");
        private static MethodInfo asyncInvokeTMethod = typeof(InvocationHandler).GetMethod("AsyncInvokeT");
        private static PropertyInfo invocationArguments = typeof(Invocation).GetProperty("Arguments");
        private static PropertyInfo invocationProxy = typeof(Invocation).GetProperty("Proxy");
        private static MethodInfo invocationHandlerIsHandlerActive = typeof(InvocationHandler).GetMethod(nameof(InvocationHandler.IsHandlerActive));

        public Type CreateProxyType(Type sourceType)
        {
            string assemblyName = sourceType.Namespace + "." + sourceType.Name.Replace('`', '$') + "$Proxy";

            bool isIntf = sourceType.IsInterface;
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName);

            var type = module.DefineType(assemblyName, TypeAttributes.Public);
            var targetType = sourceType;
            if (sourceType.ContainsGenericParameters)
            {
                var genericParameters = type.DefineGenericParameters(sourceType.GenericTypeArguments.Select(x => x.Name).ToArray());
                targetType = sourceType.MakeGenericType(genericParameters);
            }
            var baseType = isIntf ? typeof(object) : targetType;
            var intfs = isIntf ? new[] { targetType } : Type.EmptyTypes;
            type.SetParent(baseType);
            foreach (var intf in intfs)
                type.AddInterfaceImplementation(intf);

            // Create target field
            var target = type.DefineField("__target", targetType, FieldAttributes.Private);
            var invocationHandler = type.DefineField("__invocationHandler", typeof(InvocationHandler), FieldAttributes.Private);

            // Create constructor
            var constructorWithTarget = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { sourceType, typeof(InvocationHandler) });
            var constructorWithTargetIl = constructorWithTarget.GetILGenerator();
            constructorWithTargetIl.EmitDefaultBaseConstructorCall(sourceType);

            constructorWithTargetIl.Emit(OpCodes.Ldarg_0);  // Put "this" on the stack for the subsequent stfld instruction way below
            constructorWithTargetIl.Emit(OpCodes.Ldarg_1);  // Put "target" argument on the stack

            // If target is null, we will make the target ourself
            if (!isIntf)
            {
                var targetNotNull = constructorWithTargetIl.DefineLabel();
                constructorWithTargetIl.Emit(OpCodes.Dup);                      // Duplicate "target" since it will be consumed by the following branch instruction
                constructorWithTargetIl.Emit(OpCodes.Brtrue, targetNotNull);    // If target is not null, jump below
                constructorWithTargetIl.Emit(OpCodes.Pop);                      // Pop the null target off the stack
                constructorWithTargetIl.Emit(OpCodes.Ldarg_0);                  // Place "this" onto the stack (our new target)
                constructorWithTargetIl.MarkLabel(targetNotNull);               // Mark where the previous branch instruction should jump to
            }

            // Store whatever is on the stack inside the "target" field.  The value is either:
            // * The "target" argument passed in -- if not null.
            // * If null and T is an interface type, then it is a struct that implements that interface and returns default values for each method
            // * If null and T is not an interface type, then it is "this", where "proceed" will invoke the base implementation.
            constructorWithTargetIl.Emit(OpCodes.Stfld, target);

            constructorWithTargetIl.Emit(OpCodes.Ldarg_0);                      // Load "this" for subsequent call to stfld
            constructorWithTargetIl.Emit(OpCodes.Ldarg_2);                      // Load the 2nd argument, which is the invocation handler
            constructorWithTargetIl.Emit(OpCodes.Stfld, invocationHandler);     // Store it in the invocationHandler field
            constructorWithTargetIl.Emit(OpCodes.Ret);                          // Return from the constructor (a ret call is always required, even for void methods and constructors)

            // We use a static constructor to store all the method infos in static fiellds for fast access
            var staticConstructor = type.DefineConstructor(MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
            var staticIl = staticConstructor.GetILGenerator();

            // Now implement/override all methods
            var methods = sourceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).AsEnumerable();

            // If T is an interface type, we want to implement *all* the methods defined by the interface and its parent interfaces.
            if (isIntf)
                methods = methods.Concat(sourceType.GetInterfaces().SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)));

            // Now create an implementation for each method
            foreach (var methodInfo in methods)
            {
                var parameterInfos = methodInfo.GetParameters();

                // Finalize doesn't work if we try to proxy it and really, who cares?
                if (methodInfo.Name == "Finalize" && parameterInfos.Length == 0 && methodInfo.DeclaringType == typeof(object))
                    continue;

                // If we're not an interface and the method is not virtual, it's not possible to intercept
                if (!isIntf && methodInfo.IsFinal)
                    continue;

                MethodAttributes methodAttributes;
                if (isIntf)
                {
                    // The attributes required for the normal implementation of an interface method
                    methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
                }
                else
                {
                    // If we're overriding a method, these attributes are required
                    methodAttributes = methodInfo.IsPublic ? MethodAttributes.Public : MethodAttributes.Family;
                    methodAttributes |= MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;
                }

                // Define the actual method
                var method = type.DefineMethod(methodInfo.Name, methodAttributes, methodInfo.ReturnType, parameterInfos.Select(x => x.ParameterType).ToArray());

                // Initialize method info in static constructor
                var methodInfoField = type.DefineField(methodInfo.Name + "__Info", typeof(MethodInfo), FieldAttributes.Private | FieldAttributes.Static);
                staticIl.StoreMethodInfo(methodInfoField, methodInfo);

                FieldBuilder propertyInfoField = null;
                if (methodInfo.IsSpecialName)
                {
                    // Initialize the property info in static constructor
                    var propertyInfo = methodInfo.GetProperty();
                    if (propertyInfo != null)
                    {
                        propertyInfoField = type.DefineField($"{methodInfo.Name}${(propertyInfo.GetMethod == methodInfo ? "Get" : "Set")}Info", typeof(PropertyInfo), FieldAttributes.Private | FieldAttributes.Static);
                        staticIl.StorePropertyInfo(propertyInfoField, propertyInfo);
                    }
                }

                // Create proceed method (four different types).  The proceed method is what you may call in your invocation handler
                // in order to invoke the behavior that would have happened without the proxy.  The actual behavior depends on the value
                // of "target".  If it's not null, it calls the equivalent method on "target".  If it *is* null, then:
                //
                // * If it's an interface, it provides a default value
                // * If it's not an interface, and the method is not abstract, it calls the base implementation of that class.
                // * If it's abstract, then it provides a default value
                //
                // The actual implementation of proceed varies based on whether (where T represents the method's return type):
                //
                // * The method's return type is void               (Represented by Action)
                // * The method's return type is Task               (Represented by Func<Task>)
                // * The method's return type is Task<T>            (Represented by Func<Task<T>>)
                // * The method's return type is anything else      (Represented by Func<T>)
                Type proceedDelegateType;
                Type proceedReturnType;
                OpCode proceedCall = isIntf ? OpCodes.Callvirt : OpCodes.Call;
                ConstructorInfo invocationConstructor;
                MethodInfo invokeMethod;
                if (methodInfo.ReturnType == typeof(void))
                {
                    proceedDelegateType = typeof(Action<object[]>);
                    proceedReturnType = typeof(void);
                    invocationConstructor = voidInvocationConstructor;
                    invokeMethod = voidInvokeMethod;
                }
                else
                {
                    proceedDelegateType = typeof(Func<,>).MakeGenericType(typeof(object[]), methodInfo.ReturnType);
                    proceedReturnType = methodInfo.ReturnType;
                    if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                    {
                        invocationConstructor = typeof(InvocationT<>).MakeGenericType(methodInfo.ReturnType).GetConstructors()[0];
                        invokeMethod = invokeTMethod.MakeGenericMethod(methodInfo.ReturnType);
                    }
                    else if (methodInfo.ReturnType.IsTaskT())
                    {
                        var taskType = methodInfo.ReturnType.GetTaskType();
                        invocationConstructor = typeof(AsyncInvocationT<>).MakeGenericType(taskType).GetConstructors()[0];
                        invokeMethod = asyncInvokeTMethod.MakeGenericMethod(taskType);
                    }
                    else
                    {
                        invocationConstructor = voidAsyncInvocationConstructor;
                        invokeMethod = asyncVoidInvokeMethod;
                    }
                }
                var proceed = type.DefineMethod(methodInfo.Name + "$Proceed", MethodAttributes.Private | MethodAttributes.Static, proceedReturnType, new[] { typeof(Invocation) });
                var proceedIl = proceed.GetILGenerator();

                if (!methodInfo.IsAbstract || isIntf)
                {
                    // If T is an interface, then we want to check if target is null; if so, we want to just return the default value
                    if (isIntf)
                    {
                        var targetNotNull = proceedIl.DefineLabel();
                        proceedIl.Emit(OpCodes.Ldarg_0);                    // Load "invocation"
                        proceedIl.Emit(OpCodes.Call, invocationProxy.GetMethod);
                        proceedIl.Emit(OpCodes.Castclass, type);
                        proceedIl.Emit(OpCodes.Ldfld, target);              // Load "target" from "this"
                        proceedIl.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below
                        DefaultInterfaceImplementationFactory.CreateDefaultMethodImplementation(methodInfo, proceedIl);
                        proceedIl.MarkLabel(targetNotNull);                 // Mark where the previous branch instruction should jump to
                    }

                    // Load target for subsequent call
                    proceedIl.Emit(OpCodes.Ldarg_0);
                    proceedIl.Emit(OpCodes.Call, invocationProxy.GetMethod);
                    proceedIl.Emit(OpCodes.Castclass, type);
                    proceedIl.Emit(OpCodes.Ldfld, target);

                    // Decompose array into arguments
                    for (int i = 0; i < parameterInfos.Length; i++)
                    {
                        proceedIl.Emit(OpCodes.Ldarg_0);            // Push invocation
                        proceedIl.Emit(OpCodes.Call, invocationArguments.GetMethod);
                        proceedIl.Emit(OpCodes.Ldc_I4, i);                  // Push element index
                        proceedIl.Emit(OpCodes.Ldelem, typeof(object));     // Get element
                        if (parameterInfos[i].ParameterType.IsValueType || parameterInfos[i].ParameterType.IsGenericParameter)
                            proceedIl.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType);
                    }

                    proceedIl.Emit(proceedCall, methodInfo);
                    proceedIl.Emit(OpCodes.Ret);
                }
                else
                {
                    DefaultInterfaceImplementationFactory.CreateDefaultMethodImplementation(methodInfo, proceedIl);
                }

                // Implement method
                var il = method.GetILGenerator();

                // Allow the InvocationHandler to opt out of handling (for perf)
                var notOptedOut = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, invocationHandler);
                il.Emit(OpCodes.Ldarg_0);                                                       // Load this
                il.Emit(OpCodes.Ldsfld, methodInfoField);                                       // Load the MethodInfo onto the stack
                if (propertyInfoField == null)
                    il.Emit(OpCodes.Ldnull);                                                    // Not a property so load null onto the stack
                else
                    il.Emit(OpCodes.Ldsfld, propertyInfoField);                                 // Load the PropertyInfo onto the stack
                il.Emit(OpCodes.Call, invocationHandlerIsHandlerActive);                        // Call InvocationHandler.IsHandlerActive and leave the bool result on the stack
                il.Emit(OpCodes.Brtrue, notOptedOut);                                           // If they didn't opt out (returned true), jump to the normal interception logic below
                ImplementOptOut(il, methodInfo, proceedCall, isIntf, target);                   // They opted out, so do an implicit (and efficient) equivalent of proceed
                il.MarkLabel(notOptedOut);

                // Load handler
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, invocationHandler);

                // Load proxy
                il.Emit(OpCodes.Ldarg_0);

                // Load invocation handler
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, invocationHandler);

                // Load method info
                il.Emit(OpCodes.Ldsfld, methodInfoField);

                if (propertyInfoField == null)
                    il.Emit(OpCodes.Ldnull);
                else
                    il.Emit(OpCodes.Ldsfld, propertyInfoField);

                // Create arguments array
                il.Emit(OpCodes.Ldc_I4, parameterInfos.Length);         // Array length
                il.Emit(OpCodes.Newarr, typeof(object));                // Instantiate array
                for (var i = 0; i < parameterInfos.Length; i++)
                {
                    il.Emit(OpCodes.Dup);                               // Duplicate array
                    il.Emit(OpCodes.Ldc_I4, i);                         // Array index
                    il.Emit(OpCodes.Ldarg, (short)(i + 1));             // Element value

                    if (parameterInfos[i].ParameterType.IsValueType)
                        il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);

                    il.Emit(OpCodes.Stelem, typeof(object));            // Set array at index to element value
                }

                // Load function pointer to proceed method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, proceed);
                il.Emit(OpCodes.Newobj, proceedDelegateType.GetConstructors()[0]);

                // Instantiate Invocation
                il.Emit(OpCodes.Newobj, invocationConstructor);

                // Invoke handler
                il.Emit(OpCodes.Callvirt, invokeMethod);

                // Return
                il.Emit(OpCodes.Ret);
            }

            staticIl.Emit(OpCodes.Ret);

            var proxyType = type.CreateType();

            return proxyType;
        }

        private void ImplementOptOut(ILGenerator il, MethodInfo methodInfo, OpCode proceedCall, bool isIntf, FieldBuilder target)
        {
            var parameterInfos = methodInfo.GetParameters();

            // Load target for subsequent call
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, target);

            // Load the arguments onto the stack
            for (short i = 0; i < parameterInfos.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }

            il.Emit(proceedCall, methodInfo);
            il.Emit(OpCodes.Ret);
        }
    }
}
