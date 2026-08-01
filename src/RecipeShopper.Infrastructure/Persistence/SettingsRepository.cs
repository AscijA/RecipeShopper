using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class SettingsRepository(RecipeShopperDatabase database) : ISettingsRepository
{
    private const string SettingsKey = "app-settings";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) =>
        database.ReadAsync(connection =>
        {
            var row = connection.Find<AppSettingRow>(SettingsKey);
            return row is null
                ? new AppSettings()
                : JsonSerializer.Deserialize<AppSettings>(row.JsonValue, SerializerOptions) ?? new AppSettings();
        }, cancellationToken);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.CurrencyCode);
        return database.WriteAsync(connection => connection.InsertOrReplace(new AppSettingRow
        {
            Key = SettingsKey,
            JsonValue = JsonSerializer.Serialize(settings, SerializerOptions),
            UpdatedUtc = RowMapper.Date(DateTimeOffset.UtcNow)
        }), cancellationToken);
    }
}
