using System.Globalization;

namespace RecipeShopper.Domain.ValueObjects;

public readonly record struct Money
{
    public Money(long minorUnits, string currency)
    {
        if (minorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnits));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException("Currency must be a three-letter ISO code.", nameof(currency));
        }

        MinorUnits = minorUnits;
        Currency = normalized;
    }

    public long MinorUnits { get; }
    public string Currency { get; }
    public decimal MajorUnits => MinorUnits / 100m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(checked(MinorUnits + other.MinorUnits), Currency);
    }

    public Money Multiply(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new Money(checked(MinorUnits * count), Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Money values have different currencies.");
        }
    }

    public override string ToString() => $"{MajorUnits.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
}
