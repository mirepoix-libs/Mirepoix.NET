namespace Mirepoix.AccessControl.Hosting;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AccessResourceAttribute : Attribute
{
    public AccessResourceAttribute(string resourceType, string idRouteKey = "id")
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        IdRouteKey = idRouteKey ?? throw new ArgumentNullException(nameof(idRouteKey));
    }

    public string ResourceType { get; }

    public string IdRouteKey { get; }
}
