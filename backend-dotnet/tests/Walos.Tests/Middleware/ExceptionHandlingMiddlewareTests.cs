using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.API.Middleware;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Feature_Not_Enabled_Is_Reported_As_403_With_Stable_Code_And_Feature()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new FeatureNotEnabledException("inventory"),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == Environments.Production));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("feature_not_enabled", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("inventory", json.RootElement.GetProperty("details").GetProperty("feature").GetString());
    }
}
