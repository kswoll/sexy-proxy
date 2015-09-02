using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SexyProxy.Fody
{
    public abstract class ClassWeaver
    {
        protected abstract MethodWeaver CreateMethodWeaver(MethodDefinition methodInfo, string name);
        protected abstract TypeDefinition GetProxyType();
        protected abstract MethodDefinition GetStaticConstructor();

        public WeaverContext Context { get; }
        public TypeDefinition SourceType { get; }
        public TypeReference SourceTypeReference { get; }
        public TypeDefinition ProxyType { get; private set; }
        public IEnumerable<MethodDefinition> Methods { get; private set; }
        public MethodDefinition StaticConstructor { get; private set; }
        public bool ContainsAbstractNonProxiedMethod { get; private set; }

        protected ClassWeaver(WeaverContext context, TypeDefinition sourceType)
        {
            Context = context;
            SourceType = sourceType;
            SourceTypeReference = context.ModuleDefinition.Import(sourceType);
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
            var uniqueNames = new Dictionary<string, int>();
            var propertiesByAccessor = SourceType.Properties
                .Select(x => new { Property = x, Accessor = x.GetMethod })
                .Concat(SourceType.Properties.Select(x => new { Property = x, Accessor = x.SetMethod }))
                .Where(x => x.Accessor != null)
                .ToDictionary(x => x.Accessor, x => x.Property);

            // Now implement/override all methods
            foreach (var methodInfo in Methods.ToArray())
            {
                var parameterInfos = methodInfo.Parameters;

                // Finalize doesn't work if we try to proxy it and really, who cares?
                if (methodInfo.Name == "Finalize" && parameterInfos.Count == 0 && methodInfo.DeclaringType.CompareTo(Context.ModuleDefinition.TypeSystem.Object.Resolve()))
                    continue;
                if (methodInfo.IsConstructor)
                    continue;
                if (((methodInfo.IsGetter || methodInfo.IsSetter) && propertiesByAccessor[methodInfo].IsDefined(Context.DoNotProxyAttribute)) ||
                    (!methodInfo.IsGetter && !methodInfo.IsSetter && methodInfo.IsDefined(Context.DoNotProxyAttribute)))
                {
                    if (methodInfo.IsAbstract)
                        ContainsAbstractNonProxiedMethod = true;
                    continue;
                }

                // Generate a unique name for the method in the event of overloads.  
                var name = methodInfo.Name;
                int index;
                if (uniqueNames.TryGetValue(name, out index))
                {
                    uniqueNames[name] = index + 1;
                    name += "$" + index;
                }
                else
                {
                    uniqueNames[name] = 2;
                }
                var methodWeaver = CreateMethodWeaver(methodInfo, name);
                methodWeaver.DefineProxy();
            }

            StaticConstructor.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ret);
            });

            Finish();
        }
    }
}