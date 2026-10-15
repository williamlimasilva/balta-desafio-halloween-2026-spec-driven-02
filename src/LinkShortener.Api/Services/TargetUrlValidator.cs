using System.Net;
using System.Net.Sockets;

namespace LinkShortener.Api.Services;

public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string hostName, CancellationToken cancellationToken = default);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string hostName, CancellationToken cancellationToken = default)
    {
        return Dns.GetHostAddressesAsync(hostName, cancellationToken);
    }
}

public sealed class TargetUrlValidator
{
    private const int MaximumUrlLength = 2048;

    private static readonly (IPAddress Network, int PrefixLength)[] ForbiddenIpv4Networks =
    {
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24),
        (IPAddress.Parse("192.0.2.0"), 24),
        (IPAddress.Parse("192.88.99.0"), 24),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15),
        (IPAddress.Parse("198.51.100.0"), 24),
        (IPAddress.Parse("203.0.113.0"), 24),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("240.0.0.0"), 4)
    };

    private readonly IDnsResolver _dnsResolver;

    public TargetUrlValidator(IDnsResolver dnsResolver)
    {
        _dnsResolver = dnsResolver;
    }

    public async Task<bool> IsValidAsync(string? targetUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUrl) || targetUrl.Length > MaximumUrlLength ||
            !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ||
            (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var hostName = uri.DnsSafeHost.TrimEnd('.');
        if (hostName.Length == 0 || IsLocalHostName(hostName))
        {
            return false;
        }

        if (IPAddress.TryParse(hostName, out var literalAddress))
        {
            return IsPublicAddress(literalAddress);
        }

        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await _dnsResolver.ResolveAsync(hostName, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return false;
        }

        return resolvedAddresses.Length > 0 && resolvedAddresses.All(IsPublicAddress);
    }

    private static bool IsLocalHostName(string hostName)
    {
        return hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               hostName.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
               hostName.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var addressBytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !ForbiddenIpv4Networks.Any(range => IsInCidr(addressBytes, range.Network.GetAddressBytes(), range.PrefixLength));
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            IPAddress.IPv6Any.Equals(address) || IPAddress.IPv6Loopback.Equals(address) || address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
            (addressBytes[0] & 0xE0) != 0x20)
        {
            return false;
        }

        return !IsInCidr(addressBytes, IPAddress.Parse("2001::").GetAddressBytes(), 23) &&
             !IsInCidr(addressBytes, IPAddress.Parse("2001:db8::").GetAddressBytes(), 32) &&
               !IsInCidr(addressBytes, IPAddress.Parse("2002::").GetAddressBytes(), 16) &&
               !IsInCidr(addressBytes, IPAddress.Parse("3fff::").GetAddressBytes(), 20);
    }

    private static bool IsInCidr(byte[] address, byte[] network, int prefixLength)
    {
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < fullBytes; index++)
        {
            if (address[index] != network[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (address[fullBytes] & mask) == (network[fullBytes] & mask);
    }
}