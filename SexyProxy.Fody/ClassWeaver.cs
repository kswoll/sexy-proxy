using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public abstract class ClassWeaver
    {
        protected abstract TypeDefinition GetProxyType();
        protected abstract MethodDefinition GetStaticConstructor();
        protected abstract void ProxyMethod(MethodDefinition methodInfo);

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
            foreach (var methodInfo in Methods)
            {
                var parameterInfos = methodInfo.Parameters;

                // Finalize doesn't work if we try to proxy it and really, who cares?
                if (methodInfo.Name == "Finalize" && parameterInfos.Count == 0 && methodInfo.DeclaringType.CompareTo(Context.ModuleDefinition.TypeSystem.Object.Resolve()))
                    continue;

                ProxyMethod(methodInfo);
            }

            StaticConstructor.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ret);
            });

            Finish();
        }
    }
}