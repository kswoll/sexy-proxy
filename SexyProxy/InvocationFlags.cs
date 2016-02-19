using System;

namespace SexyProxy
{
    [Flags]
    public enum InvocationFlags
    {
        None = 0,
        Void = 1,
        Async = 2
    }
}
