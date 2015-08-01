using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public abstract class TargetedClassWeaver : ClassWeaver
    {
        public FieldDefinition Target { get; private set; }
        public FieldDefinition InvocationHandler { get; private set; }

        public TargetedClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected virtual TypeReference BaseType => SourceType;

        protected virtual TypeReference[] GetInterfaces()
        {
            return new TypeReference[0];
        }

        protected override TypeDefinition GetProxyType()
        {
            var type = new TypeDefinition(SourceType.Namespace, SourceType.Name + "$Proxy", TypeAttributes.Public, BaseType);
            var intfs = GetInterfaces();
            foreach (var intf in intfs)
                type.Interfaces.Add(intf);
            return type;
        }

        protected override void InitializeProxyType()
        {
            base.InitializeProxyType();

            // Create target field
            Target = new FieldDefinition("$target", FieldAttributes.Private, SourceType);
            ProxyType.Fields.Add(Target);

            // Create invocationHandler field
            InvocationHandler = new FieldDefinition("$invocationHandler", FieldAttributes.Private, Context.InvocationHandlerType);
            ProxyType.Fields.Add(InvocationHandler);

            CreateConstructor();
        }

        protected virtual void PrepareTargetForConstructor(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_1);  // Put "target" argument on the stack
        }

        protected virtual void CreateConstructor()
        {
            // Create constructor 
            var constructorWithTarget = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, Context.ModuleDefinition.TypeSystem.Void);
            constructorWithTarget.Parameters.Add(new ParameterDefinition(SourceType));
            constructorWithTarget.Parameters.Add(new ParameterDefinition(Context.InvocationHandlerType));
            ProxyType.Methods.Add(constructorWithTarget);
            constructorWithTarget.Body = new MethodBody(constructorWithTarget);
            constructorWithTarget.Body.Emit(il =>
            {
                il.EmitDefaultBaseConstructorCall(SourceType);
                il.Emit(OpCodes.Ldarg_0);  // Put "this" on the stack for the subsequent stfld instruction way below

                PrepareTargetForConstructor(il);

                // Store whatever is on the stack inside the "target" field.  The value is either: 
                // * The "target" argument passed in -- if not null.
                // * If null and T is an interface type, then it is a struct that implements that interface and returns default values for each method
                // * If null and T is not an interface type, then it is "this", where "proceed" will invoke the base implementation.
                il.Emit(OpCodes.Stfld, Target);                
    
                il.Emit(OpCodes.Ldarg_0);                      // Load "this" for subsequent call to stfld
                il.Emit(OpCodes.Ldarg_2);                      // Load the 2nd argument, which is the invocation handler
                il.Emit(OpCodes.Stfld, InvocationHandler);     // Store it in the invocationHandler field
                il.Emit(OpCodes.Ret);                          // Return from the constructor (a ret call is always required, even for void methods and constructors)
            });
        }

        protected override MethodDefinition GetStaticConstructor()
        {
            var staticConstructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, Context.ModuleDefinition.TypeSystem.Void);
            staticConstructor.Body = new MethodBody(staticConstructor);
            ProxyType.Methods.Add(staticConstructor);
            return staticConstructor;
        }

        protected override void Finish()
        {
            base.Finish();

            Context.ModuleDefinition.Types.Add(ProxyType);
        }

        protected abstract MethodAttributes GetMethodAttributes(MethodDefinition methodInfo);
        protected abstract OpCode GetProceedCallOpCode();

        protected virtual void ImplementProceed(MethodDefinition methodInfo, ILProcessor il)
        {
            var parameterInfos = methodInfo.Parameters;

            // Load target for subsequent call
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, Target);

            // Decompose array into arguments
            for (int i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Ldarg, 1);                                           // Push array 
                il.Emit(OpCodes.Ldc_I4, i);                                          // Push element index
                il.Emit(OpCodes.Ldelem_Any, Context.ModuleDefinition.TypeSystem.Object);     // Get element
                if (parameterInfos[i].ParameterType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType);
                else
                    il.Emit(OpCodes.Castclass, parameterInfos[i].ParameterType);
            }

            il.Emit(GetProceedCallOpCode(), methodInfo);
            il.Emit(OpCodes.Ret);                    
        }

        protected override void ProxyMethod(MethodDefinition methodInfo)
        {
//                    var isImplemented = !isIntf && methodInfo.IsFinal;
            MethodAttributes methodAttributes = GetMethodAttributes(methodInfo);

            // Define the actual method
            var parameterInfos = methodInfo.Parameters;
            var method = new MethodDefinition(methodInfo.Name, methodAttributes, methodInfo.ReturnType);
            foreach (var parameterType in parameterInfos.Select(x => x.ParameterType).ToArray())
                method.Parameters.Add(new ParameterDefinition(parameterType));
            ProxyType.Methods.Add(method);

            // Initialize method info in static constructor
            var methodInfoField = new FieldDefinition(methodInfo.Name + "__Info", FieldAttributes.Private | FieldAttributes.Static, Context.MethodInfoType);
            ProxyType.Fields.Add(methodInfoField);

            StaticConstructor.Body.Emit(il =>
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
            GenericInstanceType proceedDelegateType;
            MethodReference proceedDelegateTypeConstructor;
            TypeReference proceedReturnType;
            MethodReference invocationConstructor;
            MethodReference invokeMethod;

            if (methodInfo.ReturnType.CompareTo(Context.ModuleDefinition.TypeSystem.Void))
            {
                proceedDelegateType = Context.Action1Type.MakeGenericInstanceType(Context.ObjectArrayType);
                proceedDelegateTypeConstructor = Context.Action1Type.Resolve().GetConstructors().First().Bind(proceedDelegateType);
                proceedReturnType = Context.ModuleDefinition.Import(typeof(void));
                invocationConstructor = Context.VoidInvocationConstructor;
                invokeMethod = Context.VoidInvokeMethod;
            }
            else
            {
                proceedDelegateType = Context.Func2Type.MakeGenericInstanceType(Context.ObjectArrayType, methodInfo.ReturnType);
                proceedDelegateTypeConstructor = Context.Func2Type.Resolve().GetConstructors().First().Bind(proceedDelegateType);
                proceedReturnType = Context.ModuleDefinition.Import(methodInfo.ReturnType);
                if (!Context.TaskType.IsAssignableFrom(methodInfo.ReturnType))
                {
                    var invocationType = Context.InvocationTType.MakeGenericInstanceType(methodInfo.ReturnType.Resolve());
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.InvocationTType.Resolve().GetConstructors().First());
                    invocationConstructor = Context.ModuleDefinition.Import(unconstructedConstructor.Bind(invocationType));
                    invokeMethod = Context.ModuleDefinition.Import(Context.InvokeTMethod.MakeGenericMethod(methodInfo.ReturnType.Resolve()));
                }
                else if (methodInfo.ReturnType.IsTaskT())
                {
                    var taskTType = methodInfo.ReturnType.GetTaskType();
                    var invocationType = Context.AsyncInvocationTType.MakeGenericInstanceType(taskTType);
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.AsyncInvocationTType.Resolve().GetConstructors().First());
                    invocationConstructor = Context.ModuleDefinition.Import(unconstructedConstructor.Bind(invocationType));
                    invokeMethod = Context.ModuleDefinition.Import(Context.AsyncInvokeTMethod.MakeGenericMethod(taskTType));
                }
                else
                {
                    invocationConstructor = Context.VoidAsyncInvocationConstructor;
                    invokeMethod = Context.AsyncVoidInvokeMethod;
                }
            }

            var proceed = new MethodDefinition(methodInfo.Name + "$Proceed", MethodAttributes.Private, proceedReturnType);
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            proceed.Body = new MethodBody(proceed);
            ProxyType.Methods.Add(proceed);

            proceed.Body.Emit(il =>
            {
                ImplementProceed(methodInfo, il);
            });

            // Implement method
            method.Body = new MethodBody(method);
            method.Body.Emit(il =>
            {
                // Load handler
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, InvocationHandler);

                // Load method info
                il.Emit(OpCodes.Ldsfld, methodInfoField);

                // Create arguments array
                il.Emit(OpCodes.Ldc_I4, parameterInfos.Count);         // Array length
                il.Emit(OpCodes.Newarr, Context.ModuleDefinition.TypeSystem.Object);                // Instantiate array
                for (var i = 0; i < parameterInfos.Count; i++)
                {
                    il.Emit(OpCodes.Dup);                               // Duplicate array
                    il.Emit(OpCodes.Ldc_I4, i);                         // Array index
                    il.Emit(OpCodes.Ldarg, (short)(i + 1));             // Element value

                    if (parameterInfos[i].ParameterType.IsValueType)
                        il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);

                    il.Emit(OpCodes.Stelem_Any, Context.ModuleDefinition.TypeSystem.Object);            // Set array at index to element value
                }

                // Load function pointer to proceed method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, proceed);
                il.Emit(OpCodes.Newobj, proceedDelegateTypeConstructor);

                // Instantiate Invocation
                il.Emit(OpCodes.Newobj, invocationConstructor);

                // Invoke handler
                il.Emit(OpCodes.Callvirt, invokeMethod);

                // Return
                il.Emit(OpCodes.Ret);
            });
        }
    }
}