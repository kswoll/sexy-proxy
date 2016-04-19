using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SexyProxy.Reflection
{
    public static class MethodFinder
    {
        private static ConcurrentDictionary<MethodInfo, MethodInfo> originalMethods = new ConcurrentDictionary<MethodInfo, MethodInfo>();

        internal static string GenerateSignature(MethodInfo method)
        {
            return $"{method.Name}$$${method.GetGenericArguments().Length}$$$" +
                   $"{string.Join("$$", method.GetParameters().Select(x => GetFriendlyName(x.ParameterType).Replace(".", "$")))}";
        }

        public static string GetFriendlyName(Type type)
        {
            if (type.FullName == null)
                return type.Name;
            else if (type.IsGenericType)
                return $"{type.FullName.Split('[')[0]}<{string.Join(",", type.GetGenericArguments().Select(x => GetFriendlyName(x)))}>";
            else
                return type.FullName;
        }

        public static MethodInfo GetOriginalMethod(MethodInfo method)
        {
            return originalMethods.GetOrAdd(method, _ =>
            {
                foreach (var current in method.DeclaringType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    var originalMethodName = current.GetCustomAttribute<OriginalMethodAttribute>()?.Name;
                    if (originalMethodName != null)
                        originalMethods[current] = method.DeclaringType.GetMethod(originalMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
                }
                return originalMethods[method];
            });
        }
    }

    public static class MethodFinder<T>
    {
        private static Dictionary<string, MethodInfo> methodsBySignature = new Dictionary<string, MethodInfo>();
        private static Dictionary<string, PropertyInfo> propertiesByName = new Dictionary<string, PropertyInfo>();

        static MethodFinder()
        {
            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var signature = MethodFinder.GenerateSignature(method);
                methodsBySignature[signature] = method;
            }
            foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                propertiesByName[property.Name] = property;
            }
        }

        public static MethodInfo FindMethod(string signature)
        {
            MethodInfo method;
            if (!methodsBySignature.TryGetValue(signature, out method))
                throw new Exception("Could not find method with signature: " + signature);
            return methodsBySignature[signature];
        }

        public static PropertyInfo FindProperty(string name)
        {
            return propertiesByName[name];
        }
    }
}