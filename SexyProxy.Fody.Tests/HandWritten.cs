using System.Threading.Tasks;

namespace SexyProxy.Fody.Tests
{
    [Proxy]
    public class HandWritten : IHandWritten
    {
        public const string GetStringAsyncReturnValue = "Some async string";
        public const string GetStringReturnValue = "Some non async string";

        public bool DoSomethingAsyncCalled { get; set; }
        public bool DoSomethingCalled { get; set; }

        public virtual async Task<string> GetStringAsync()
        {
            await Task.Delay(1);
            return GetStringAsyncReturnValue;
        }

        public virtual async Task DoSomethingAsync()
        {
            await Task.Delay(1);
            DoSomethingAsyncCalled = true;
        }

        public virtual string GetString()
        {
            return GetStringReturnValue;
        }

        public virtual void DoSomething()
        {
            DoSomethingCalled = true;
        }

        public virtual int Sum(int first, int second)
        {
            return first + second;
        }

        public virtual async Task<int> SumAsync(int first, int second)
        {
            await Task.Delay(1);
            return first + second;
        }

        public virtual string StringProperty { get; set; }
    }
}