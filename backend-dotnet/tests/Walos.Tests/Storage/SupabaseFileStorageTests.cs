using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Walos.Application.Storage;
using Walos.Domain.Exceptions;
using Walos.Infrastructure.Storage;

namespace Walos.Tests.Storage;

public class SupabaseFileStorageTests
{
    private const string ProjectUrl = "https://project-ref.supabase.co";
    private const string ServiceRoleKey = "test-service-role-key";
    private const string Bucket = "walos-public-images";

    [Fact]
    public async Task UploadImageAsync_BuildsTenantScopedImmutableBrandingKey()
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.StartsWith(
                $"{ProjectUrl}/storage/v1/object/{Bucket}/companies/16/branding/logo-",
                request.RequestUri!.AbsoluteUri);
            Assert.EndsWith(".png", request.RequestUri.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(ServiceRoleKey, request.Headers.Authorization?.Parameter);
            Assert.Equal(ServiceRoleKey, request.Headers.GetValues("apikey").Single());
            Assert.Equal("false", request.Headers.GetValues("x-upsert").Single());
            Assert.Equal("31536000", request.Headers.GetValues("cache-control").Single());
            Assert.Equal("image/png", request.Content!.Headers.ContentType?.MediaType);
            Assert.Equal(new byte[] { 1, 2, 3 }, await request.Content.ReadAsByteArrayAsync());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var storage = CreateStorage(handler);

        await using var source = new MemoryStream([9]);
        var result = await storage.UploadImageAsync(new ImageUploadRequest(
            16,
            ImageStorageScope.Branding,
            null,
            source,
            "spoofed.exe",
            "application/octet-stream"));

        Assert.Matches(
            @"^companies/16/branding/logo-[a-f0-9]{32}\.png$",
            result.ObjectKey);
        Assert.Equal($"{ProjectUrl}/storage/v1/object/public/{Bucket}/{result.ObjectKey}", result.PublicUrl);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(3, result.Length);
        Assert.Equal(2, result.Width);
        Assert.Equal(3, result.Height);
    }

    [Fact]
    public async Task UploadImageAsync_RequiresProductIdForProductScope()
    {
        var storage = CreateStorage(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called")));
        await using var source = new MemoryStream([1]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            storage.UploadImageAsync(new ImageUploadRequest(
                16,
                ImageStorageScope.Product,
                null,
                source)));
    }

    [Fact]
    public async Task OpenReadAsync_AcceptsManagedPublicUrlAndReturnsContent()
    {
        const string objectKey = "companies/16/products/44/image-0123456789abcdef0123456789abcdef.jpg";
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"{ProjectUrl}/storage/v1/object/{Bucket}/{objectKey}",
                request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([4, 5, 6])
            });
        });
        var storage = CreateStorage(handler);

        await using var result = await storage.OpenReadAsync(storage.GetPublicUrl(objectKey));

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 4, 5, 6 }, ((MemoryStream)result).ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_RejectsExternalAndLegacyUrlsWithoutHttpCall()
    {
        var storage = CreateStorage(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called")));

        await Assert.ThrowsAsync<ValidationException>(() =>
            storage.OpenReadAsync("https://attacker.example/logo.png"));
        await Assert.ThrowsAsync<ValidationException>(() =>
            storage.OpenReadAsync("/uploads/branding/legacy.png"));
    }

    [Fact]
    public async Task DeleteIfManagedAsync_UsesSupabaseBulkDeleteEndpoint()
    {
        const string objectKey = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.webp";
        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal(
                $"{ProjectUrl}/storage/v1/object/{Bucket}",
                request.RequestUri!.AbsoluteUri);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(objectKey, json.RootElement.GetProperty("prefixes")[0].GetString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var storage = CreateStorage(handler);

        Assert.True(await storage.DeleteIfManagedAsync(objectKey));
        Assert.False(await storage.DeleteIfManagedAsync("/uploads/branding/legacy.png"));
    }

    [Fact]
    public void IsManagedReference_RejectsTraversalAndOtherBuckets()
    {
        var storage = CreateStorage(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        const string objectKey = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.png";

        Assert.True(storage.IsManagedReference(objectKey));
        Assert.True(storage.IsManagedReference(storage.GetPublicUrl(objectKey)));
        Assert.False(storage.IsManagedReference("companies/16/branding/../secret.png"));
        Assert.False(storage.IsManagedReference(
            $"{ProjectUrl}/storage/v1/object/public/another-bucket/{objectKey}"));
    }

    [Fact]
    public void TryGetManagedObjectKey_ResolvesOnlyConfiguredHostBucketAndCanonicalKey()
    {
        var storage = CreateStorage(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called")));
        const string objectKey = "companies/16/branding/logo-0123456789abcdef0123456789abcdef.webp";

        Assert.True(storage.TryGetManagedObjectKey(storage.GetPublicUrl(objectKey), out var resolved));
        Assert.Equal(objectKey, resolved);
        Assert.False(storage.TryGetManagedObjectKey(
            $"https://attacker.example/storage/v1/object/public/{Bucket}/{objectKey}", out _));
        Assert.False(storage.TryGetManagedObjectKey(
            $"{ProjectUrl}/storage/v1/object/public/another-bucket/{objectKey}", out _));
        Assert.False(storage.TryGetManagedObjectKey(
            $"{ProjectUrl}/storage/v1/object/public/{Bucket}/companies/16/branding/%2E%2E/secret.webp",
            out _));
    }

    private static SupabaseFileStorage CreateStorage(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            new StubImageValidator(),
            Options.Create(new SupabaseStorageOptions
            {
                Url = ProjectUrl,
                ServiceRoleKey = ServiceRoleKey,
                Bucket = Bucket
            }),
            NullLogger<SupabaseFileStorage>.Instance);

    private sealed class StubImageValidator : IImageValidator
    {
        public Task<ValidatedImage> ValidateAsync(
            Stream content,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ValidatedImage(
                new MemoryStream([1, 2, 3]),
                "image/png",
                ".png",
                3,
                2,
                3));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
