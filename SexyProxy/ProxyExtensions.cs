using System;

namespace SexyProxy
{
    public static class ProxyExtensions
    {
        /// <summary>
        /// Call this to get the active invocation in your proxy class.
        /// </summary>
        /// <param name="proxy">Your proxy instance.</param>
        /// <returns>An Invocation instance that allows you to proceed with the proxy invocation.</returns>
        public static Invocation Invocation(this IReverseProxy proxy)
        {
            throw new Exception("This method should never actually be executed.  Seeing this exception means something failed with proxy generation.");
        }
    }
}