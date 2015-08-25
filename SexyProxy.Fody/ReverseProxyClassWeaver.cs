using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public class ReverseProxyClassWeaver : ClassWeaver
    {
        public ReverseProxyClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override OpCode GetProceedCallOpCode(MethodDefinition methodInfo)
        {
            return OpCodes.Call;
        }

        protected override void EmitInvocationHandler(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, Context.ReverseProxyGetInvocationHandlerMethod);
        }

        protected override void EmitProceedTarget(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
        }

        protected override void ImplementBody(MethodDefinition methodInfo, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            // If it's abstract, then the method is entirely implemented by the InvocationHandler
            if (methodInfo.IsAbstract)
            {
                base.ImplementBody(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, invokeMethod);
            }
            // Otherwise, it is implemented by the class itself, and calling this.Invocation().Proceed() calls the InvocationHandler
            else
            {
                methodInfo.Body.InitLocals = true;

                // First declare the invocation in a private local variable
                var invocation = new VariableDefinition(Context.InvocationType);
                var instructions = methodInfo.Body.Instructions.ToList();
                methodInfo.Body.Instructions.Clear();
                methodInfo.Body.Variables.Add(invocation);
                EmitInvocation(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor);
                il.Emit(OpCodes.Dup);                               // Duplicate invocation for below
                il.Emit(OpCodes.Stloc, invocation);

                // Add the invocation to the end of the array
                il.Emit(OpCodes.Call, Context.InvocationGetArguments);  // Array now on the stack with the invocation above it
                il.Emit(OpCodes.Ldc_I4, methodInfo.Parameters.Count);   // Array index
                il.Emit(OpCodes.Ldloc, invocation);                     // Element value
                il.Emit(OpCodes.Stelem_Any, Context.ModuleDefinition.TypeSystem.Object);  // Set array at index to element value

                // Special instrumentation for async methods
                var returnType = methodInfo.ReturnType;
                if (Context.TaskType.IsAssignableFrom(returnType))
                {
                    // If the return type is Task<T>
                    if (returnType.IsTaskT())
                    {
                        var actualReturnType = returnType.GetTaskType();
                        var expectedAsyncBuilder = Context.AsyncTaskMethodBuilder.MakeGenericInstanceType(actualReturnType);

                        // Now find the call to .Start() (only will be found if we're async)
                        var startInstructionMethod = (GenericInstanceMethod)instructions
                            .Where(x => x.OpCode == OpCodes.Call)
                            .Select(x => (MethodReference)x.Operand)
                            .Where(x => x.IsGenericInstance && x.Name == "Start" && x.DeclaringType.CompareTo(expectedAsyncBuilder))
                            .SingleOrDefault();
                        if (startInstructionMethod != null)
                        {
                            var asyncType = startInstructionMethod.GenericArguments[0];
                            var asyncConstructor = asyncType.Resolve().GetConstructors().Single();
                            var invocationField = InstrumentAsyncType(asyncType);

                            // Now find the instantiation of the asyncType
                            var instantiateAsyncTypeIndex = instructions.IndexOf(x => x.OpCode == OpCodes.Newobj && x.Operand.Equals(asyncConstructor));
                            if (instantiateAsyncTypeIndex == -1)
                                throw new Exception($"Could not find expected instantiation of async type: {asyncType}");
                            var nextSetFieldIndex = instructions.IndexOf(x => x.OpCode == OpCodes.Stfld);
                            if (nextSetFieldIndex == -1)
                                throw new Exception($"Could not find expected stfld of async type: {asyncType}");
                            var setFieldLoadInstance = instructions[nextSetFieldIndex - 2];
                            instructions.Insert(nextSetFieldIndex - 2, il.Clone(setFieldLoadInstance));
                            instructions.Insert(nextSetFieldIndex - 1, il.Create(OpCodes.Ldloc, invocation));
                            instructions.Insert(nextSetFieldIndex, il.Create(OpCodes.Stfld, invocationField));
                        }
                    }
                }

                InstrumentInstructions(il, instructions, 2, x => x.Emit(OpCodes.Ldloc, invocation));
            }
        }

        private void InstrumentInstructions(ILProcessor il, IList<Instruction> instructions, int numberOfReplacedInstructions, Action<ILProcessor> loadInvocation)
        {
            var seekDepth = numberOfReplacedInstructions - 1;

            // Now add all the instructions back, but transforming this.Invocation() if present
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                var nextInstruction = i < instructions.Count - seekDepth ? instructions[i + seekDepth] : null;
                if (nextInstruction != null && nextInstruction.OpCode == OpCodes.Call)
                {
                    var methodReference = (MethodReference)nextInstruction.Operand;
                    if (methodReference.FullName == "SexyProxy.Invocation SexyProxy.ProxyExtensions::Invocation(SexyProxy.IReverseProxy)")
                    {
                        // Insert reference to invocation
                        loadInvocation(il);
                        i += seekDepth;
                        continue;
                    }
                }
                il.Append(instruction);
            }            
        }

        private FieldDefinition InstrumentAsyncType(TypeReference asyncType)
        {
            var asyncTypeDefinition = asyncType.Resolve();
            var invocationField = new FieldDefinition("$invocation", FieldAttributes.Public, Context.InvocationType);
            asyncTypeDefinition.Fields.Add(invocationField);

            var moveNext = asyncTypeDefinition.Methods.Single(x => x.Name == "MoveNext");
            var instructions = moveNext.Body.Instructions.ToList();
            moveNext.Body.Instructions.Clear();
            InstrumentInstructions(moveNext.Body.GetILProcessor(), instructions, 3, x =>
            {
                x.Emit(OpCodes.Ldarg_0);
                x.Emit(OpCodes.Ldfld, invocationField);
            });

            return invocationField;
        }

        protected override void EmitInvocationArgumentsArray(MethodDefinition methodInfo, ILProcessor il, int size)
        {
            base.EmitInvocationArgumentsArray(methodInfo, il, size + 1);
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
        {
            if (methodInfo.IsAbstract)
            {
                // Always return the default value
                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
            }
            else
            {
                // Load handler (consumed by call to invokeMethod near the end)
                EmitInvocationHandler(il);

                // Put Invocation onto the stack
                il.Emit(OpCodes.Ldarg_1);                                                   // Array
                il.Emit(OpCodes.Ldc_I4, methodInfo.Parameters.Count);                       // Array index
                il.Emit(OpCodes.Ldelem_Any, Context.ModuleDefinition.TypeSystem.Object);    // Load element
                il.Emit(OpCodes.Castclass, invocationType);                                 // Cast it into specific invocation subclass

                // Invoke handler
                il.Emit(OpCodes.Callvirt, invokeMethod);

                // Return from method
                il.Emit(OpCodes.Ret);
            }
        }

        protected override void ProxyMethod(MethodDefinition methodInfo, MethodBody body, MethodReference proceedTargetMethod)
        {
            if (methodInfo.ReturnType.CompareTo(Context.InvocationHandlerType) && methodInfo.Name == "get_InvocationHandler") 
                return;

            if (methodInfo.IsAbstract)
            {
                // If it's abstract, we need to implement a default implementation
                body = methodInfo.Body = new MethodBody(methodInfo);
            }

            base.ProxyMethod(methodInfo, body, proceedTargetMethod);

            if (methodInfo.IsAbstract)
            {
                methodInfo.IsAbstract = false;
            }
        }

        protected override void Finish()
        {
            ProxyType.IsAbstract = false;

            // Ensure constructor is public
            ProxyType.GetConstructors().Single(x => !x.IsStatic).IsPublic = true;

            base.Finish();
        }

        protected override TypeDefinition GetProxyType()
        {
            return SourceType;
        }

        protected override MethodDefinition GetStaticConstructor()
        {
            var staticConstructor = ProxyType.GetStaticConstructor();
            if (staticConstructor == null)
            {
                staticConstructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, Context.ModuleDefinition.TypeSystem.Void);
                staticConstructor.Body = new MethodBody(staticConstructor);
                ProxyType.Methods.Add(staticConstructor);
            }
            else
            {
                staticConstructor.Body.Instructions.RemoveAt(staticConstructor.Body.Instructions.Count - 1);
            }            
            return staticConstructor;
        }
    }
}