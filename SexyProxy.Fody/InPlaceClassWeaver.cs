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
            il.Emit(OpCodes.Call, Context.ProxyGetInvocationHandlerMethod);
        }

        protected override void EmitProceedTarget(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
        }

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
*/
            }

            // Now create a new method body
            methodInfo.Body = new MethodBody(methodInfo);

            base.ProxyMethod(methodInfo, methodInfo.Body, originalMethod);
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