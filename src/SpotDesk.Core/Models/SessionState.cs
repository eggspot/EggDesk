namespace SpotDesk.Core.Models;

public record SessionState
{
    public Guid ConnectionId { get; init; }
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    public int LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int? FrameWidth { get; set; }
    public int? FrameHeight { get; set; }
    public string? Codec { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
}

public enum SessionStatus { Idle, Connecting, Connected, Disconnecting, Error, Reconnecting }
