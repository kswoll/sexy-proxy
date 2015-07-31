using System;
using System.Reflection;

namespace SexyProxy
{
    public class FodyProxyTypeFactory : IProxyTypeFactory
    {
        public Type CreateProxyType(Type sourceType)
        {
            return sourceType.Assembly.GetType(sourceType.FullName + "$Proxy");
        }
    }
}
