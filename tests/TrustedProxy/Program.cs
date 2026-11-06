using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using UniversityTimetable.API;

static IConfiguration Config(string? network) => new ConfigurationBuilder().AddInMemoryCollection(
    network is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["TrustedProxyNetworks:0"] = network }).Build();

var defaults = new ForwardedHeadersOptions();
TrustedProxyConfiguration.Configure(defaults, Config(null));
if (defaults.ForwardedHeaders != ForwardedHeaders.XForwardedProto || defaults.KnownNetworks.Count != 1)
    throw new Exception("Default trust must remain limited to framework loopback defaults and protocol only.");

var configured = new ForwardedHeadersOptions();
TrustedProxyConfiguration.Configure(configured, Config("10.2.0.0/16"));
if (!configured.KnownNetworks.Any(n => n.Prefix.ToString() == "10.2.0.0" && n.PrefixLength == 16))
    throw new Exception("Explicit trusted proxy network was not added.");

var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(configured));
foreach (var (ip, expected) in new[] { ("10.2.3.4", "https"), ("203.0.113.4", "http") })
{
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
    context.Request.Scheme = "http";
    context.Request.Headers["X-Forwarded-Proto"] = "https";
    await middleware.Invoke(context);
    if (context.Request.Scheme != expected) throw new Exception("Forwarded protocol trust check failed for " + ip);
}

foreach (var invalid in new[] { "0.0.0.0/0", "not-an-address", "10.0.0.1/33" })
{
    try { TrustedProxyConfiguration.Configure(new ForwardedHeadersOptions(), Config(invalid)); }
    catch (InvalidOperationException) { continue; }
    throw new Exception("An invalid or unrestricted proxy network was accepted.");
}
Console.WriteLine("Trusted proxy configuration checks passed.");
