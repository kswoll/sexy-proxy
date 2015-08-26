using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public abstract class ClassWeaver
    {
        protected abstract TypeDefinition GetProxyType();
        protected abstract MethodDefinition GetStaticConstructor();

        public WeaverContext Context { get; }
        public TypeDefinition SourceType { get; }
        public TypeDefinition ProxyType { get; private set; }
        public IEnumerable<MethodDefinition> Methods { get; private set; }
        public MethodDefinition StaticConstructor { get; private set; }

        protected ClassWeaver(WeaverContext context, TypeDefinition sourceType)
        {
            Context = context;
            SourceType = sourceType;
        }

        protected virtual void InitializeProxyType()
        {
        }

        protected virtual IEnumerable<MethodDefinition> GetMethods()
        {
            var methods = SourceType.Methods.Where(x => !x.IsStatic);
            return methods;
        }

        protected virtual void Finish()
        {
        }

        public void Execute()
        {
            ProxyType = GetProxyType();
            InitializeProxyType();
            Methods = GetMethods();
            StaticConstructor = GetStaticConstructor();

            // Now implement/override all methods
            foreach (var methodInfo in Methods.ToArray())
            {
                var parameterInfos = methodInfo.Parameters;

                // Finalize doesn't work if we try to proxy it and really, who cares?
                if (methodInfo.Name == "Finalize" && parameterInfos.Count == 0 && methodInfo.DeclaringType.CompareTo(Context.ModuleDefinition.TypeSystem.Object.Resolve()))
                    continue;
                if (methodInfo.IsConstructor)
                    continue;

                var proceedMethodTarget = GetProceedMethodTarget(methodInfo);

                Context.LogInfo($"{proceedMethodTarget}");
                ProxyMethod(methodInfo, methodInfo.Body, proceedMethodTarget);
            }

            StaticConstructor.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ret);
            });

            Finish();
        }
    }
}