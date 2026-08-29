namespace Backend.Config;

/// <summary>One trash window for everything the app soft-deletes, and the cutoff every trash reads.</summary>
public sealed class TrashWindow
{
    public const string SectionName = "Retention";

    // No default: appsettings.json is where the number lives, and a missing key has to fail
    // loudly rather than fall back to one hidden here.
    [ConfigurationKeyName("TrashWindowDays")]
    public int Days { get; init; }

    /// <summary>Anything deleted before this has left the window.</summary>
    public DateTime Cutoff => DateTime.UtcNow.AddDays(-Days);
}
