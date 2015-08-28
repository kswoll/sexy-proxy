using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SexyProxy.Reflection
{
    public static class MethodFinder<T>
    {
        private static Dictionary<string, MethodInfo> methodsBySignature = new Dictionary<string, MethodInfo>();

        static MethodFinder()
        {
            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var signature = GenerateSignature(method);
                methodsBySignature[signature] = method;
            }
        }

        public static string GetFriendlyName(Type type)
        {
            if (type.FullName == null)
                return type.Name;
            if (type.IsGenericType)
                return string.Format("{0}<{1}>", type.FullName.Split('[')[0], string.Join(", ", type.GetGenericArguments().Select(x => GetFriendlyName(x))));
            else
                return type.FullName;
        }

        private static string GenerateSignature(MethodInfo method)
        {
            return $"{method.Name}$$${method.GetGenericArguments().Length}$$$" +
                   $"{string.Join("$$", method.GetParameters().Select(x => GetFriendlyName(x.ParameterType).Replace(".", "$")))}";
        }

        public static MethodInfo FindMethod(string signature)
        {
            MethodInfo method;
            if (!methodsBySignature.TryGetValue(signature, out method))
                throw new Exception("Could not find method with signature: " + signature);
            return methodsBySignature[signature];
        }
    }
}
