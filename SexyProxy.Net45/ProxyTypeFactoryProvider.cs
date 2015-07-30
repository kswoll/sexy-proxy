using SexyProxy.Emit;

namespace SexyProxy
{
    public static class ProxyTypeFactoryProvider
    {
        public static IProxyTypeFactory CreateProxyTypeFactory()
        {
            return new EmitProxyTypeFactory();
        }
    }
}