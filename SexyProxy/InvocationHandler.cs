using System;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class InvocationHandler
    {
        private Func<Invocation, Task<object>> asyncHandler;
        private Func<Invocation, object> handler;
        private Func<object, bool> proxyPredicate;

        /// <summary>
        /// Provides a handler for all methods, async or otherwise.
        /// </summary>
        public InvocationHandler(Func<Invocation, Task<object>> asyncHandler, Func<object, bool> proxyPredicate = null)
        {
            this.asyncHandler = asyncHandler;
            this.proxyPredicate = proxyPredicate ?? (x => true);
        }

        /// <summary>
        /// Provides a handler that works for non-async methods only.  This is useful if you want to avoid the async
        /// overhead in scenarios where you know you don't want to handle async methods anyway.  (Such async methods
        /// will simply operate without interception)
        /// </summary>
        public InvocationHandler(Func<Invocation, object> handler, Func<object, bool> proxyPredicate = null)
        {
            this.handler = handler;
            this.proxyPredicate = proxyPredicate ?? (x => true);
        }

        public bool IsHandlerActive(object proxy)
        {
            return proxyPredicate(proxy);
        }

        private Task<object> GetTask(Invocation invocation)
        {
            Task<object> task;
            if (asyncHandler != null)
                task = asyncHandler(invocation);
            else
                task = invocation.Proceed();
            if (!typeof(Task).IsAssignableFrom(invocation.Method.ReturnType) && !task.IsCompleted)
                throw new InvalidAsyncException(
                    "Cannot use async tasks (await) in proxy handler for methods with a non-Task return-type");
            return task;
        }

        public async Task<T> AsyncInvokeT<T>(AsyncInvocationT<T> invocation)
        {
            var task = GetTask(invocation);
            var result = await task;
            return (T)result;
        }

        public async Task VoidAsyncInvoke(VoidAsyncInvocation invocation)
        {
            var task = GetTask(invocation);
            await task;
        }

        public T InvokeT<T>(InvocationT<T> invocation)
        {
            if (handler != null)
                return (T)handler(invocation);

            var task = GetTask(invocation);
            return (T)task.Result;
        }

        public void VoidInvoke(VoidInvocation invocation)
        {
            if (handler != null)
            {
                handler(invocation);
                return;
            }

            var task = GetTask(invocation);
            task.Wait();
        }
    }
}
