namespace SexyProxy.Tests.ExternalLibrary
{
    public abstract class SomeService
    {
        public virtual string GetString(string input)
        {
            return "foo";
        }

        public abstract string GetInt(int input);
    }
}