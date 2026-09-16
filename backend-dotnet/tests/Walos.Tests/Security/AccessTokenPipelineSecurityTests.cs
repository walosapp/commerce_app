using System.Runtime.CompilerServices;

namespace Walos.Tests.Security;

public sealed class AccessTokenPipelineSecurityTests
{
    [Fact]
    public void JwtPipeline_FailsClosed_WhenSecurityStateIsInvalidOrUnavailable()
    {
        var source = File.ReadAllText(GetApiPath("Program.cs"));
        var start = source.IndexOf("OnTokenValidated = async context =>", StringComparison.Ordinal);
        Assert.True(start >= 0, "JWT OnTokenValidated hook is required");
        var end = source.IndexOf("builder.Services.AddWalosAuthorization", start, StringComparison.Ordinal);
        var hook = source[start..end];

        Assert.Contains("IAccessTokenValidationService", hook, StringComparison.Ordinal);
        Assert.Contains("validator.ValidateAsync(context.Principal)", hook, StringComparison.Ordinal);
        Assert.Contains("context.Fail(\"Token invalidated\")", hook, StringComparison.Ordinal);
        Assert.Contains("context.Fail(\"Token validation unavailable\")", hook, StringComparison.Ordinal);
    }

    private static string GetApiPath(
        string fileName,
        [CallerFilePath] string testSourceFile = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("Could not resolve test source directory");
        var backendRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", ".."));
        return Path.Combine(backendRoot, "src", "Walos.API", fileName);
    }
}
