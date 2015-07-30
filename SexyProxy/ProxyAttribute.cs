using System;

namespace SexyProxy
{
    /// <summary>
    /// Decorate on your proxy classes and interfaces.  Only necessary with the Fody plugin.  
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ProxyAttribute : Attribute
    {
    }
}
