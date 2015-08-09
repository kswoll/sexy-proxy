using System;
using System.Linq;
using System.Reflection;

namespace SexyProxy
{
    public class FodyProxyTypeFactory : IProxyTypeFactory
    {
        public Type CreateProxyType(Type sourceType)
        {
            var proxyType = sourceType.Assembly.GetType(sourceType.FullName.Split('[').First().Replace('`', '$') + "$Proxy");
            if (proxyType.ContainsGenericParameters)
            {
                proxyType = proxyType.MakeGenericType(sourceType.GetGenericArguments());
            }
            return proxyType;
        }
    }
}
