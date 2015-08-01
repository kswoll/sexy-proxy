using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public abstract class ClassWeaver
    {
        protected abstract TypeDefinition GetProxyType();
        protected abstract MethodDefinition GetStaticConstructor();
        protected abstract OpCode GetProceedCallOpCode();
        protected abstract void EmitInvocationHandler(ILProcessor il);
        protected abstract void EmitProceedTarget(ILProcessor il);

        public WeaverContext Context { get; }
        public TypeDefinition SourceType { get; }
        public TypeDefinition ProxyType { get; private set; }
        public IEnumerable<MethodDefinition> Methods { get; private set; }
        public MethodDefinition StaticConstructor { get; private set; }

        protected ClassWeaver(WeaverContext context, TypeDefinition sourceType)
        {
            Context = context;
            SourceType = sourceType;
        }

        protected virtual void InitializeProxyType()
        {
        }

        protected virtual IEnumerable<MethodDefinition> GetMethods()
        {
            var methods = SourceType.Methods.Where(x => !x.IsStatic);
            return methods;
        }

        protected virtual void Finish()
        {
        }

        public void Execute()
        {
            ProxyType = GetProxyType();
            InitializeProxyType();
            Methods = GetMethods();
            StaticConstructor = GetStaticConstructor();

            // Now implement/override all methods
            foreach (var methodInfo in Methods.ToArray())
            {
                var parameterInfos = methodInfo.Parameters;

                // Finalize doesn't work if we try to proxy it and really, who cares?
                if (methodInfo.Name == "Finalize" && parameterInfos.Count == 0 && methodInfo.DeclaringType.CompareTo(Context.ModuleDefinition.TypeSystem.Object.Resolve()))
                    continue;
                if (methodInfo.IsConstructor)
                    continue;

                ProxyMethod(methodInfo, methodInfo.Body, methodInfo);
            }

            StaticConstructor.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ret);
            });

            Finish();
        }

        protected virtual void ProxyMethod(MethodDefinition methodInfo, MethodBody body, MethodDefinition proceedTargetMethod)
        {
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
            TypeReference invocationType;
            MethodReference invocationConstructor;
            MethodReference invokeMethod;

            if (methodInfo.ReturnType.CompareTo(Context.ModuleDefinition.TypeSystem.Void))
            {
                proceedDelegateType = Context.Action1Type.MakeGenericInstanceType(Context.ObjectArrayType);
                proceedDelegateTypeConstructor = Context.Action1Type.Resolve().GetConstructors().First().Bind(proceedDelegateType);
                proceedReturnType = Context.ModuleDefinition.Import(typeof(void));
                invocationType = Context.VoidInvocationType;
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
                    // !!! This WAS the cause of the error not finding InvocationHandler type since IProxy.InvocationHandler hadn't been ignored yet and we're not importing the return type
                    // Create some unit tests that return custom types from a separate assembly
                    var returnType = Context.ModuleDefinition.Import(methodInfo.ReturnType);
                    var genericInvocationType = Context.InvocationTType.MakeGenericInstanceType(returnType);
                    invocationType = genericInvocationType;
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.InvocationTType.Resolve().GetConstructors().First());
                    invocationConstructor = Context.ModuleDefinition.Import(unconstructedConstructor.Bind(genericInvocationType));
                    invokeMethod = Context.ModuleDefinition.Import(Context.InvokeTMethod.MakeGenericMethod(returnType));
                }
                else if (methodInfo.ReturnType.IsTaskT())
                {
                    var taskTType = methodInfo.ReturnType.GetTaskType();
                    var genericInvocationType = Context.AsyncInvocationTType.MakeGenericInstanceType(taskTType);
                    invocationType = genericInvocationType;
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.AsyncInvocationTType.Resolve().GetConstructors().First());
                    invocationConstructor = Context.ModuleDefinition.Import(unconstructedConstructor.Bind(genericInvocationType));
                    invokeMethod = Context.ModuleDefinition.Import(Context.AsyncInvokeTMethod.MakeGenericMethod(taskTType));
                }
                else
                {
                    invocationType = Context.VoidAsyncInvocationType;
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
                ImplementProceed(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, 
                    invokeMethod, proceedTargetMethod);
            });

            // Implement method
            body.Emit(il =>
            {
                ImplementBody(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, 
                    invocationType, invocationConstructor, invokeMethod);
            });
        }

        protected virtual void ImplementBody(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField,
            MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, 
            MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            EmitCallToInvocationHandler(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, 
                invocationType, invocationConstructor, invokeMethod);

            // Return
            il.Emit(OpCodes.Ret);            
        }
            
        protected void EmitCallToInvocationHandler(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField,
            MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, 
            MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            // Load handler (consumed by call to invokeMethod near the end)
            EmitInvocationHandler(il);

            // Put Invocation onto the stack
            EmitInvocation(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor);

            // Invoke handler
            il.Emit(OpCodes.Callvirt, invokeMethod);
        }

        protected void EmitInvocation(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField,
            MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, 
            MethodReference invocationConstructor)
        {
            // Load method info
            il.Emit(OpCodes.Ldsfld, methodInfoField);

            // Create arguments array
            EmitInvocationArgumentsArray(methodInfo, il, methodInfo.Parameters.Count);

            // Load function pointer to proceed method
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldftn, proceed);
            il.Emit(OpCodes.Newobj, proceedDelegateTypeConstructor);

            // Instantiate Invocation
//            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Newobj, invocationConstructor);
        }

        protected virtual void EmitInvocationArgumentsArray(MethodDefinition methodInfo, ILProcessor il, int size)
        {
            var parameterInfos = methodInfo.Parameters;
            il.Emit(OpCodes.Ldc_I4, size);                          // Array length
            il.Emit(OpCodes.Newarr, Context.ModuleDefinition.TypeSystem.Object);    // Instantiate array
            for (var i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Dup);                               // Duplicate array
                il.Emit(OpCodes.Ldc_I4, i);                         // Array index
                il.Emit(OpCodes.Ldarg, (short)(i + 1));             // Element value

                if (parameterInfos[i].ParameterType.IsValueType)
                    il.Emit(OpCodes.Box, Context.ModuleDefinition.Import(parameterInfos[i].ParameterType));

                il.Emit(OpCodes.Stelem_Any, Context.ModuleDefinition.TypeSystem.Object);  // Set array at index to element value
            }            
        }

        protected virtual void ImplementProceed(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField,
            MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, 
            MethodReference invocationConstructor, MethodReference invokeMethod, MethodDefinition proceedTargetMethod)
        {
            var parameterInfos = methodInfo.Parameters;

            // Load target for subsequent call
            EmitProceedTarget(il);

            // Decompose array into arguments
            for (int i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Ldarg, 1);                                                   // Push array 
                il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                il.Emit(OpCodes.Ldelem_Any, Context.ModuleDefinition.TypeSystem.Object);     // Get element
                if (parameterInfos[i].ParameterType.IsValueType)                             // If it's a value type, unbox it
                    il.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType);
                else                                                                         // Otherwise, cast it
                    il.Emit(OpCodes.Castclass, parameterInfos[i].ParameterType);
            }

            il.Emit(GetProceedCallOpCode(), proceedTargetMethod);
            il.Emit(OpCodes.Ret);                    
        }
    }
}