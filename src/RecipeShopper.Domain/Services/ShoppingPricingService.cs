using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Services;

public sealed record PricedPackageSelection(
    Guid PackageDefinitionId,
    Guid StoreId,
    int Count,
    Quantity NetQuantity,
    Money? PricePerPackage);

public sealed record ShoppingPricingItem(
    Guid ItemId,
    bool IsChecked,
    IReadOnlyList<Quantity?> RequiredQuantities,
    IReadOnlyList<PricedPackageSelection> Selections);

public sealed record ShoppingTotalSummary(
    Money KnownSubtotal,
    PriceCoverageStatus Status,
    int MissingItemCount)
{
    public bool IsComplete => Status == PriceCoverageStatus.Complete;
}

public static class ShoppingPricingService
{
    public static ShoppingTotalSummary Calculate(IEnumerable<ShoppingPricingItem> items, string activeCurrency)
    {
        ArgumentNullException.ThrowIfNull(items);
        var currency = new Money(0, activeCurrency).Currency;
        long subtotal = 0;
        var missing = 0;

        foreach (var item in items.Where(candidate => !candidate.IsChecked))
        {
            var itemIncomplete = item.RequiredQuantities.Count == 0 || item.RequiredQuantities.Any(quantity => !quantity.HasValue);
            var selections = item.Selections.Where(selection => selection.Count > 0).ToList();
            var stores = selections.Select(selection => selection.StoreId).Distinct().Count();
            if (stores > 1) itemIncomplete = true;

            foreach (var selection in selections)
            {
                if (selection.PricePerPackage is { } price && price.Currency == currency)
                {
                    subtotal = checked(subtotal + checked(price.MinorUnits * selection.Count));
                }
                else
                {
                    itemIncomplete = true;
                }
            }

            foreach (var required in item.RequiredQuantities.OfType<Quantity>())
            {
                decimal covered = 0m;
                foreach (var selection in selections.Where(selection =>
                             QuantityConverter.CanConvert(selection.NetQuantity.Unit, required.Unit)))
                {
                    covered += QuantityConverter.Convert(selection.NetQuantity, required.Unit).Amount * selection.Count;
                }

                if (covered < required.Amount) itemIncomplete = true;
            }

            if (selections.Count == 0) itemIncomplete = true;
            if (itemIncomplete) missing++;
        }

        return new ShoppingTotalSummary(
            new Money(subtotal, currency),
            missing == 0 ? PriceCoverageStatus.Complete : PriceCoverageStatus.Incomplete,
            missing);
    }
}
