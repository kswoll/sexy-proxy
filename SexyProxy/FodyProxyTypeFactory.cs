using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SexyProxy
{
    public class FodyProxyTypeFactory : IProxyTypeFactory
    {
        private static ConcurrentDictionary<Type, Type> proxyTypesBySourceType = new ConcurrentDictionary<Type, Type>();

        static FodyProxyTypeFactory()
        {
            Action<Assembly> checkAssembly = assembly =>
            {
                foreach (var attribute in assembly.GetCustomAttributes<ProxyForAttribute>())
                {
                    var sourceType = attribute.Type;
                    var proxyType = GetProxyType(assembly, sourceType);
                    proxyTypesBySourceType[sourceType] = proxyType;
                }
            };
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                checkAssembly(args.LoadedAssembly);
            };
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                checkAssembly(assembly);
            }
        }

        private static Type GetProxyType(Assembly assembly, Type sourceType)
        {
            var name = sourceType.FullName.Split('`')[0] + "$Proxy";
            if (sourceType.IsGenericType)
                name += "`" + sourceType.GenericTypeArguments.Length;
            var proxyType = assembly.GetType(name) ?? Type.GetType(name);
            return proxyType;
        }

        public static bool IsFodyProxy(Type sourceType)
        {
            return proxyTypesBySourceType.ContainsKey(sourceType) || GetProxyType(sourceType.Assembly, sourceType) != null;
        }

        public Type CreateProxyType(Type sourceType)
        {
            Type proxyType;
            if (proxyTypesBySourceType.TryGetValue(sourceType, out proxyType))
                return proxyType;

            proxyType = GetProxyType(sourceType.Assembly, sourceType);
            if (proxyType.ContainsGenericParameters)
            {
                proxyType = proxyType.MakeGenericType(sourceType.GetGenericArguments());
            }
            return proxyType;
        }
    }
}
