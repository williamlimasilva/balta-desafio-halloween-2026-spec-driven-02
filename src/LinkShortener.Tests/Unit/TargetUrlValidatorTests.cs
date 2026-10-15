using System.Net;
using System.Net.Sockets;
using LinkShortener.Api.Services;
using Xunit;

namespace LinkShortener.Tests.Unit;

public class TargetUrlValidatorTests
{
    [Theory]
    [InlineData("http://example.com/path")]
    [InlineData("https://example.com/path")]
    public async Task IsValidAsync_ReturnsTrueForPublicHttpAndHttpsUrls(string targetUrl)
    {
        var validator = CreateValidator("93.184.216.34");

        var isValid = await validator.IsValidAsync(targetUrl);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://localhost")]
    [InlineData("https://service.localhost/path")]
    [InlineData("http://printer.local/path")]
    public async Task IsValidAsync_ReturnsFalseForInvalidOrLocalUrls(string? targetUrl)
    {
        var validator = CreateValidator("93.184.216.34");

        var isValid = await validator.IsValidAsync(targetUrl);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://100.64.0.1")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://169.254.10.20")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://192.0.2.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://198.18.0.1")]
    [InlineData("http://203.0.113.1")]
    [InlineData("http://224.0.0.1")]
    [InlineData("http://[::]")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[fc00::1]")]
    [InlineData("http://[2001:db8::1]")]
    [InlineData("http://[::ffff:127.0.0.1]")]
    public async Task IsValidAsync_ReturnsFalseForForbiddenIpLiterals(string targetUrl)
    {
        var validator = CreateValidator("93.184.216.34");

        var isValid = await validator.IsValidAsync(targetUrl);

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidAsync_ReturnsFalseWhenDnsResolvesToForbiddenAddress()
    {
        var validator = CreateValidator("10.0.0.1");

        var isValid = await validator.IsValidAsync("https://internal.example.com");

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidAsync_ReturnsFalseWhenAnyDnsAnswerIsForbidden()
    {
        var validator = CreateValidator("93.184.216.34", "127.0.0.1");

        var isValid = await validator.IsValidAsync("https://mixed.example.com");

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidAsync_ReturnsFalseWhenDnsReturnsNoAddresses()
    {
        var validator = new TargetUrlValidator(new StubDnsResolver(Array.Empty<IPAddress>()));

        var isValid = await validator.IsValidAsync("https://unresolved.example.com");

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidAsync_ReturnsFalseWhenDnsResolutionFails()
    {
        var validator = new TargetUrlValidator(new StubDnsResolver(new SocketException((int)SocketError.HostNotFound)));

        var isValid = await validator.IsValidAsync("https://unresolved.example.com");

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidAsync_AcceptsUrlAt2048Characters()
    {
        const string prefix = "https://example.com/";
        var targetUrl = prefix + new string('a', 2048 - prefix.Length);
        var validator = CreateValidator("93.184.216.34");

        var isValid = await validator.IsValidAsync(targetUrl);

        Assert.Equal(2048, targetUrl.Length);
        Assert.True(isValid);
    }

    [Fact]
    public async Task IsValidAsync_RejectsUrlLongerThan2048Characters()
    {
        var targetUrl = "https://example.com/" + new string('a', 2048);
        var validator = CreateValidator("93.184.216.34");

        var isValid = await validator.IsValidAsync(targetUrl);

        Assert.False(isValid);
    }

    private static TargetUrlValidator CreateValidator(params string[] addresses)
    {
        return new TargetUrlValidator(new StubDnsResolver(addresses.Select(IPAddress.Parse).ToArray()));
    }

    private sealed class StubDnsResolver : IDnsResolver
    {
        private readonly IPAddress[]? _addresses;
        private readonly Exception? _exception;

        public StubDnsResolver(params IPAddress[] addresses)
        {
            _addresses = addresses;
        }

        public StubDnsResolver(Exception exception)
        {
            _exception = exception;
        }

        public Task<IPAddress[]> ResolveAsync(string hostName, CancellationToken cancellationToken = default)
        {
            return _exception is not null
                ? Task.FromException<IPAddress[]>(_exception)
                : Task.FromResult(_addresses!);
        }
    }
}