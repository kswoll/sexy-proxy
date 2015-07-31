using System;
using System.Reflection;

namespace SexyProxy
{
    public class FodyProxyTypeFactory : IProxyTypeFactory
    {
        public Type CreateProxyType(Type sourceType)
        {
            var field = sourceType.GetField("$proxy", BindingFlags.Static | BindingFlags.NonPublic);
            return (Type)field.GetValue(null);
        }
    }
}
