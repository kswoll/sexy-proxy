using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace SexyProxy.Fody
{
    public class SexyPropertyWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }

        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public Action<string> LogInfo { get; set; }

        // Will log an error message to MSBuild. OPTIONAL
        public Action<string> LogError { get; set; }

        public Action<string> LogWarning { get; set; }

        public void Execute()
        {
            var foo = ModuleDefinition.Import(typeof(Task<string>));
            LogInfo(foo.GetTaskType().ToString());

            var sexyProxy = ModuleDefinition.FindAssembly("SexyProxy");
            if (sexyProxy == null)
            {
                LogError("Could not find assembly: SexyProxy (" + string.Join(", ", ModuleDefinition.AssemblyReferences.Select(x => x.Name)) + ")");
                return;
            }
            LogInfo($"{sexyProxy.Name} {sexyProxy.Version}");

            var proxyAttribute = ModuleDefinition.FindType("SexyProxy", "ProxyAttribute", sexyProxy);
            if (proxyAttribute == null)
                throw new Exception($"{nameof(proxyAttribute)} is null");

            var targetTypes = ModuleDefinition.GetAllTypes().Where(x => x.IsDefined(proxyAttribute, true)).ToArray();
            var methodInfoType = ModuleDefinition.Import(typeof(MethodInfo));

            var typeType = ModuleDefinition.Import(typeof(Type));
//            var getPropertyByName = ModuleDefinition.Import(typeType.Resolve().Methods.Single(x => x.Name == "GetProperty" && x.Parameters.Count == 1));
//            var getTypeFromTypeHandle = ModuleDefinition.Import(typeType.Resolve().Methods.Single(x => x.Name == "GetTypeFromHandle"));
            var func2Type = ModuleDefinition.Import(typeof(Func<,>));
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

            foreach (var sourceType in targetTypes)
            {
                bool isIntf = sourceType.IsInterface;
                var baseType = isIntf ? objectType : sourceType;
                var intfs = isIntf ? new[] { sourceType } : new TypeDefinition[0];
                var type = new TypeDefinition(sourceType.Namespace, sourceType.Name + "$Proxy", TypeAttributes.Public, baseType);
                foreach (var intf in intfs)
                    type.Interfaces.Add(intf);

                // Create target field
                var target = new FieldDefinition("$target", FieldAttributes.Private, sourceType);
                type.Fields.Add(target);

                // Create invocationHandler field
                var invocationHandler = new FieldDefinition("$invocationHandler", FieldAttributes.Private, invocationHandlerType);
                type.Fields.Add(invocationHandler);

                // Create constructor 
                var constructorWithTarget = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, ModuleDefinition.TypeSystem.Void);
                constructorWithTarget.Parameters.Add(new ParameterDefinition(sourceType));
                constructorWithTarget.Parameters.Add(new ParameterDefinition(invocationHandlerType));
                type.Methods.Add(constructorWithTarget);
                constructorWithTarget.Body = new MethodBody(constructorWithTarget);
                constructorWithTarget.Body.Emit(il =>
                {
                    il.EmitDefaultBaseConstructorCall(sourceType);
                    il.Emit(OpCodes.Ldarg_0);  // Put "this" on the stack for the subsequent stfld instruction way below
                    il.Emit(OpCodes.Ldarg_1);  // Put "target" argument on the stack

                    // If target is null, we will make the target ourself
                    if (!isIntf)
                    {
                        var targetNotNull = il.Create(OpCodes.Nop);
                        il.Emit(OpCodes.Dup);                      // Duplicate "target" since it will be consumed by the following branch instruction
                        il.Emit(OpCodes.Brtrue, targetNotNull);    // If target is not null, jump below
                        il.Emit(OpCodes.Pop);                      // Pop the null target off the stack
                        il.Emit(OpCodes.Ldarg_0);                  // Place "this" onto the stack (our new target)
                        il.Append(targetNotNull);
                    }

                    // Store whatever is on the stack inside the "target" field.  The value is either: 
                    // * The "target" argument passed in -- if not null.
                    // * If null and T is an interface type, then it is a struct that implements that interface and returns default values for each method
                    // * If null and T is not an interface type, then it is "this", where "proceed" will invoke the base implementation.
                    il.Emit(OpCodes.Stfld, target);                
    
                    il.Emit(OpCodes.Ldarg_0);                      // Load "this" for subsequent call to stfld
                    il.Emit(OpCodes.Ldarg_2);                      // Load the 2nd argument, which is the invocation handler
                    il.Emit(OpCodes.Stfld, invocationHandler);     // Store it in the invocationHandler field
                    il.Emit(OpCodes.Ret);                          // Return from the constructor (a ret call is always required, even for void methods and constructors)
                });

                var methods = sourceType.Methods.Where(x => !x.IsStatic);

                // If T is an interface type, we want to implement *all* the methods defined by the interface and its parent interfaces.
                if (isIntf)
                    methods = methods.Concat(sourceType.Interfaces.SelectMany(x => x.Resolve().Methods.Where(y => !y.IsStatic)));

                var staticConstructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, ModuleDefinition.TypeSystem.Void);
                staticConstructor.Body = new MethodBody(staticConstructor);
                sourceType.Methods.Add(staticConstructor);

                // Now implement/override all methods
                foreach (var methodInfo in methods)
                {
                    LogInfo($"{sourceType}.{methodInfo}");

                    var parameterInfos = methodInfo.Parameters;

                    // Finalize doesn't work if we try to proxy it and really, who cares?
                    if (methodInfo.Name == "Finalize" && parameterInfos.Count == 0 && methodInfo.DeclaringType.CompareTo(ModuleDefinition.TypeSystem.Object.Resolve()))
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
                    var method = new MethodDefinition(methodInfo.Name, methodAttributes, methodInfo.ReturnType);
                    foreach (var parameterType in parameterInfos.Select(x => x.ParameterType).ToArray())
                        method.Parameters.Add(new ParameterDefinition(parameterType));
                    type.Methods.Add(method);

                    // Initialize method info in static constructor
                    var methodInfoField = new FieldDefinition(methodInfo.Name + "__Info", FieldAttributes.Private | FieldAttributes.Static, methodInfoType);
                    type.Fields.Add(methodInfoField);

                    staticConstructor.Body.Emit(il =>
                    {
                        il.StoreMethodInfo(methodInfoField, methodInfo);
                    });

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
                    TypeDefinition proceedDelegateType;
                    TypeReference proceedReturnType;
                    OpCode proceedCall = isIntf ? OpCodes.Callvirt : OpCodes.Call;
                    MethodReference invocationConstructor;
                    MethodReference invokeMethod;

                    if (methodInfo.ReturnType.CompareTo(ModuleDefinition.TypeSystem.Void))
                    {
                        proceedDelegateType = ModuleDefinition.Import(typeof(Action<object[]>)).Resolve();
                        proceedReturnType = ModuleDefinition.Import(typeof(void));
                        invocationConstructor = voidInvocationConstructor;
                        invokeMethod = voidInvokeMethod;
                    }
                    else
                    {
                        proceedDelegateType = func2Type.MakeGenericInstanceType(objectArrayType, methodInfo.ReturnType).Resolve();
                        proceedReturnType = ModuleDefinition.Import(methodInfo.ReturnType);
                        if (!taskType.IsAssignableFrom(methodInfo.ReturnType))
                        {
                            invocationConstructor = ModuleDefinition.Import(invocationTType.MakeGenericInstanceType(methodInfo.ReturnType.Resolve()).Resolve().GetConstructors().First());
                            invokeMethod = ModuleDefinition.Import(invokeTMethod.MakeGenericMethod(methodInfo.ReturnType.Resolve()));
                        }
                        else if (methodInfo.ReturnType.IsTaskT())
                        {
                            var taskTType = methodInfo.ReturnType.GetTaskType();
                            invocationConstructor = ModuleDefinition.Import(asyncInvocationTType.MakeGenericInstanceType(taskTType).Resolve().GetConstructors().First());
                            invokeMethod = ModuleDefinition.Import(asyncInvokeTMethod.MakeGenericMethod(taskTType).Resolve());
                        }
                        else
                        {
                            invocationConstructor = voidAsyncInvocationConstructor;
                            invokeMethod = asyncVoidInvokeMethod;
                        }
                    }

                    var proceed = new MethodDefinition(methodInfo.Name + "$Proceed", MethodAttributes.Private, proceedReturnType);
                    proceed.Parameters.Add(new ParameterDefinition(objectArrayType));
                    proceed.Body = new MethodBody(proceed);
                    type.Methods.Add(proceed);

                    proceed.Body.Emit(il =>
                    {
                        if (!methodInfo.IsAbstract || isIntf)
                        {
                            // If T is an interface, then we want to check if target is null; if so, we want to just return the default value
                            if (isIntf)
                            {
                                var targetNotNull = il.Create(OpCodes.Nop);
                                il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                                il.Emit(OpCodes.Ldfld, target);              // Load "target" from "this"
                                il.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below
                                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);

                                il.Append(targetNotNull);                 // Mark where the previous branch instruction should jump to                        
                            }

                            // Load target for subsequent call
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldfld, target);

                            // Decompose array into arguments
                            for (int i = 0; i < parameterInfos.Count; i++)
                            {
                                il.Emit(OpCodes.Ldarg, 1);            // Push array 
                                il.Emit(OpCodes.Ldc_I4, i);                  // Push element index
                                il.Emit(OpCodes.Ldelem_Any, ModuleDefinition.TypeSystem.Object);     // Get element
                                if (parameterInfos[i].ParameterType.IsValueType)
                                    il.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType);
                            }

                            il.Emit(proceedCall, methodInfo);
                            il.Emit(OpCodes.Ret);                    
                        }
                        else
                        {
                            CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
                        }
                    });

                    // Implement method
                    method.Body = new MethodBody(method);
                    method.Body.Emit(il =>
                    {
                        // Load handler
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, invocationHandler);

                        // Load method info
                        il.Emit(OpCodes.Ldsfld, methodInfoField);

                        // Create arguments array
                        il.Emit(OpCodes.Ldc_I4, parameterInfos.Count);         // Array length
                        il.Emit(OpCodes.Newarr, ModuleDefinition.TypeSystem.Object);                // Instantiate array
                        for (var i = 0; i < parameterInfos.Count; i++)
                        {
                            il.Emit(OpCodes.Dup);                               // Duplicate array
                            il.Emit(OpCodes.Ldc_I4, i);                         // Array index
                            il.Emit(OpCodes.Ldarg, (short)(i + 1));             // Element value

                            if (parameterInfos[i].ParameterType.IsValueType)
                                il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);

                            il.Emit(OpCodes.Stelem_Any, ModuleDefinition.TypeSystem.Object);            // Set array at index to element value
                        }

                        // Load function pointer to proceed method
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldftn, proceed);
                        il.Emit(OpCodes.Newobj, ModuleDefinition.Import(proceedDelegateType.GetConstructors().First()));

                        // Instantiate Invocation
                        il.Emit(OpCodes.Newobj, invocationConstructor);

                        // Invoke handler
                        il.Emit(OpCodes.Callvirt, invokeMethod);

                        // Return
                        il.Emit(OpCodes.Ret);
                    });
                }

                staticConstructor.Body.Emit(il =>
                {
                    il.Emit(OpCodes.Ret);
                });
                ModuleDefinition.Types.Add(type);
            }
        }
    }
}