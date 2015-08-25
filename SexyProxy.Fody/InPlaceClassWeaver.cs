using System;
using System.Diagnostics;
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

        protected override OpCode GetProceedCallOpCode(MethodDefinition methodInfo)
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
            il.Emit(OpCodes.Ldarg_0);
        }

        protected override void ProxyMethod(MethodDefinition methodInfo, MethodBody body, MethodReference proceedTargetMethod)
        {
//            Debugger.Launch();
            if (methodInfo.ReturnType.CompareTo(Context.InvocationHandlerType) && methodInfo.Name == "get_InvocationHandler") 
                return;

            methodInfo.Body = body = new MethodBody(methodInfo);

            base.ProxyMethod(methodInfo, body, proceedTargetMethod);

            if (methodInfo.IsAbstract)
            {
                methodInfo.IsAbstract = false;
            }
        }

        protected override MethodReference GetProceedMethodTarget(MethodDefinition methodInfo)
        {
            if (methodInfo.IsAbstract)
            {
                return base.GetProceedMethodTarget(methodInfo);
            }
            else
            {
                // Transplant the original method into a new one that can be invoked when calling proceed
                var originalMethod = new MethodDefinition(methodInfo.Name + "$Original", MethodAttributes.Private, methodInfo.ReturnType);
                foreach (var parameter in methodInfo.Parameters)
                {
                    originalMethod.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType));
                }
                originalMethod.Body = new MethodBody(originalMethod);
                foreach (var variable in methodInfo.Body.Variables)
                {
                    originalMethod.Body.InitLocals = true;
                    originalMethod.Body.Variables.Add(new VariableDefinition(variable.Name, variable.VariableType));
                }
                originalMethod.Body.Emit(il =>
                {
                    foreach (var instruction in methodInfo.Body.Instructions)
                    {
                        il.Append(instruction);
                    }
                });
                ProxyType.Methods.Add(originalMethod);                
                
                return base.GetProceedMethodTarget(originalMethod);
            }
        }

        protected override void ImplementProceed(MethodDefinition methodInfo, MethodBody methodBody, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod, Action<ILProcessor> emitProceedTarget, MethodReference proceedTargetMethod, OpCode proceedOpCode)
        {
            if (methodInfo.IsAbstract)
                CecilExtensions.CreateDefaultMethodImplementation(methodInfo, il);
            else
                base.ImplementProceed(methodInfo, methodBody, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, invokeMethod, emitProceedTarget, proceedTargetMethod, proceedOpCode);
        }

        protected override void Finish()
        {
            ProxyType.IsAbstract = false;

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

        protected override void ImplementBody(MethodDefinition methodInfo, ILProcessor il, FieldReference methodInfoField, MethodReference proceed, MethodReference proceedDelegateTypeConstructor, TypeReference invocationType, MethodReference invocationConstructor, MethodReference invokeMethod)
        {
//            Debugger.Launch();
            base.ImplementBody(methodInfo, il, methodInfoField, proceed, proceedDelegateTypeConstructor, invocationType, invocationConstructor, invokeMethod);
        }
    }
}