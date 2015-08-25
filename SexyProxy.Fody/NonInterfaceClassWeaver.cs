using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public class NonInterfaceClassWeaver : TargetedClassWeaver
    {
        public NonInterfaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override void PrepareTargetForConstructor(ILProcessor il)
        {
            base.PrepareTargetForConstructor(il);

            // If target is null, we will make the target ourself
            var targetNotNull = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Dup);                      // Duplicate "target" since it will be consumed by the following branch instruction
            il.Emit(OpCodes.Brtrue, targetNotNull);    // If target is not null, jump below
            il.Emit(OpCodes.Pop);                      // Pop the null target off the stack
            il.Emit(OpCodes.Ldarg_0);                  // Place "this" onto the stack (our new target)
            il.Append(targetNotNull);
        }

        protected override MethodAttributes GetMethodAttributes(MethodDefinition methodInfo)
        {
            // If we're overriding a method, these attributes are required
            var methodAttributes = methodInfo.IsPublic ? MethodAttributes.Public : MethodAttributes.Family;
            methodAttributes |= MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual;
            return methodAttributes;
        }

        protected override OpCode GetProceedCallOpCode(MethodDefinition methodInfo)
        {
            return methodInfo.IsVirtual ? OpCodes.Callvirt : OpCodes.Call;
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod, MethodReference proceedTargetMethod)
        {
            if (methodInfo.IsAbstract)
                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
            else
                base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, proceedDelegateTypeConstructor, 
                    invocationType, invocationConstructor, invokeMethod, proceedTargetMethod);
        }
    }
}