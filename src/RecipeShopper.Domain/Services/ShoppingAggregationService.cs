using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Services;

public sealed record ShoppingContributionInput(
    Guid SourceId,
    string SourceName,
    Guid IngredientId,
    string IngredientName,
    Quantity? Quantity,
    string? Note = null);

public sealed record ShoppingContributionSource(
    Guid SourceId,
    string SourceName,
    Quantity? Quantity,
    string? Note);

public sealed record AggregatedShoppingItem(
    Guid IngredientId,
    string IngredientName,
    string QuantityBucket,
    Quantity? TotalQuantity,
    bool RequiresAmount,
    IReadOnlyList<ShoppingContributionSource> Sources);

public static class ShoppingAggregationService
{
    public static IReadOnlyList<AggregatedShoppingItem> Aggregate(IEnumerable<ShoppingContributionInput> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var result = new List<AggregatedShoppingItem>();

        foreach (var ingredientGroup in contributions.GroupBy(item => item.IngredientId))
        {
            var ingredientName = ingredientGroup.Select(item => item.IngredientName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
            var knownGroups = ingredientGroup.Where(item => item.Quantity.HasValue)
                .GroupBy(item => GetBucket(item.Quantity!.Value))
                .ToList();
            var missing = ingredientGroup.Where(item => !item.Quantity.HasValue).ToList();

            if (knownGroups.Count == 0)
            {
                result.Add(new AggregatedShoppingItem(
                    ingredientGroup.Key,
                    ingredientName,
                    "unspecified",
                    null,
                    true,
                    missing.Select(ToSource).ToList()));
                continue;
            }

            for (var index = 0; index < knownGroups.Count; index++)
            {
                var group = knownGroups[index];
                var canonical = group.Select(item => QuantityConverter.ToCanonical(item.Quantity!.Value)).ToList();
                var total = canonical.Skip(1).Aggregate(canonical[0], QuantityConverter.Add);
                var sources = group.Select(ToSource).ToList();
                if (index == 0) sources.AddRange(missing.Select(ToSource));

                result.Add(new AggregatedShoppingItem(
                    ingredientGroup.Key,
                    ingredientName,
                    group.Key,
                    QuantityConverter.ToFriendlyDisplay(total),
                    index == 0 && missing.Count > 0,
                    sources));
            }
        }

        return result
            .OrderBy(item => item.IngredientName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.QuantityBucket, StringComparer.Ordinal)
            .ToList();
    }

    private static string GetBucket(Quantity quantity)
    {
        if (!QuantityConverter.TryGetDefinition(quantity.Unit, out var definition) ||
            definition.Family == MeasurementFamily.Container)
        {
            return $"unit:{quantity.Unit.Value}";
        }

        return $"family:{definition.Family}";
    }

    private static ShoppingContributionSource ToSource(ShoppingContributionInput item) =>
        new(item.SourceId, item.SourceName, item.Quantity, item.Note);
}
