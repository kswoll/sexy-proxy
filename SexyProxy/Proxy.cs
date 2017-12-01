using System;
using System.Reflection;
using System.Threading.Tasks;

#if EMIT
using SexyProxy.Emit;
#endif

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
        public static T CreateProxy<T>(T target, InvocationHandler invocationHandler)
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
        /// <param name="predicate">Optional predicate to determine if the interception should happen.  Useful
        /// improving performance in certain situations</param>
        /// <param name="asyncMode">Controls what happens when an async invocation handler performs async related actions
        /// where the original method being proxied is not an async method.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxyAsync<T>(T target, Func<IAsyncInvocation, Task<object>> invocationHandler, ProxyPredicate<T> predicate = null,
            AsyncInvocationMode asyncMode = AsyncInvocationMode.Throw)
        {
            return Proxy<T>.CreateProxy(target, invocationHandler, predicate, asyncMode);
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
        /// <param name="predicate">Optional predicate to determine if the interception should happen.  Useful
        /// improving performance in certain situations</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(T target, Func<Invocation, object> invocationHandler, ProxyPredicate<T> predicate = null)
        {
            return Proxy<T>.CreateProxy(target, invocationHandler, predicate);
        }

        ///  <summary>
        ///  Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        ///  If T is an interface, the target should be an implementation of that interface. In
        ///  this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        ///  at the calling site is of that interface.  In other words, if the calling site has the
        ///  <i>target</i> declared as the concrete implementation, the proxy will be generated
        ///  for the implementation, rather than for the interface.
        ///
        ///  If T is a class, the target should be an instance of that class, and a subclassing
        ///  proxy will be created for it.  However, because target is specified in this case,
        ///  the base class behavior will be ignored (it will all be delegated to the target).
        ///  </summary>
        ///  <typeparam name="T">The type to create the proxy for.  May be an interface or a
        ///  concrete base class.</typeparam>
        ///  <param name="invocationHandler">This is where you get to inject your logic.</param>
        ///  <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(InvocationHandler invocationHandler)
        {
            return CreateProxy(default(T), invocationHandler);
        }

        ///  <summary>
        ///  Creates a proxy for a given type.  This method supports two discrete usage scenarios.<p/>
        ///  If T is an interface, the target should be an implementation of that interface. In
        ///  this scenario, T should be <i>explicitly</i> specified unless the type of <i>target</i>
        ///  at the calling site is of that interface.  In other words, if the calling site has the
        ///  <i>target</i> declared as the concrete implementation, the proxy will be generated
        ///  for the implementation, rather than for the interface.
        ///
        ///  If T is a class, the target should be an instance of that class, and a subclassing
        ///  proxy will be created for it.  However, because target is specified in this case,
        ///  the base class behavior will be ignored (it will all be delegated to the target).
        ///  </summary>
        ///  <typeparam name="T">The type to create the proxy for.  May be an interface or a
        ///  concrete base class.</typeparam>
        ///  <param name="invocationHandler">This is where you get to inject your logic.</param>
        ///  <param name="predicate">Optional predicate to determine if the interception should happen.  Useful
        ///  improving performance in certain situations</param>
        /// <param name="asyncMode">Controls what happens when an async invocation handler performs async related actions
        /// where the original method being proxied is not an async method.</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxyAsync<T>(Func<IAsyncInvocation, Task<object>> invocationHandler, ProxyPredicate<T> predicate = null,
            AsyncInvocationMode asyncMode = AsyncInvocationMode.Throw)
        {
            return CreateProxyAsync(default(T), invocationHandler, predicate, asyncMode);
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
        /// <param name="predicate">Optional predicate to determine if the interception should happen.  Useful
        /// improving performance in certain situations</param>
        /// <returns>The new instance of the proxy that is an instance of T</returns>
        public static T CreateProxy<T>(Func<Invocation, object> invocationHandler, ProxyPredicate<T> predicate = null)
        {
            return Proxy<T>.CreateProxy(default(T), invocationHandler, predicate);
        }
    }

    public static class Proxy<T>
    {
        private static bool isInPlace = typeof(IReverseProxy).IsAssignableFrom(typeof(T)) || typeof(IProxy).IsAssignableFrom(typeof(T));
        private static bool isSetAsyncInvocationHandler = isInPlace && typeof(ISetAsyncInvocationHandler).IsAssignableFrom(typeof(T));
        private static bool isSetInvocationHandler = isInPlace && typeof(ISetInvocationHandler).IsAssignableFrom(typeof(T));
        private static bool isFodyProxy = FodyProxyTypeFactory.IsFodyProxy(typeof(T));
        private static Type proxyType = CreateProxyType();

        public static T CreateProxy(T target, Func<IAsyncInvocation, Task<object>> invocationHandler, ProxyPredicate<T> predicate,
            AsyncInvocationMode asyncMode = AsyncInvocationMode.Throw)
        {
            if (isSetAsyncInvocationHandler)
            {
                var result = (T)Activator.CreateInstance(proxyType);
                ((ISetAsyncInvocationHandler)result).InvocationHandler = new AsyncInvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property), asyncMode);
                return result;
            }
            else if (isInPlace)
            {
                return (T)Activator.CreateInstance(proxyType, new AsyncInvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property), asyncMode));
            }
            else
            {
                return (T)Activator.CreateInstance(proxyType, target, new AsyncInvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property), asyncMode));
            }
        }

        public static T CreateProxy(T target, Func<Invocation, object> invocationHandler, ProxyPredicate<T> predicate)
        {
            if (isSetInvocationHandler)
            {
                var result = (T)Activator.CreateInstance(proxyType);
                ((ISetInvocationHandler)result).InvocationHandler = new InvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property));
                return result;
            }
            else if (isInPlace)
            {
                return (T)Activator.CreateInstance(proxyType, new InvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property)));
            }
            else
            {
                return (T)Activator.CreateInstance(proxyType, target, new InvocationHandler(invocationHandler, predicate == null ? (Func<object, MethodInfo, PropertyInfo, bool>)null : (x, method, property) => predicate((T)x, method, property)));
            }
        }

        public static T CreateProxy(T target, InvocationHandler invocationHandler)
        {
            if (isSetInvocationHandler)
            {
                var result = (T)Activator.CreateInstance(proxyType);
                ((ISetInvocationHandler)result).InvocationHandler = invocationHandler;
                return result;
            }
            else if (isInPlace)
            {
                return (T)Activator.CreateInstance(proxyType, invocationHandler);
            }
            else
            {
                return (T)Activator.CreateInstance(proxyType, target, invocationHandler);
            }
        }

        private static Type CreateProxyType()
        {
            if (isInPlace)
                return typeof(T);
#if EMIT
            else if (isFodyProxy)
                return new FodyProxyTypeFactory().CreateProxyType(typeof(T));
            else
            {
                var result = new EmitProxyTypeFactory().CreateProxyType(typeof(T));
                if (result.ContainsGenericParameters)
                {
                    result = result.MakeGenericType(typeof(T).GetGenericArguments());
                }
                return result;
            }
#else
            else
                throw new Exception("Emit generator is not available, so you must use Fody");
#endif
        }
    }
}