using System;
using System.Reflection;

namespace SexyProxy
{
    public class InvocationHandler
    {
        private Func<Invocation, object> handler;
        private Func<object, MethodInfo, PropertyInfo, bool> proxyPredicate;

        /// <summary>
        /// Provides a handler that works for non-async methods only.  This is useful if you want to avoid the async
        /// overhead in scenarios where you know you don't want to handle async methods anyway.
        /// </summary>
        public InvocationHandler(Func<Invocation, object> handler, Func<object, MethodInfo, PropertyInfo, bool> proxyPredicate = null)
        {
            this.handler = handler;
            this.proxyPredicate = proxyPredicate ?? ((x, method, property) => true);
        }

        public bool IsHandlerActive(object proxy, MethodInfo method, PropertyInfo property)
        {
            return proxyPredicate(proxy, method, property);
        }

        public T InvokeT<T>(InvocationT<T> invocation)
        {
            return (T)handler(invocation);
        }

        public void VoidInvoke(VoidInvocation invocation)
        {
            handler(invocation);
        }
    }
}
