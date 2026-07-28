using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using OcabBridge.Api.Configuration;

namespace OcabBridge.Api.Adapters;

// DelegatingHandler that attaches HTTP Basic credentials to every
// outbound request to the OpenCode runner. Username is fixed
// ("opencode"); password comes from OcabOptions.OpenCodePassword
// (env OCAB__OpenCodePassword or config Ocab:OpenCodePassword).
// If OpenCodePassword is null/empty the handler attaches no header,
// matching the upstream behaviour when OPENCODE_SERVER_PASSWORD is
// unset on the runner side.
public sealed class OpenCodeAuthHandler : DelegatingHandler
{
    private const string OpenCodeUsername = "opencode";
    private readonly IOptionsMonitor<OcabOptions> _options;

    public OpenCodeAuthHandler(IOptionsMonitor<OcabOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var password = _options.CurrentValue.OpenCodePassword;
        if (!string.IsNullOrEmpty(password))
        {
            var raw = $"{OpenCodeUsername}:{password}";
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", b64);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
