using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public class NonInterfaceClassWeaver : TargetedClassWeaver
    {
        public NonInterfaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override MethodWeaver CreateMethodWeaver(MethodDefinition methodInfo, string name)
        {
            return new NonInterfaceMethodWeaver(Context, SourceType, ProxyType, methodInfo, name, StaticConstructor, Target, InvocationHandler);
        }

        protected class NonInterfaceMethodWeaver : TargetedMethodWeaver
        {
            public NonInterfaceMethodWeaver(WeaverContext context, TypeDefinition source, TypeDefinition proxy, MethodDefinition method, string name, MethodDefinition staticConstructor, FieldReference target, FieldDefinition invocationHandler) : base(context, source, proxy, method, name, staticConstructor, target, invocationHandler)
            {
            }

            protected override MethodAttributes GetMethodAttributes()
            {
                // If we're overriding a method, these attributes are required
                var methodAttributes = Method.IsPublic ? MethodAttributes.Public : MethodAttributes.Family;
                methodAttributes |= MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;
                return methodAttributes;
            }

            protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
            {
                if (methodInfo.IsAbstract)
                {
                    CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
                }
                else
                {
                    var targetNotNull = il.Create(OpCodes.Nop);
                    EmitProxyFromProceed(il);
                    il.Emit(OpCodes.Ldfld, target);              // Load "target" from "this"
                    il.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below

                    base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, _ => EmitProxyFromProceed(il), proceedTargetMethod, OpCodes.Call);

                    il.Append(targetNotNull);                    // Mark where the previous branch instruction should jump to                        
                
                    base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, emitProceedTarget, proceedTargetMethod, proceedOpCode);
                }
            }
        }
    }
}