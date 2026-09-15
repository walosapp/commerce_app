using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Moq;
using Walos.API.Authorization;

namespace Walos.Tests.Security;

public class WalosAuthorizationResultHandlerTests
{
    private static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    [Fact]
    public async Task Forbidden_Returns_Permission_Denied_Json_Without_Running_Feature_Middleware()
    {
        var context = Context(authenticated: true);
        var next = new Mock<RequestDelegate>();

        await new WalosAuthorizationResultHandler().HandleAsync(
            next.Object, context, Policy, PolicyAuthorizationResult.Forbid());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.False(next.Invocations.Any());
        Assert.Equal("permission_denied", await ReadCodeAsync(context));
    }

    [Fact]
    public async Task Challenge_Returns_Authentication_Required_Json()
    {
        var context = Context(authenticated: false);
        var next = new Mock<RequestDelegate>();

        await new WalosAuthorizationResultHandler().HandleAsync(
            next.Object, context, Policy, PolicyAuthorizationResult.Challenge());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("authentication_required", await ReadCodeAsync(context));
        Assert.False(next.Invocations.Any());
    }

    [Fact]
    public async Task Successful_Authorization_Continues_Pipeline()
    {
        var context = Context(authenticated: true);
        var next = new Mock<RequestDelegate>();
        next.Setup(n => n(context)).Returns(Task.CompletedTask);

        await new WalosAuthorizationResultHandler().HandleAsync(
            next.Object, context, Policy, PolicyAuthorizationResult.Success());

        next.Verify(n => n(context), Times.Once);
    }

    private static DefaultHttpContext Context(bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "Bearer"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }

    private static async Task<string?> ReadCodeAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("code").GetString();
    }
}
