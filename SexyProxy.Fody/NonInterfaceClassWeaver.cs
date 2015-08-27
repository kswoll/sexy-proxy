using System;
using System.Linq;
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
            private MethodDefinition callBaseMethod;

            public NonInterfaceMethodWeaver(WeaverContext context, TypeDefinition source, TypeDefinition proxy, MethodDefinition method, string name, MethodDefinition staticConstructor, FieldReference target, FieldDefinition invocationHandler) : base(context, source, proxy, method, name, staticConstructor, target, invocationHandler)
            {
            }

            protected override void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
            {
                // Create base call
                if (!Method.IsAbstract)
                {
                    callBaseMethod = new MethodDefinition(Name + "$Base", MethodAttributes.Private, Method.ReturnType);
                    foreach (var parameter in Method.Parameters)
                    {
                        var newParameter = new ParameterDefinition(parameter.Name, ParameterAttributes.None, parameter.ParameterType);
                        callBaseMethod.Parameters.Add(newParameter);
                    }
                    Method.CopyGenericParameters(callBaseMethod);
                    callBaseMethod.Body.Emit(il =>
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        for (var i = 0; i < Method.Parameters.Count; i++)
                        {
                            il.Emit(OpCodes.Ldarg, (short)i + 1);
                        }
                        var methodReference = GetProceedMethodTarget();
                        if (Method.GenericParameters.Count > 0)
                            methodReference = methodReference.MakeGenericMethod(callBaseMethod.GenericParameters.ToArray());
                        il.Emit(OpCodes.Call, methodReference);
                        il.Emit(OpCodes.Ret);
                    });
                    Proxy.Methods.Add(callBaseMethod);
                }
                base.ProxyMethod(body, proceedTargetMethod);
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

                    base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, _ => EmitProxyFromProceed(il), callBaseMethod, OpCodes.Call);

                    il.Append(targetNotNull);                    // Mark where the previous branch instruction should jump to                        
                
                    base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, emitProceedTarget, proceedTargetMethod, proceedOpCode);
                }
            }
        }
    }
}