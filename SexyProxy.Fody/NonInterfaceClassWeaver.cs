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

        protected override MethodAttributes GetMethodAttributes(MethodDefinition methodInfo)
        {
            // If we're overriding a method, these attributes are required
            var methodAttributes = methodInfo.IsPublic ? MethodAttributes.Public : MethodAttributes.Family;
            methodAttributes |= MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;
            return methodAttributes;
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
        {
            if (methodInfo.IsAbstract)
            {
                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
            }
            else
            {
                var targetNotNull = il.Create(OpCodes.Nop);
                il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                il.Emit(OpCodes.Ldfld, Target);              // Load "target" from "this"
                il.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below

                base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, invokeMethod, _ => il.Emit(OpCodes.Ldarg_0), proceedTargetMethod, OpCodes.Call);

                il.Append(targetNotNull);                    // Mark where the previous branch instruction should jump to                        
                
                base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, invokeMethod, emitProceedTarget, proceedTargetMethod, proceedOpCode);
            }
        }
    }
}