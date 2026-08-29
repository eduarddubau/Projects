namespace Backend.Config;

/// <summary>One trash window for everything the app soft-deletes.</summary>
public class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Pre-2026-08-29 name. Startup refuses it rather than ignoring it silently.</summary>
    public const string LegacySectionName = "ProjectRetention";

    public int TrashWindowDays { get; set; } = 30;
}
