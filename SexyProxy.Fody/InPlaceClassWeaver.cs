using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public class InPlaceClassWeaver : ClassWeaver
    {
        public InPlaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override void ProxyMethod(MethodDefinition methodInfo)
        {
            // Check the source method for any usages of this.Invocation()
            for (var i = 0; i < methodInfo.Body.Instructions.Count; i++)
            {
                var instruction = methodInfo.Body.Instructions[i];
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
            }
        }

        protected override TypeDefinition GetProxyType()
        {
            throw new NotImplementedException();
        }

        protected override MethodDefinition GetStaticConstructor()
        {
            throw new NotImplementedException();
        }
    }
}