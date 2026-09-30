using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace Yakult.Inventory.Gateway.Data;

/// <summary>
/// Per-request access to the signed-in user's database. The environment and user
/// always come from the token, never from the request body.
/// </summary>
public sealed class DbSession
{
    private readonly IHttpContextAccessor _http;
    private readonly GatewayEnvironments _environments;
    private readonly SqlConnectionFactory _factory;

    public DbSession(IHttpContextAccessor http, GatewayEnvironments environments, SqlConnectionFactory factory)
    {
        _http = http;
        _environments = environments;
        _factory = factory;
    }

    public ClaimsPrincipal User => _http.HttpContext?.User ?? new ClaimsPrincipal();

    public int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public string UserName => User.Identity?.Name ?? "System";

    public GatewayEnvironment Environment =>
        _environments.Find(User.FindFirstValue(Program.EnvClaim))
        ?? throw new SessionEnvironmentGoneException();

    public Task<SqlConnection> OpenAsync(CancellationToken ct = default) => _factory.OpenAsync(Environment, ct);
}

/// <summary>The token names a database that was removed from the server config: sign in again.</summary>
public sealed class SessionEnvironmentGoneException : Exception
{
    public SessionEnvironmentGoneException()
        : base("That database was removed from the sign-in server. Please sign in again.") { }
}
