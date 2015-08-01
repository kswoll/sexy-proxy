using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public class InPlaceClassWeaver : ClassWeaver
    {
        public InPlaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override OpCode GetProceedCallOpCode()
        {
            return OpCodes.Call;
        }

        protected override void EmitInvocationHandler(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, Context.ProxyGetInvocationHandlerMethod);
        }

        protected override void EmitProceedTarget(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
        }

        protected override void ImplementBody(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField, MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, MethodReference invocationConstructor, MethodReference invokeMethod)
        {
            // If it's abstract, then the method is entirely implemented by the InvocationHandler
            if (methodInfo.IsAbstract)
            {
                base.ImplementBody(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationConstructor, invokeMethod);
            }
            // Otherwise, it is implemented by the class itself, and calling this.Invocation().Proceed() calls the InvocationHandler
            else
            {
                // First declare the invocation in a private local variable
                var invocation = new VariableDefinition(Context.InvocationType);
                var instructions = methodInfo.Body.Instructions.ToArray();
                methodInfo.Body.Instructions.Clear();
                methodInfo.Body.Variables.Add(invocation);
//                il.Emit(OpCodes.Ldnull);
//                EmitCallToInvocationHandler(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationConstructor, invokeMethod);
                EmitInvocation(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationConstructor);
                il.Emit(OpCodes.Stloc, invocation);

                // Now add all the instructions back, but transforming this.Invocation() if present
                for (var i = 0; i < instructions.Length; i++)
                {
                    var instruction = instructions[i];
                    var nextInstruction = i < instructions.Length - 1 ? instructions[i + 1] : null;
                    if (nextInstruction != null && nextInstruction.OpCode == OpCodes.Call)
                    {
                        Context.LogInfo(nextInstruction.ToString());
                        var methodReference = (MethodReference)nextInstruction.Operand;
                        if (methodReference.FullName == "SexyProxy.Invocation SexyProxy.ProxyExtensions::Invocation(SexyProxy.IProxy)")
                        {
                            // Insert reference to invocation
                            il.Emit(OpCodes.Ldloc, invocation);
                            i++;
                            continue;
                        }
                    }
                    il.Append(instruction);
                }
            }
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, ILProcessor il, FieldDefinition methodInfoField,
            MethodDefinition proceed, MethodReference proceedDelegateTypeConstructor, MethodReference invocationConstructor,
            MethodReference invokeMethod, MethodDefinition proceedTargetMethod)
        {
//            if (methodInfo.IsAbstract)
//            {
                // Always return the default value
                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
//            }
//            else
//            {
//                EmitCallToInvocationHandler(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, 
//                    invocationConstructor, invokeMethod);
//            }
        }
/*

        protected override void ProxyMethod(MethodDefinition methodInfo, MethodBody body, MethodDefinition proceedTargetMethod)
        {

            // Create a new method for the original implementation
            var originalMethod = new MethodDefinition(methodInfo.Name + "$Original", MethodAttributes.Private, methodInfo.ReturnType);
            foreach (var parameter in methodInfo.Parameters)
            {
                originalMethod.Parameters.Add(parameter);
            }
            originalMethod.Body = new MethodBody(originalMethod);
            foreach (var variable in methodInfo.Body.Variables)
            {
                originalMethod.Body.Variables.Add(variable);
            }
            foreach (var instruction in methodInfo.Body.Instructions)
            {
                originalMethod.Body.GetILProcessor().Append(instruction);
/*
                // Check the source method for any usages of this.Invocation()
                if (instruction.OpCode == OpCodes.Call)
                {
                    var methodReference = (MethodReference)instruction.Operand;
                    if (methodReference.FullName == "SexyProxy.Invocation SexyProxy.ProxyExtensions::Invocation(SexyProxy.IProxy)")
                    {
                        // Insert reference to invocation
                                    

                        // Remove method call and the "this" argument
                        methodInfo.Body.Instructions.RemoveAt(i);
                        methodInfo.Body.Instructions.RemoveAt(i - 1);
                    }
                }
#1#
            }

            // Now create a new method body
            methodInfo.Body = new MethodBody(methodInfo);

            base.ProxyMethod(methodInfo, methodInfo.Body, originalMethod);
        }
*/

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