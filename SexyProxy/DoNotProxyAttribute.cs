using System;

namespace SexyProxy
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class DoNotProxyAttribute : Attribute
    {
    }
}
