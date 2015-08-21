# sexy-proxy
Async-enabled proxy generator with support for either compile-time or runtime 
based generation.  By "proxy generator" we mean being able to take an existing 
class or interface and inject an interceptor that can generically handle each 
method in the source type and choose when and whether to pass the invocation off
to the original implementation (if any).  The idea is similar to [Castle's 
DynamicProxy](http://www.castleproject.org/projects/dynamicproxy/) but in 
contrast to that library, *sexy-proxy* is fully async/await enabled.  By that, we 
mean the methods around which you are proxying can return `Task` or `Task<T>` 
and your *interceptor* itself can use `async` and `await` without having to 
fuss with awkward uses of `.ContinueWith`.

## Installation

Install using nuget:

    Install-Package sexy-proxy
	
## Getting Started

To get started we'll demonstrate a simple example using a virtual method in a 
base class.  We will proxy that class and intercept the method, logging to a 
`StringWriter` (asynchronously!) both before and after **proceeding** to the 
original implementation.  This sample will use the `Reflection.Emit` version 
because there are fewer dependencies and is thus simper for a first example.

To start, let's consider the following class:

    public class HelloWorldPrinter
    {
        public async virtual Task SayHello(TextWriter writer)
        {
            await writer.WriteAsync("Hello World!");
        }
    }

Ordinarilly, you could use this method like so:

    var writer = new StringWriter();
    await new HelloWorldPrinter().SayHello(writer);

This would result in the string "Hello World!" being written to your 
`StringWriter`.  But now let's use a proxy to change the data written to the 
writer such that the full string written is:

> John says, "Hello World!"

 `HelloWorldPrinter` can stay the same, but the callsite will change to:

    1) var writer = new StringWriter();
    2) var printer = Proxy.CreateProxy<HelloWorldPrinter>(async invocation =>
    3) {
    4)    await writer.WriteAsync("John says, \"");
    5)    await invocation.Proceed();
    6)    await writer.WriteAsync("\"");
    7)    return null;
    8) });
    9) await printer.SayHello(writer);

Here we're storing off the printer in a separate variable (for clarity) and 
creating a proxy around `HelloWorldPrinter` with an invocation handler responsible
for handling the method's interception.  Let's break this down into smaller pieces:

1. We create the proxy itself via `Proxy.CreateProxy<HelloWorldPrinter>(...)`
2. The argument to `CreateProxy` expects a function that either returns an 
`object` or returns a `Task<object>`.  The latter demonstrates async semantics,
so we'll use that for our example.
3. The argument passed to your function is an instance of `Invocation` (more 
details on that later)  For now, suffice to say that it allows us to invoke the 
original behavior of the `SayHello` at a time of our choosing.
4. Line 4) asynchronously writes the string `John says, "` to the writer.
5. Line 5) asynchronously calls the original implementation of `SayHello`, which 
writes the string `Hello World!` to the writer.
6. Line 6) asynchronously closes the quotation by writing `"` to the writer.
7. Finally, we invoke `SayHello` as before on line 9).

The end result of this is the string `John says, "Hello World!"` written to the 
writer as we expected. 


## Overview

*sexy-proxy* provides two key ways of generating the proxy.  One uses 
`Reflection.Emit` and is simple and works out of the box.  (it has no dependencies)
However, it has two key limitations:

* It requires `Reflection.Emit` -- this is a problem for iOS apps.
* It requires that proxied methods be virtual or that the proxy type be an interface.
* Any costs associated with generating a proxy (admitedly fairly minimal) are incurrred
at runtime rather than compile time.

If any of these concerns are paramount, *sexy-proxy* provides an alternative generator
that uses [Fody](https://github.com/Fody/Fody). This is a tool that allows the proxies
to be generated at compile-time.  This addresses all the aforementioned issues. The 
downsides of using Fody are minimal:

* The generation happens at compile-time, so any penalty incurred for generating proxies
must be dealt with every time you compile. So, for example, it can (slightly) slow down 
your iterations when leveraging TDD.
* On your proxy'd type you must either:
    * Decorate it with the `[Proxy]` attribute
    * Implement the interface `IProxy`