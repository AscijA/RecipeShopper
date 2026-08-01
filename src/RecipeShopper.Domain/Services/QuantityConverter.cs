using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Services;

public static class QuantityConverter
{
    private static readonly IReadOnlyDictionary<string, UnitDefinition> Definitions =
        new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["g"] = new(UnitCode.Gram, MeasurementFamily.Mass, 1m),
            ["kg"] = new(UnitCode.Kilogram, MeasurementFamily.Mass, 1000m),
            ["ml"] = new(UnitCode.Milliliter, MeasurementFamily.Volume, 1m),
            ["l"] = new(UnitCode.Liter, MeasurementFamily.Volume, 1000m),
            ["tsp"] = new(UnitCode.Teaspoon, MeasurementFamily.Volume, 5m),
            ["tbsp"] = new(UnitCode.Tablespoon, MeasurementFamily.Volume, 15m),
            ["cup"] = new(UnitCode.Cup, MeasurementFamily.Volume, 240m),
            ["kom"] = new(UnitCode.Piece, MeasurementFamily.Count, 1m),
            ["pakovanje"] = new(UnitCode.Package, MeasurementFamily.Container, 1m),
            ["kesica"] = new(UnitCode.Sachet, MeasurementFamily.Container, 1m),
            ["kutija"] = new(UnitCode.Box, MeasurementFamily.Container, 1m),
            ["boca"] = new(UnitCode.Bottle, MeasurementFamily.Container, 1m),
            ["tegla"] = new(UnitCode.Jar, MeasurementFamily.Container, 1m),
            ["konzerva"] = new(UnitCode.Can, MeasurementFamily.Container, 1m)
        };

    public static IReadOnlyCollection<UnitDefinition> KnownUnits => Definitions.Values.ToArray();

    public static bool TryGetDefinition(UnitCode unit, out UnitDefinition definition) =>
        Definitions.TryGetValue(unit.Value, out definition!);

    public static MeasurementFamily GetFamily(UnitCode unit) =>
        TryGetDefinition(unit, out var definition) ? definition.Family : MeasurementFamily.Container;

    public static bool CanConvert(UnitCode source, UnitCode target)
    {
        if (source == target) return true;
        return TryGetDefinition(source, out var sourceDefinition)
               && TryGetDefinition(target, out var targetDefinition)
               && sourceDefinition.Family != MeasurementFamily.Container
               && sourceDefinition.Family == targetDefinition.Family;
    }

    public static Quantity Convert(Quantity quantity, UnitCode targetUnit)
    {
        if (quantity.Unit == targetUnit) return quantity;
        if (!TryGetDefinition(quantity.Unit, out var source) ||
            !TryGetDefinition(targetUnit, out var target) ||
            source.Family == MeasurementFamily.Container ||
            source.Family != target.Family)
        {
            throw new InvalidOperationException($"Cannot convert {quantity.Unit} to {targetUnit}.");
        }

        return new Quantity(quantity.Amount * source.BaseUnitFactor / target.BaseUnitFactor, targetUnit);
    }

    public static Quantity ToCanonical(Quantity quantity)
    {
        if (!TryGetDefinition(quantity.Unit, out var definition) || definition.Family == MeasurementFamily.Container)
        {
            return quantity;
        }

        var canonicalUnit = definition.Family switch
        {
            MeasurementFamily.Mass => UnitCode.Gram,
            MeasurementFamily.Volume => UnitCode.Milliliter,
            MeasurementFamily.Count => UnitCode.Piece,
            _ => quantity.Unit
        };
        return Convert(quantity, canonicalUnit);
    }

    public static Quantity ToFriendlyDisplay(Quantity quantity)
    {
        var canonical = ToCanonical(quantity);
        if (canonical.Unit == UnitCode.Gram && canonical.Amount >= 1000m)
            return Convert(canonical, UnitCode.Kilogram);
        if (canonical.Unit == UnitCode.Milliliter && canonical.Amount >= 1000m)
            return Convert(canonical, UnitCode.Liter);
        return canonical;
    }

    public static Quantity Add(Quantity left, Quantity right)
    {
        var converted = Convert(right, left.Unit);
        return new Quantity(left.Amount + converted.Amount, left.Unit);
    }
}
