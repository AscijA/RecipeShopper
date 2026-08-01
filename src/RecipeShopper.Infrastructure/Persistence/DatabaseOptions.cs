namespace RecipeShopper.Infrastructure.Persistence;

public sealed record DatabaseOptions(string DatabasePath)
{
    public static DatabaseOptions InDirectory(string directory) =>
        new(Path.Combine(directory, "recipe-shopper.db3"));
}
