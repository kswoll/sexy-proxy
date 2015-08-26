using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace SexyProxy.Fody
{
    public abstract class MethodWeaver
    {
        protected abstract void EmitInvocationHandler(ILProcessor il);
        protected abstract void EmitProceedTarget(ILProcessor il);

        public WeaverContext Context { get; }
        public TypeDefinition Source { get; }
        public TypeDefinition Proxy { get; }
        public MethodDefinition Method { get; }
        public string Name { get; }
        public MethodDefinition StaticConstructor { get; }

        protected virtual MethodReference GetProceedMethodTarget(MethodDefinition methodInfo)
        {
            MethodReference result = methodInfo;
            if (Source.HasGenericParameters)
                result = methodInfo.Bind(Source.MakeGenericInstanceType(Proxy.GenericParameters.ToArray()));
            return result;
        }

        protected virtual void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
        {
            // Initialize method info in static constructor
            var methodInfoFieldDefinition = new FieldDefinition(Method.GenerateSignature() + "$Info", FieldAttributes.Private | FieldAttributes.Static, Context.MethodInfoType);
            Proxy.Fields.Add(methodInfoFieldDefinition);
            FieldReference methodInfoField = methodInfoFieldDefinition;
            StaticConstructor.Body.Emit(il =>
            {
                TypeReference methodDeclaringType = Method.DeclaringType;
                if (Proxy.HasGenericParameters)
                {
                    var genericProxyType = Proxy.MakeGenericInstanceType(Proxy.GenericParameters.ToArray());
                    methodInfoField = methodInfoField.Bind(genericProxyType);
                    methodDeclaringType = Source.MakeGenericInstanceType(Proxy.GenericParameters.ToArray());
                }

                var methodSignature = Method.GenerateSignature();
                var methodFinder = Context.MethodFinder.MakeGenericInstanceType(methodDeclaringType);
                var findMethod = Context.FindMethod.Bind(methodFinder);

                // Store MethodInfo into the static field
                il.Emit(OpCodes.Ldstr, methodSignature);
                il.Emit(OpCodes.Call, findMethod);
                il.Emit(OpCodes.Stsfld, methodInfoField);
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

            if (Method.ReturnType.CompareTo(Context.ModuleDefinition.TypeSystem.Void))
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
                proceedDelegateType = Context.Func2Type.MakeGenericInstanceType(Context.ObjectArrayType, Method.ReturnType);
                proceedDelegateTypeConstructor = Context.Func2Type.Resolve().GetConstructors().First().Bind(proceedDelegateType);
                proceedReturnType = Context.ModuleDefinition.Import(Method.ReturnType);
                if (!Context.TaskType.IsAssignableFrom(Method.ReturnType))
                {
                    // !!! This WAS the cause of the error not finding InvocationHandler type since IProxy.InvocationHandler hadn't been ignored yet and we're not importing the return type
                    // Create some unit tests that return custom types from a separate assembly
                    var returnType = Context.ModuleDefinition.Import(Method.ReturnType);
                    var genericInvocationType = Context.InvocationTType.MakeGenericInstanceType(returnType);
                    invocationType = genericInvocationType;
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.InvocationTType.Resolve().GetConstructors().First());
                    invocationConstructor = Context.ModuleDefinition.Import(unconstructedConstructor.Bind(genericInvocationType));
                    invokeMethod = Context.ModuleDefinition.Import(Context.InvokeTMethod.MakeGenericMethod(returnType));
                }
                else if (Method.ReturnType.IsTaskT())
                {
                    var taskTType = Method.ReturnType.GetTaskType();
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

            var proceed = new MethodDefinition(Method.GenerateSignature() + "$Proceed", MethodAttributes.Private, proceedReturnType.ResolveGenericParameter(Proxy));
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            proceed.Body = new MethodBody(proceed);
            proceed.Body.InitLocals = true;
            Proxy.Methods.Add(proceed);

            MethodReference proceedReference = proceed;
            if (Proxy.HasGenericParameters) 
                proceedReference = proceed.Bind(Proxy.MakeGenericInstanceType(Proxy.GenericParameters.ToArray()));

            proceed.Body.Emit(il =>
            {
                ImplementProceed(Method, body, il, methodInfoField, proceedReference, proceedDelegateTypeConstructor, invocationType, invocationConstructor, 
                    invokeMethod, EmitProceedTarget, proceedTargetMethod, GetProceedCallOpCode(Method));
            });

            // Implement method
            body.Emit(il =>
            {
                ImplementBody(Method, il, methodInfoField, proceedReference, proceedDelegateTypeConstructor, 
                    invocationType, invocationConstructor, invokeMethod);
            });
        }

        protected virtual void ImplementBody(ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            EmitCallToInvocationHandler(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, 
                invocationType, invocationConstructor, invokeMethod);

            // Return
            il.Emit(OpCodes.Ret);            
        }
            
        protected void EmitCallToInvocationHandler(MethodDefinition methodInfo, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            // Load handler (consumed by call to invokeMethod near the end)
            EmitInvocationHandler(il);

            // Put Invocation onto the stack
            EmitInvocation(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor);

            // Invoke handler
            il.Emit(OpCodes.Callvirt, invokeMethod);
        }

        protected void EmitInvocation(MethodDefinition methodInfo, ILProcessor il, FieldReference methodInfoField,
            MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, 
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

                if (parameterInfos[i].ParameterType.IsValueType || parameterInfos[i].ParameterType.IsGenericParameter)
                    il.Emit(OpCodes.Box, Context.ModuleDefinition.Import(parameterInfos[i].ParameterType));

                il.Emit(OpCodes.Stelem_Any, Context.ModuleDefinition.TypeSystem.Object);  // Set array at index to element value
            }            
        }

        protected virtual OpCode GetProceedCallOpCode(MethodDefinition methodInfo)
        {
            return OpCodes.Callvirt;
        }

        protected virtual void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
        {
            var parameterInfos = methodInfo.Parameters;

            // Load target for subsequent call
            emitProceedTarget(il);

            // Decompose array into arguments
            for (int i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Ldarg, 1);                                                   // Push array 
                il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                il.Emit(OpCodes.Ldelem_Any, Context.ModuleDefinition.TypeSystem.Object);     // Get element
                if (parameterInfos[i].ParameterType.IsValueType || parameterInfos[i].ParameterType.IsGenericParameter) // If it's a value type, unbox it
                    il.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType);
                else                                                                         // Otherwise, cast it
                    il.Emit(OpCodes.Castclass, parameterInfos[i].ParameterType);
            }

            il.Emit(proceedOpCode, proceedTargetMethod);
            il.Emit(OpCodes.Ret);                    
        }
         
    }
}