namespace RecipeShopper.Domain.ValueObjects;

public readonly record struct Quantity
{
    public Quantity(decimal amount, UnitCode unit)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Quantity must be greater than zero.");
        }

        Amount = amount;
        Unit = unit;
    }

    public decimal Amount { get; }
    public UnitCode Unit { get; }

    public override string ToString() => $"{Amount} {Unit}";
}
