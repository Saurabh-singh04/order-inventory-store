namespace Store.Infrastructure.Persistence.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    // Passwords come from configuration (appsettings.Development.json / env vars), never from code.
    public string AdminPassword { get; set; } = string.Empty;
    public string CustomerPassword { get; set; } = string.Empty;
}
