using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SexyProxy.Fody
{
    public abstract class TargetedClassWeaver : ClassWeaver
    {
        public FieldReference Target { get; private set; }
        public FieldDefinition InvocationHandler { get; private set; }

        protected TargetedClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override IEnumerable<MethodDefinition> GetMethods()
        {
            var methods = SourceType.Methods.Where(x => !x.IsStatic && x.IsVirtual);
            return methods;
        }

        protected virtual TypeReference GetBaseType(GenericParameter[] genericParameters) => !SourceType.HasGenericParameters ? (TypeReference)SourceType : SourceType.MakeGenericInstanceType(ProxyType.GenericParameters.ToArray());

        protected virtual TypeReference[] GetInterfaces(GenericParameter[] genericParameters)
        {
            return new TypeReference[0];
        }

        protected virtual TypeReference GetSourceType() => GetBaseType(ProxyType.GenericParameters.ToArray());

        protected override TypeDefinition GetProxyType()
        {
            var visibility = SourceType.Attributes & (TypeAttributes.Public | TypeAttributes.NestedPrivate | 
                TypeAttributes.NestedFamily | TypeAttributes.NestedAssembly | TypeAttributes.NestedPublic | 
                TypeAttributes.NestedFamANDAssem | TypeAttributes.NestedFamORAssem);
            var name = SourceType.Name.Split('`')[0] + "$Proxy";
            if (SourceType.GenericParameters.Any())
                name += "`" + SourceType.GenericParameters.Count;
            var type = new TypeDefinition(SourceType.Namespace, name, visibility);
            SourceType.CopyGenericParameters(type);
            type.BaseType = GetBaseType(type.GenericParameters.ToArray());
            var intfs = GetInterfaces(type.GenericParameters.ToArray());
            foreach (var intf in intfs)
                type.Interfaces.Add(intf);
            return type;
        }

        protected override void InitializeProxyType()
        {
            base.InitializeProxyType();

            var targetDefinition = new FieldDefinition("$target", FieldAttributes.Private, GetSourceType());
            ProxyType.Fields.Add(targetDefinition);
            Target = targetDefinition;

            // Create invocationHandler field
            InvocationHandler = new FieldDefinition("$invocationHandler", FieldAttributes.Private, Context.InvocationHandlerType);
            ProxyType.Fields.Add(InvocationHandler);

            CreateConstructor();
        }

        protected virtual void CreateConstructor()
        {
            // Create constructor 
            var constructorWithTarget = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, Context.ModuleDefinition.TypeSystem.Void);
            constructorWithTarget.Parameters.Add(new ParameterDefinition(GetSourceType()));
            constructorWithTarget.Parameters.Add(new ParameterDefinition(Context.InvocationHandlerType));
            ProxyType.Methods.Add(constructorWithTarget);
            constructorWithTarget.Body = new MethodBody(constructorWithTarget);
            constructorWithTarget.Body.Emit(il =>
            {
                il.EmitDefaultBaseConstructorCall(ProxyType);
                il.Emit(OpCodes.Ldarg_0);  // Put "this" on the stack for the subsequent stfld instruction way below
                il.Emit(OpCodes.Ldarg_1);  // Put "target" argument on the stack
                il.Emit(OpCodes.Stfld, Target);                
    
                il.Emit(OpCodes.Ldarg_0);                      // Load "this" for subsequent call to stfld
                il.Emit(OpCodes.Ldarg_2);                      // Load the 2nd argument, which is the invocation handler
                il.Emit(OpCodes.Stfld, InvocationHandler);     // Store it in the invocationHandler field
                il.Emit(OpCodes.Ret);                          // Return from the constructor (a ret call is always required, even for void methods and constructors)
            });
        }

        protected override MethodDefinition GetStaticConstructor()
        {
            var staticConstructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, Context.ModuleDefinition.TypeSystem.Void);
            staticConstructor.Body = new MethodBody(staticConstructor);
            ProxyType.Methods.Add(staticConstructor);
            return staticConstructor;
        }

        protected override void Finish()
        {
            base.Finish();

            if (SourceType.DeclaringType == null)
            {
                Context.ModuleDefinition.Types.Add(ProxyType);
            }
            else
            {
                SourceType.DeclaringType.NestedTypes.Add(ProxyType);
            }
        }

        protected abstract class TargetedMethodWeaver : MethodWeaver
        {
            protected abstract MethodAttributes GetMethodAttributes();

            protected readonly FieldReference target;
            protected readonly FieldDefinition invocationHandler;

            protected TargetedMethodWeaver(WeaverContext context, TypeDefinition source, TypeDefinition proxy, MethodDefinition method, string name, MethodDefinition staticConstructor, FieldReference target, FieldDefinition invocationHandler) : base(context, source, proxy, method, name, staticConstructor)
            {
                this.target = target;
                if (Proxy.GenericParameters.Any())
                    this.target = target.Bind(proxy.MakeGenericInstanceType(Proxy.GenericParameters.ToArray()));

                this.invocationHandler = invocationHandler;
            }

            protected override void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
            {
                MethodAttributes methodAttributes = GetMethodAttributes();

                // Define the actual method
                var parameterInfos = Method.Parameters;
                var method = new MethodDefinition(Method.Name, methodAttributes, Method.ReturnType.ResolveGenericParameter(Proxy));
                foreach (var parameterType in parameterInfos.Select(x => x.ParameterType).ToArray())
                {
                    method.Parameters.Add(new ParameterDefinition(parameterType));
                }
                Method.CopyGenericParameters(method);
                Proxy.Methods.Add(method);

                method.Body = new MethodBody(method);

                base.ProxyMethod(method.Body, proceedTargetMethod);
            }

            protected override void EmitInvocationHandler(ILProcessor il)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, invocationHandler);
            }

            protected override void EmitProceedTarget(ILProcessor il)
            {
                EmitProxyFromProceed(il);
                il.Emit(OpCodes.Ldfld, target);
            }
        }
    }
}