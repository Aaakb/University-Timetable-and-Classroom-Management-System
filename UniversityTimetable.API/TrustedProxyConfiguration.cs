using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace UniversityTimetable.API;

public static class TrustedProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        foreach (var value in configuration.GetSection("TrustedProxyNetworks").Get<string[]>() ?? [])
        {
            var parts = value.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) ||
                !int.TryParse(parts[1], out var prefix) || prefix <= 0 ||
                prefix > (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
                throw new InvalidOperationException("TrustedProxyNetworks must contain explicit valid CIDR networks.");
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefix));
        }
    }
}
