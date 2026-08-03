namespace RecipeShopper.Infrastructure.Sync;

public sealed record SupabaseSyncOptions(string Url, string AnonKey)
{
    public bool IsConfigured =>
        Uri.TryCreate(Url, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(AnonKey);
}
