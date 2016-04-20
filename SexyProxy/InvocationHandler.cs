using System;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class InvocationHandler
    {
        private Func<Invocation, Task<object>> handler;
        private Func<object, bool> proxyPredicate;

        public InvocationHandler(Func<Invocation, Task<object>> handler, Func<object, bool> proxyPredicate = null)
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
            var task = handler(invocation);
            if (!typeof (Task).IsAssignableFrom(invocation.Method.ReturnType) && !task.IsCompleted)
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
            var task = GetTask(invocation);
            return (T)task.Result;
        }

        public void VoidInvoke(VoidInvocation invocation)
        {
            var task = GetTask(invocation);
            task.Wait();
        }
    }
}
