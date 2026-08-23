using System.Net;

namespace Ajure.Api;

internal static class LocalSettingsAccess
{
    internal static IResult? Denied(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } address
            || !IPAddress.IsLoopback(address)
            || !IsLoopbackHost(context.Request.Host.Host)
            || !HasLoopbackOrigin(context.Request))
        {
            return ApiProblems.Forbidden(
                context,
                "local_access_required",
                "Model provider settings are available only from this computer.");
        }

        return null;
    }

    private static bool HasLoopbackOrigin(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var origins)
            || origins.Count == 0)
        {
            return true;
        }

        if (origins.Count != 1
            || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin)
            || origin.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return IsLoopbackHost(origin.DnsSafeHost);
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address)
            && IPAddress.IsLoopback(address));
}
