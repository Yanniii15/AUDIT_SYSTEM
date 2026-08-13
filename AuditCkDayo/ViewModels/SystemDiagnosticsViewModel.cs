namespace AuditCkDayo.ViewModels
{
    public enum DiagnosticsCheckStatus
    {
        Pass,
        Warn,
        Fail
    }

    public sealed class SystemDiagnosticsReport
    {
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
        public DiagnosticsCheckStatus OverallStatus { get; init; }
        public List<SystemDiagnosticsGroup> Groups { get; init; } = new();
    }

    public sealed class SystemDiagnosticsGroup
    {
        public required string Name { get; init; }
        public List<SystemDiagnosticsCheck> Checks { get; init; } = new();
    }

    public sealed class SystemDiagnosticsCheck
    {
        public required string Name { get; init; }
        public required DiagnosticsCheckStatus Status { get; init; }
        public required string Detail { get; init; }
    }
}
