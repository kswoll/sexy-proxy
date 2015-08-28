using System;
using System.Linq;
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

        protected override MethodWeaver CreateMethodWeaver(MethodDefinition methodInfo, string name)
        {
            return new InPlaceMethodWeaver(Context, SourceType, ProxyType, methodInfo, name, StaticConstructor);
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

        protected class InPlaceMethodWeaver : MethodWeaver
        {
            public InPlaceMethodWeaver(WeaverContext context, TypeDefinition source, TypeDefinition proxy, MethodDefinition method, string name, MethodDefinition staticConstructor) : base(context, source, proxy, method, name, staticConstructor)
            {
            }

            protected override OpCode GetProceedCallOpCode()
            {
                return OpCodes.Callvirt;
            }

            protected override void EmitInvocationHandler(ILProcessor il)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, Context.ProxyGetInvocationHandlerMethod);
            }

            protected override void EmitProceedTarget(ILProcessor il)
            {
                EmitProxyFromProceed(il);
            }

            protected override void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
            {
                if (Method.ReturnType.CompareTo(Context.InvocationHandlerType) && Method.Name == "get_InvocationHandler") 
                    return;
                if (Method.Name == "set_InvocationHandler" && Method.Parameters.Count == 1 && Method.Parameters.Single().ParameterType.CompareTo(Context.InvocationHandlerType))
                    return;

                Method.Body = body = new MethodBody(Method);

                base.ProxyMethod(body, proceedTargetMethod);

                if (Method.IsAbstract)
                {
                    Method.IsAbstract = false;
                }
            }

            protected override MethodReference GetProceedMethodTarget()
            {
                if (Method.IsAbstract)
                {
                    return base.GetProceedMethodTarget();
                }
                else
                {
                    // Transplant the original method into a new one that can be invoked when calling proceed
                    var originalMethod = new MethodDefinition(Method.GenerateSignature() + "$Original", MethodAttributes.Private, Method.ReturnType);
                    Method.CopyParameters(originalMethod);
                    Method.CopyGenericParameters(originalMethod);
                    originalMethod.Body = new MethodBody(originalMethod);
                    foreach (var variable in Method.Body.Variables)
                    {
                        originalMethod.Body.InitLocals = true;
                        originalMethod.Body.Variables.Add(new VariableDefinition(variable.Name, variable.VariableType));
                    }
                    originalMethod.Body.Emit(il =>
                    {
                        foreach (var instruction in Method.Body.Instructions)
                        {
                            il.Append(instruction);
                        }
                    });
                    Proxy.Methods.Add(originalMethod);                
                
                    return originalMethod;
                }
            }

            protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
            {
                if (methodInfo.IsAbstract)
                    CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
                else
                    base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, emitProceedTarget, proceedTargetMethod, proceedOpCode);
            }
        }
    }
}