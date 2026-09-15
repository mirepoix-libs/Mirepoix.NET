namespace Mirepoix.AccessControl.Hosting;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AccessOperationAttribute : Attribute
{
    public AccessOperationAttribute(string operation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public string Operation { get; }
}
