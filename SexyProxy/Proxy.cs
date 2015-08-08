using System;
using System.Threading.Tasks;
using SexyProxy.Emit;

namespace SexyProxy
{
    /// <summary>
    /// Generates proxies for the following usage scenarios:<ul>
    /// 
    /// <li>Create a proxy on a base class where the proxy class should override virtual methods
    ///     in the base class, making those methods available to the proxy.  In this context, 
    ///     Invocation.Proceed invokes the base method implementation.</li>
    /// 
    /// <li>Create a proxy on an interface while supplying a target implementation of that 
    ///     interface.  In this context, Invocation.Proceed invokes the method on the provided
    ///     target.</li>
    /// 
    /// <li>Create a proxy on an interface, not providing any target.  In this context, 
    ///     Invocation.Proceed does nothing.</li>
    /// 
    /// </ul>
    /// 
    /// <b>Note:</b> Generated implementations are stored in a static field of the generic
    /// Proxy&lt;T&gt; class.  These are instantiated upon first access of a particular
    /// variant of that class (variant on the type argument), which solves any thread
    /// contention issues.
    /// </summary>
    public static class Proxy
    {
        /// <summary>
        /// Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        /// If T is an interface, the target should be an implementation of that interface. In 
        /// this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        /// at the calling site is of that interface.  In other words, if the calling site has the
        /// <i>target</i> declared as the concrete implementation, the proxy will be generated
        /// for the implementation, rather than for the interface.
        /// 
        /// If T is a class, the target should be an instance of that class, and a subclassing 
        /// proxy will be created for it.  However, because target is specified in this case, 
        /// the base class behavior will be ignored (it will all be delegated to the target).
        /// </summary>
        /// <typeparam name="T">The type to create the proxy for.  May be an interface or a 
        /// concrete base class.</typeparam>
        /// <param name="target">The instance of T that should be the recipient of all invocations
        /// on the proxy via Invocation.Proceed.</param>
        /// <param name="invocationHandler">This is where you get to inject your logic.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(T target, Func<Invocation, Task<object>> invocationHandler)
        {
            return Proxy<T>.CreateProxy(target, invocationHandler);
        }

        /// <summary>
        /// Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        /// If T is an interface, the target should be an implementation of that interface. In 
        /// this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        /// at the calling site is of that interface.  In other words, if the calling site has the
        /// <i>target</i> declared as the concrete implementation, the proxy will be generated
        /// for the implementation, rather than for the interface.
        /// 
        /// If T is a class, the target should be an instance of that class, and a subclassing 
        /// proxy will be created for it.  However, because target is specified in this case, 
        /// the base class behavior will be ignored (it will all be delegated to the target).
        /// </summary>
        /// <typeparam name="T">The type to create the proxy for.  May be an interface or a 
        /// concrete base class.</typeparam>
        /// <param name="target">The instance of T that should be the recipient of all invocations
        /// on the proxy via Invocation.Proceed.</param>
        /// <param name="invocationHandler">This is where you get to inject your logic.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(T target, Func<Invocation, object> invocationHandler)
        {
            return CreateProxy(target, invocation => Task.FromResult(invocationHandler(invocation)));
        }

        /// <summary>
        /// Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        /// If T is an interface, the target should be an implementation of that interface. In 
        /// this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        /// at the calling site is of that interface.  In other words, if the calling site has the
        /// <i>target</i> declared as the concrete implementation, the proxy will be generated
        /// for the implementation, rather than for the interface.
        /// 
        /// If T is a class, the target should be an instance of that class, and a subclassing 
        /// proxy will be created for it.  However, because target is specified in this case, 
        /// the base class behavior will be ignored (it will all be delegated to the target).
        /// </summary>
        /// <typeparam name="T">The type to create the proxy for.  May be an interface or a 
        /// concrete base class.</typeparam>
        /// <param name="invocationHandler">This is where you get to inject your logic.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(Func<Invocation, Task<object>> invocationHandler)
        {
            return CreateProxy(default(T), invocationHandler);
        }

        /// <summary>
        /// Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        /// If T is an interface, the target should be an implementation of that interface. In 
        /// this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        /// at the calling site is of that interface.  In other words, if the calling site has the
        /// <i>target</i> declared as the concrete implementation, the proxy will be generated
        /// for the implementation, rather than for the interface.
        /// 
        /// If T is a class, the target should be an instance of that class, and a subclassing 
        /// proxy will be created for it.  However, because target is specified in this case, 
        /// the base class behavior will be ignored (it will all be delegated to the target).
        /// </summary>
        /// <typeparam name="T">The type to create the proxy for.  May be an interface or a 
        /// concrete base class.</typeparam>
        /// <param name="invocationHandler">This is where you get to inject your logic.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(Func<Invocation, object> invocationHandler)
        {
            return CreateProxy(default(T), invocation => Task.FromResult(invocationHandler(invocation)));
        }
    }

    public static class Proxy<T>
    {
        private static bool isInPlace = typeof(IProxy).IsAssignableFrom(typeof(T));
        private static bool isSetInvocationHandler = isInPlace && typeof(ISetInvocationHandler).IsAssignableFrom(typeof(T));
        private static Type proxyType = CreateProxyType();

        public static T CreateProxy(T target, Func<Invocation, Task<object>> invocationHandler)
        {
            if (isSetInvocationHandler)
            {
                var result = (T)Activator.CreateInstance(proxyType);
                ((ISetInvocationHandler)result).InvocationHandler = new InvocationHandler(invocationHandler);
                return result;
            }
            else if (isInPlace)
            {
                return (T)Activator.CreateInstance(proxyType, new InvocationHandler(invocationHandler));
            }
            else
            {
                return (T)Activator.CreateInstance(proxyType, target, new InvocationHandler(invocationHandler));
            }
        }

        private static Type CreateProxyType()
        {
            if (isInPlace)
                return typeof(T);
            else if (Attribute.IsDefined(typeof(T), typeof(ProxyAttribute)))
                return new FodyProxyTypeFactory().CreateProxyType(typeof(T));
            else
                return new EmitProxyTypeFactory().CreateProxyType(typeof(T));
        }
    }
}