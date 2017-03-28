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
        public abstract MethodAttributes GetMethodAttributes(MethodDefinition methodInfo);

        protected TargetedClassWeaver(WeaverContext context, TypeDefinition sourceType) : base(context, sourceType)
        {
        }

        protected override IEnumerable<MethodDefinition> GetMethods()
        {
            var methods = SourceType.Methods.Where(x => !x.IsStatic && x.IsVirtual && !x.IsFinal);
            return methods;
        }

        protected virtual TypeReference GetBaseType(GenericParameter[] genericParameters) => !SourceType.HasGenericParameters ? SourceTypeReference : SourceTypeReference.MakeGenericInstanceType(ProxyType.GenericParameters.ToArray());

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
                type.Interfaces.Add(new InterfaceImplementation(intf));
            return type;
        }

        protected override void InitializeProxyType()
        {
            base.InitializeProxyType();

            var targetDefinition = new FieldDefinition("$target", FieldAttributes.Private, Context.ModuleDefinition.Import(GetSourceType()));
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
            constructorWithTarget.Parameters.Add(new ParameterDefinition(Context.ModuleDefinition.Import(GetSourceType())));
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
            public new TargetedClassWeaver ClassWeaver { get; }
            protected readonly FieldDefinition invocationHandler;
            protected FieldReference target;

            protected TargetedMethodWeaver(TargetedClassWeaver classWeaver, MethodDefinition method, string name, MethodDefinition staticConstructor, FieldReference target, FieldDefinition invocationHandler) : base(classWeaver, method, name, staticConstructor)
            {
                ClassWeaver = classWeaver;
                this.target = classWeaver.Target;
                if (classWeaver.ProxyType.GenericParameters.Any())
                    this.target = target.Bind(classWeaver.ProxyType.MakeGenericInstanceType(classWeaver.ProxyType.GenericParameters.ToArray()));

                this.invocationHandler = invocationHandler;
            }

            protected override void ProxyMethod(MethodBody body, MethodReference proceedTargetMethod)
            {
                MethodAttributes methodAttributes = ClassWeaver.GetMethodAttributes(Method);

                // Define the actual method
                var method = new MethodDefinition(Method.Name, methodAttributes, Method.ReturnType.ResolveGenericParameter(ClassWeaver.ProxyType));
                Method.CopyParameters(method);
                Method.CopyGenericParameters(method);
                ClassWeaver.ProxyType.Methods.Add(method);

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

            protected override void EmitOptOutTarget(ILProcessor il)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, target);
            }
        }
    }
}