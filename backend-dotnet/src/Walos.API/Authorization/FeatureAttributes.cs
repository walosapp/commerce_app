namespace Walos.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute : Attribute
{
    public RequireFeatureAttribute(string feature) => Feature = feature;

    public string Feature { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireAnyFeatureAttribute : Attribute
{
    public RequireAnyFeatureAttribute(params string[] features) => Features = features;

    public IReadOnlyList<string> Features { get; }
}
