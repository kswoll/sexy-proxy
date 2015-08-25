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
            var type = new TypeDefinition(SourceType.Namespace, SourceType.Name.Replace('`', '$') + "$Proxy", visibility);
            foreach (var parameter in SourceType.GenericParameters)
            {
                var newParameter = new GenericParameter("$" + parameter.Name, type);
                foreach (var constraint in parameter.Constraints)
                {
                    newParameter.Constraints.Add(constraint);
                }
                type.GenericParameters.Add(newParameter);
            }
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

        protected virtual void PrepareTargetForConstructor(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_1);  // Put "target" argument on the stack
        }

        protected override void EmitInvocationHandler(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, InvocationHandler);
        }

        protected override void EmitProceedTarget(ILProcessor il)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, Target);
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

                PrepareTargetForConstructor(il);

                // Store whatever is on the stack inside the "target" field.  The value is either: 
                // * The "target" argument passed in -- if not null.
                // * If null and T is an interface type, then it is a struct that implements that interface and returns default values for each method
                // * If null and T is not an interface type, then it is "this", where "proceed" will invoke the base implementation.
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

        protected abstract MethodAttributes GetMethodAttributes(MethodDefinition methodInfo);

        protected override void ProxyMethod(MethodDefinition methodInfo, MethodBody body, MethodReference proceedTargetMethod)
        {
//                    var isImplemented = !isIntf && methodInfo.IsFinal;
            MethodAttributes methodAttributes = GetMethodAttributes(methodInfo);

            // Define the actual method
            var parameterInfos = methodInfo.Parameters;
            var method = new MethodDefinition(methodInfo.Name, methodAttributes, methodInfo.ReturnType.ResolveGenericParameter(ProxyType));
            foreach (var parameterType in parameterInfos.Select(x => x.ParameterType).ToArray())
                method.Parameters.Add(new ParameterDefinition(parameterType));
            ProxyType.Methods.Add(method);

            method.Body = new MethodBody(method);

            base.ProxyMethod(methodInfo, method.Body, proceedTargetMethod);
        }
    }
}