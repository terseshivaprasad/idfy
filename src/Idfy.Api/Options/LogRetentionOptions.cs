namespace Idfy.Api.Options;

public sealed class LogRetentionOptions
{
    public const string SectionName = "LogRetention";

    /// <summary>Rows older than this are deleted. Zero or negative disables retention.</summary>
    public int RetentionDays { get; set; } = 90;

    public int SweepIntervalHours { get; set; } = 24;

    /// <summary>Rows deleted per DELETE statement, to avoid long locks.</summary>
    public int BatchSize { get; set; } = 5000;
}
