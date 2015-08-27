using System;
using System.Linq;
using System.Reflection;

namespace SexyProxy
{
    public class FodyProxyTypeFactory : IProxyTypeFactory
    {
        public Type CreateProxyType(Type sourceType)
        {
            var name = sourceType.FullName.Split('`')[0] + "$Proxy";
            if (sourceType.IsGenericType)
                name += "`" + sourceType.GenericTypeArguments.Length;
            var proxyType = sourceType.Assembly.GetType(name);
            if (proxyType.ContainsGenericParameters)
            {
                proxyType = proxyType.MakeGenericType(sourceType.GetGenericArguments());
            }
            return proxyType;
        }
    }
}
