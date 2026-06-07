using Balsm.Entity.Application.Queries;
using Balsm.Supervisor.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/server-info")]
public class ServerInfoController(IMediator mediator) : ControllerBase
{
    private static readonly DateTime _startTime = DateTime.UtcNow;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var version = typeof(ServerInfoController).Assembly
            .GetName().Version?.ToString() ?? "0.0.0";

        var uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds;

        var deploymentMode = Environment.GetEnvironmentVariable("DeploymentMode") ?? "Standalone";

        // Workspace name is empty pre-setup. This endpoint is exempt from the migration
        // gate, so the query may run before the schema exists — treat any failure as
        // "no workspace yet" rather than surfacing a 500 on an unauthenticated endpoint.
        var workspaceName = string.Empty;
        try
        {
            var ws = await mediator.Send(new GetWorkspaceQuery(), ct).ConfigureAwait(false);
            if (ws.IsSuccess && ws.Value is not null)
                workspaceName = ws.Value.Name;
        }
        catch
        {
            // no workspace / DB not ready — leave empty per contract
        }

        // Cert fingerprint enables client trust-pinning (FR-009). Empty until a
        // certificate has been provisioned (first Standalone boot).
        var certificateSha256 = CertificateService.TryGetFingerprint() ?? string.Empty;

        return Ok(new
        {
            version,
            mode = deploymentMode,
            workspace_name = workspaceName,
            certificate_sha256 = certificateSha256,
            uptime_seconds = uptimeSeconds
        });
    }
}
