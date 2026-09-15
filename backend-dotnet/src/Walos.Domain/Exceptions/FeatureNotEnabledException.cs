namespace Walos.Domain.Exceptions;

public sealed class FeatureNotEnabledException : BusinessException
{
    public string Feature { get; }

    public FeatureNotEnabledException(string feature)
        : base("El modulo no esta habilitado para este comercio", "feature_not_enabled")
    {
        Feature = feature;
    }
}
