namespace Walos.Infrastructure.Storage;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "SupabaseStorage";
    public const string DefaultBucket = "walos-public-images";

    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = DefaultBucket;
}
