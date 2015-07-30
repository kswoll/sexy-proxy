using System;

namespace SexyProxy
{
    public interface IProxyTypeFactory
    {
        Type CreateProxyType(Type sourceType);
    }
}
