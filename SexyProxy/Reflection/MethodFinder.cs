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
        private static Dictionary<string, PropertyInfo> propertiesBySignature = new Dictionary<string, PropertyInfo>();

        static MethodFinder()
        {
            var type = typeof(T);
            if (type.IsGenericType)
                type = type.GetGenericTypeDefinition();

            foreach (var item in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(x => x.MetadataToken).Zip(
                typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(x => x.MetadataToken), (baseInfo, realInfo) => new { BaseInfo = baseInfo, RealInfo = realInfo }))
            {
                var signature = MethodFinder.GenerateSignature(item.BaseInfo);
                methodsBySignature[signature] = item.RealInfo;
            }
            foreach (var item in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(x => x.MetadataToken).Zip(
                typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(x => x.MetadataToken), (baseInfo, realInfo) => new { BaseInfo = baseInfo, RealInfo = realInfo }))
            {
                if (item.RealInfo.GetMethod != null)
                {
                    var signature = MethodFinder.GenerateSignature(item.BaseInfo.GetMethod);
                    propertiesBySignature[signature] = item.RealInfo;
                }
                if (item.RealInfo.SetMethod != null)
                {
                    var signature = MethodFinder.GenerateSignature(item.BaseInfo.SetMethod);
                    propertiesBySignature[signature] = item.RealInfo;
                }
            }
        }

        public static MethodInfo FindMethod(string signature)
        {
            MethodInfo method;
            if (!methodsBySignature.TryGetValue(signature, out method))
                throw new Exception("Could not find method with signature: " + signature);
            return methodsBySignature[signature];
        }

        public static PropertyInfo FindProperty(string signature)
        {
            PropertyInfo property;
            if (!propertiesBySignature.TryGetValue(signature, out property))
                throw new Exception("Could not find property for accessor with signature: " + signature);
            return propertiesBySignature[signature];
        }
    }
}