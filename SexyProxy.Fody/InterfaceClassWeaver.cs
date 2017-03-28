using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public class InterfaceClassWeaver : TargetedClassWeaver
    {
        public InterfaceClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override MethodWeaver CreateMethodWeaver(MethodDefinition methodInfo, string name)
        {
            return new InterfaceMethodWeaver(this, methodInfo, name, StaticConstructor, Target, InvocationHandler);
        }

        public override MethodAttributes GetMethodAttributes(MethodDefinition methodInfo)
        {
            // The attributes required for the normal implementation of an interface method
            return MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
        }

        protected override TypeReference GetBaseType(GenericParameter[] genericParameters) => Context.ObjectType;
        protected override TypeReference[] GetInterfaces(GenericParameter[] genericParameters) => new[] { !SourceType.HasGenericParameters ? Context.ModuleDefinition.Import(SourceType) : Context.ModuleDefinition.Import(SourceType).MakeGenericInstanceType(genericParameters) };
        protected override TypeReference GetSourceType() => GetInterfaces(ProxyType.GenericParameters.ToArray())[0];

        protected override IEnumerable<MethodDefinition> GetMethods()
        {
            var result = base.GetMethods();

            // If T is an interface type, we want to implement *all* the methods defined by the interface and its parent interfaces.
            result = result.Concat(SourceType.Interfaces.SelectMany(x => x.InterfaceType.Resolve().Methods.Where(y => !y.IsStatic)));

            return result;
        }

        protected class InterfaceMethodWeaver : TargetedMethodWeaver
        {
            public InterfaceMethodWeaver(InterfaceClassWeaver classWeaver, MethodDefinition method, string name, MethodDefinition staticConstructor, FieldReference target, FieldDefinition invocationHandler) : base(classWeaver, method, name, staticConstructor, target, invocationHandler)
            {
            }

            protected override OpCode GetProceedCallOpCode()
            {
                return OpCodes.Callvirt;
            }

            protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
            {
                // If T is an interface, then we want to check if target is null; if so, we want to just return the default value
                var targetNotNull = il.Create(OpCodes.Nop);
                EmitProxyFromProceed(il);
                il.Emit(OpCodes.Ldfld, ClassWeaver.Target);  // Load "target" from "this"
                il.Emit(OpCodes.Brtrue, targetNotNull);      // If target is not null, jump below
                CecilExtensions.CreateDefaultMethodImplementation(methodBody.Method, il);

                il.Append(targetNotNull);                    // Mark where the previous branch instruction should jump to                        

                base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, emitProceedTarget, proceedTargetMethod, proceedOpCode);
            }
        }
    }
}