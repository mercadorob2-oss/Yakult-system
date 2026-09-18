namespace Yakult.ITCM.Server.Models;

public sealed class ItcmUserIdentity
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsDeveloper { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
