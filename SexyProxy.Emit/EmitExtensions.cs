using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SexyProxy.Emit
{
    public static class EmitExtensions
    {
        private static readonly MethodInfo getTypeFromRuntimeHandleMethod = typeof(Type).GetMethod("GetTypeFromHandle");
        private static readonly MethodInfo typeGetMethod = typeof(Type).GetMethod("GetMethod",
            new[] { typeof(string), typeof(BindingFlags), typeof(Binder), typeof(Type[]), typeof(ParameterModifier[]) });

        public static void EmitDefaultBaseConstructorCall(this ILGenerator il, Type baseType)
        {
            Type constructorType = baseType;
            ConstructorInfo conObj = null;
            while (conObj == null)
            {
                constructorType = (constructorType == null ? baseType : constructorType.BaseType) ?? typeof(object);
                conObj = constructorType.GetConstructor(new Type[0]);
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, conObj);
        }

        public static void LoadType(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, getTypeFromRuntimeHandleMethod);
        }

        public static void StoreMethodInfo(this ILGenerator il, FieldBuilder staticField, MethodInfo method)
        {
            Type[] parameterTypes = method.GetParameters().Select(info => info.ParameterType).ToArray();

            // The type we want to invoke GetMethod upon
            il.LoadType(method.DeclaringType);

            // Arg1: methodName
            il.Emit(OpCodes.Ldstr, method.Name);

            // Arg2: bindingFlags
            il.Emit(OpCodes.Ldc_I4, (int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            // Arg3: binder
            il.Emit(OpCodes.Ldnull);

            // Arg4: parameterTypes
            il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            il.Emit(OpCodes.Newarr, typeof(Type));
            // Copy array for each element we are going to set
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Dup);
            }
            // Set each element 
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldc_I4, i);
                il.LoadType(parameterTypes[i]);
                il.Emit(OpCodes.Stelem, typeof(Type));
            }

            // Arg5: parameterModifiers
            il.Emit(OpCodes.Ldnull);

            // Invoke method
            il.EmitCall(OpCodes.Call, typeGetMethod, null);

            // Store MethodInfo into the static field
            il.Emit(OpCodes.Stsfld, staticField);
        }

        public static void EmitDefaultValue(this ILGenerator il, Type type)
        {
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(short) || type == typeof(int))
                il.Emit(OpCodes.Ldc_I4_0);
            else if (type == typeof(float))
                il.Emit(OpCodes.Ldc_R4, (float)0);
            else if (type == typeof(long))
                il.Emit(OpCodes.Ldc_I8);
            else if (type == typeof(double))
                il.Emit(OpCodes.Conv_R8);
            else if (type.IsValueType)
            {
                var local = il.DeclareLocal(type);
                il.Emit(OpCodes.Ldloca_S, local);
                il.Emit(OpCodes.Initobj, type);
                il.Emit(OpCodes.Ldloc, local);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }
        }
    }
}
