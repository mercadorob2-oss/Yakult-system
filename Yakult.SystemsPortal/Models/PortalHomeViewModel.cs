namespace Yakult.SystemsPortal.Models;

public sealed class PortalHomeViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public IReadOnlyList<PortalSystemLink> Systems { get; init; } = Array.Empty<PortalSystemLink>();
    public IReadOnlyList<PortalMiniCard> MiniCards { get; init; } = Array.Empty<PortalMiniCard>();
    public PortalNoticeSettings Notice { get; init; } = new();
}
