namespace Backend.Config;

public class ProjectRetentionOptions
{
    public const string SectionName = "ProjectRetention";

    public int TrashWindowDays { get; set; } = 30;
}
