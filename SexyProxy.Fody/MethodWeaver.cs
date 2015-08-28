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
        public TypeDefinition ProceedClass { get; private set; }

        public GenericInstanceType ProceedDelegateType { get; private set; }
        public MethodReference ProceedDelegateTypeConstructor { get; private set; }
        public TypeReference ProceedReturnType { get; private set; }
        public TypeReference InvocationType { get; private set; }
        public MethodReference InvocationConstructor { get; private set; }
        public MethodReference InvokeMethod { get; private set; }

        protected MethodWeaver(WeaverContext context, TypeDefinition source, TypeDefinition proxy, MethodDefinition method, string name, MethodDefinition staticConstructor)
        {
            Context = context;
            Source = source;
            Proxy = proxy;
            Method = method;
            Name = name;
            StaticConstructor = staticConstructor;
        }

        public void DefineProxy()
        {
            var name = Name + "$Proceed";
            var arity = Proxy.GenericParameters.Count + Method.GenericParameters.Count;
            if (arity > 0)
            {
                name += "`" + arity;
            }
            ProceedClass = new TypeDefinition(Proxy.Namespace, name, TypeAttributes.NestedPrivate, Context.ObjectType);
            Proxy.CopyGenericParameters(ProceedClass);
            Method.CopyGenericParameters(ProceedClass);

            var proceedMethodTarget = GetProceedMethodTarget();

            ProxyMethod(Method.Body, proceedMethodTarget);

            Proxy.NestedTypes.Add(ProceedClass);
        }

        protected virtual MethodReference GetProceedMethodTarget()
        {
            MethodReference result = Method;
            if (Source.HasGenericParameters)
                result = Method.Bind(Source.MakeGenericInstanceType(Proxy.GenericParameters.ToArray()));
            return result;
        }

        protected virtual void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
        {
            // Initialize method info in static constructor
            var methodInfoFieldDefinition = new FieldDefinition(Name + "$Info", FieldAttributes.Private | FieldAttributes.Static, Context.MethodInfoType);
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
            SetUpTypes();

            var proceed = new MethodDefinition("Proceed", MethodAttributes.Public | MethodAttributes.Static, ProceedReturnType.ResolveGenericParameter(Proxy));
            proceed.Parameters.Add(new ParameterDefinition(Context.InvocationType));
            proceed.Body = new MethodBody(proceed);
            proceed.Body.InitLocals = true;
            ProceedClass.Methods.Add(proceed);

            MethodReference proceedReference = proceed;
            TypeReference proceedClass = ProceedClass;

            if (ProceedClass.HasGenericParameters)
            {
                proceedReference = proceed.Bind(proceedClass.MakeGenericInstanceType(Proxy.GenericParameters.Concat(body.Method.GenericParameters).ToArray()));
            }

            proceed.Body.Emit(il =>
            {
                ImplementProceed(Method, body, il, methodInfoField, proceedReference, EmitProceedTarget, proceedTargetMethod, GetProceedCallOpCode());
            });

            // Implement method
            body.Emit(il =>
            {
                ImplementBody(il, methodInfoField, proceedReference);
            });
        }

        protected virtual void ImplementBody(ILProcessor il, FieldReference methodInfoField, MethodReference proceed)
        {
            EmitCallToInvocationHandler(il, methodInfoField, proceed);

            // Return
            il.Emit(OpCodes.Ret);            
        }
            
        protected void EmitCallToInvocationHandler(ILProcessor il, FieldReference methodInfoField, MethodReference proceed)
        {
            // Load handler (consumed by call to invokeMethod near the end)
            EmitInvocationHandler(il);

            // Put Invocation onto the stack
            EmitInvocation(il, methodInfoField, proceed);

            // Invoke handler
            il.Emit(OpCodes.Callvirt, InvokeMethod);
        }

        protected void EmitInvocation(ILProcessor il, FieldReference methodInfoField, MethodReference proceed)
        {
            // Load proxy
            il.Emit(OpCodes.Ldarg_0);

            // Load invocation handler
            EmitInvocationHandler(il);

            // Load method info
            il.Emit(OpCodes.Ldsfld, methodInfoField);

            // Create arguments array
            EmitInvocationArgumentsArray(il, Method.Parameters.Count);

            // Load function pointer to proceed method
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldftn, proceed);
            il.Emit(OpCodes.Newobj, ProceedDelegateTypeConstructor);

            // Instantiate Invocation
            il.Emit(OpCodes.Newobj, InvocationConstructor);
        }

        protected virtual void EmitInvocationArgumentsArray(ILProcessor il, int size)
        {
            var parameterInfos = Method.Parameters;
            il.Emit(OpCodes.Ldc_I4, size);                          // Array length
            il.Emit(OpCodes.Newarr, Context.ModuleDefinition.TypeSystem.Object);    // Instantiate array
            for (var i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Dup);                               // Duplicate array
                il.Emit(OpCodes.Ldc_I4, i);                         // Array index
                il.Emit(OpCodes.Ldarg, (short)(i + 1));             // Element value

                if (parameterInfos[i].ParameterType.IsValueType || parameterInfos[i].ParameterType.IsGenericParameter)
                    il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);

                il.Emit(OpCodes.Stelem_Any, Context.ModuleDefinition.TypeSystem.Object);  // Set array at index to element value
            }            
        }

        protected virtual OpCode GetProceedCallOpCode()
        {
            return OpCodes.Callvirt;
        }

        protected void EmitProxyFromProceed(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);                    // Load "this"
            il.Emit(OpCodes.Call, Context.InvocationGetProxy);

            il.Emit(OpCodes.Castclass, GetProxyTypeReference());
        }

        protected TypeReference GetProxyTypeReference()
        {
            TypeReference proxy = Proxy;
            if (proxy.HasGenericParameters)
            {
                proxy = proxy.MakeGenericInstanceType(Proxy.GenericParameters.ToArray());
            }
            return proxy;
        }

        protected virtual void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
        {
            var parameterInfos = methodInfo.Parameters;

            // Load target for subsequent call
            emitProceedTarget(il);

            // Decompose array into arguments
            for (int i = 0; i < parameterInfos.Count; i++)
            {
                il.Emit(OpCodes.Ldarg_0);                                                    // Push array 
                il.Emit(OpCodes.Call, Context.InvocationGetArguments);                       // invocation.Arguments
                il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                il.Emit(OpCodes.Ldelem_Any, Context.ModuleDefinition.TypeSystem.Object);     // Get element
                if (parameterInfos[i].ParameterType.IsValueType || parameterInfos[i].ParameterType.IsGenericParameter) // If it's a value type, unbox it
                    il.Emit(OpCodes.Unbox_Any, parameterInfos[i].ParameterType.ResolveGenericParameter(ProceedClass));
                else                                                                         // Otherwise, cast it
                    il.Emit(OpCodes.Castclass, parameterInfos[i].ParameterType.ResolveGenericParameter(ProceedClass));
            }

            var genericProceedTargetMethod = proceedTargetMethod;
            if (Method.GenericParameters.Count > 0)
                genericProceedTargetMethod = genericProceedTargetMethod.MakeGenericMethod(Method.GenericParameters.Select(x => x.ResolveGenericParameter(ProceedClass)).ToArray());

            il.Emit(proceedOpCode, genericProceedTargetMethod);
            il.Emit(OpCodes.Ret);                    
        }
 

        private void SetUpTypes()
        {
            if (Method.ReturnType.CompareTo(Context.ModuleDefinition.TypeSystem.Void))
            {
                ProceedDelegateType = Context.Action1Type.MakeGenericInstanceType(Context.InvocationType);
                ProceedDelegateTypeConstructor = Context.Action1Type.Resolve().GetConstructors().First().Bind(ProceedDelegateType);
                ProceedReturnType = Context.ModuleDefinition.TypeSystem.Void;
                InvocationType = Context.VoidInvocationType;
                InvocationConstructor = Context.VoidInvocationConstructor;
                InvokeMethod = Context.VoidInvokeMethod;
            }
            else
            {
                ProceedDelegateType = Context.Func2Type.MakeGenericInstanceType(Context.InvocationType, Method.ReturnType);
                ProceedDelegateTypeConstructor = Context.Func2Type.Resolve().GetConstructors().First().Bind(ProceedDelegateType);
                ProceedReturnType = Method.ReturnType;
                if (!Context.TaskType.IsAssignableFrom(Method.ReturnType))
                {
                    var returnType = Method.ReturnType;
                    var genericInvocationType = Context.InvocationTType.MakeGenericInstanceType(returnType);
                    InvocationType = genericInvocationType;
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.InvocationTType.Resolve().GetConstructors().First());
                    InvocationConstructor = unconstructedConstructor.Bind(genericInvocationType);
                    InvokeMethod = Context.InvokeTMethod.MakeGenericMethod(returnType);
                }
                else if (Method.ReturnType.IsTaskT())
                {
                    var taskTType = Method.ReturnType.GetTaskType();
                    var genericInvocationType = Context.AsyncInvocationTType.MakeGenericInstanceType(taskTType);
                    InvocationType = genericInvocationType;
                    var unconstructedConstructor = Context.ModuleDefinition.Import(Context.AsyncInvocationTType.Resolve().GetConstructors().First());
                    InvocationConstructor = unconstructedConstructor.Bind(genericInvocationType);
                    InvokeMethod = Context.AsyncInvokeTMethod.MakeGenericMethod(taskTType);
                }
                else
                {
                    InvocationType = Context.VoidAsyncInvocationType;
                    InvocationConstructor = Context.VoidAsyncInvocationConstructor;
                    InvokeMethod = Context.AsyncVoidInvokeMethod;
                }
            }            
        }                
    }
}