using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Domain.ValueObjects;

public readonly record struct UnitCode
{
    public UnitCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator UnitCode(string value) => new(value);
    public static implicit operator string(UnitCode unit) => unit.Value;

    public static UnitCode Gram => new("g");
    public static UnitCode Kilogram => new("kg");
    public static UnitCode Milliliter => new("ml");
    public static UnitCode Liter => new("l");
    public static UnitCode Teaspoon => new("tsp");
    public static UnitCode Tablespoon => new("tbsp");
    public static UnitCode Cup => new("cup");
    public static UnitCode Piece => new("kom");
    public static UnitCode Package => new("pakovanje");
    public static UnitCode Sachet => new("kesica");
    public static UnitCode Box => new("kutija");
    public static UnitCode Bottle => new("boca");
    public static UnitCode Jar => new("tegla");
    public static UnitCode Can => new("konzerva");
}

public sealed record UnitDefinition(UnitCode Code, MeasurementFamily Family, decimal BaseUnitFactor);
