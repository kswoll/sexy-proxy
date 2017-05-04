namespace SexyProxy
{
    /// <summary>
    /// Controls what happens when an async invocation handler performs async related actions
    /// where the original method being proxied is not an async method.
    /// </summary>
    public enum AsyncInvocationMode
    {
        /// <summary>
        /// If the invocation handler does not return a task in a completed state, and the
        /// proxied method is not an async method, throw an exception.
        /// </summary>
        Throw,

        /// <summary>
        /// If the invocation handler does not return a task in a completed state, call
        /// `Task.Wait();`
        /// </summary>
        Wait
    }
}
