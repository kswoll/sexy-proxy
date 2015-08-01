using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public class InterfaceClassWeaver : TargetedClassWeaver
    {
        public InterfaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override TypeReference BaseType => Context.ObjectType;
        protected override TypeReference[] GetInterfaces() => new[] { SourceType };

        protected override IEnumerable<MethodDefinition> GetMethods()
        {
            var result = base.GetMethods();

            // If T is an interface type, we want to implement *all* the methods defined by the interface and its parent interfaces.
            result = result.Concat(SourceType.Interfaces.SelectMany(x => x.Resolve().Methods.Where(y => !y.IsStatic)));

            return result;
        }

        protected override MethodAttributes GetMethodAttributes(MethodDefinition methodInfo)
        {
            // The attributes required for the normal implementation of an interface method
            return MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
        }

        protected override OpCode GetProceedCallOpCode()
        {
            return OpCodes.Callvirt;
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, ILProcessor il)
        {
            // If T is an interface, then we want to check if target is null; if so, we want to just return the default value
            var targetNotNull = il.Create(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0);                    // Load "this"
            il.Emit(OpCodes.Ldfld, Target);              // Load "target" from "this"
            il.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below
            CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);

            il.Append(targetNotNull);                    // Mark where the previous branch instruction should jump to                        

            base.ImplementProceed(methodInfo, il);
        }
    }
}