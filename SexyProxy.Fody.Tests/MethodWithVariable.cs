using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
	[TestFixture]
	public class MethodWithVariable
	{
		[Test]
		public void Compiles()
		{
		}

		public class MethodWithVariableClass : IProxy
		{
			public InvocationHandler InvocationHandler { get; }

			public void MethodWithVariableMethod()
			{
				int i = 0;
			}
		}
	}
}