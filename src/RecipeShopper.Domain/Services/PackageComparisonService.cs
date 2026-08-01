using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Services;

public sealed record PackageOfferCandidate(
    Guid PackageDefinitionId,
    Guid StoreId,
    Quantity NetQuantity,
    Money Price,
    bool IsAvailable = true);

public sealed record RecommendedPackage(Guid PackageDefinitionId, int Count);

public sealed record StorePackageRecommendation(
    Guid StoreId,
    IReadOnlyList<RecommendedPackage> Packages,
    Quantity CoveredQuantity,
    Money TotalPrice,
    decimal ExcessInRequiredUnit,
    Money? SavingsComparedWithSmallestPackages);

public sealed record PackageComparisonResult(
    StorePackageRecommendation? Best,
    IReadOnlyList<StorePackageRecommendation> ByStore,
    Money? SavingsComparedWithNextStore);

public static class PackageComparisonService
{
    private const int MaximumStates = 100_000;

    public static PackageComparisonResult Compare(
        Quantity required,
        IEnumerable<PackageOfferCandidate> candidates,
        string currency)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var normalizedCurrency = new Money(0, currency).Currency;
        var recommendations = candidates
            .Where(candidate => candidate.IsAvailable && candidate.Price.Currency == normalizedCurrency)
            .GroupBy(candidate => candidate.StoreId)
            .Select(group => FindBestForStore(required, group.ToList(), normalizedCurrency))
            .Where(result => result is not null)
            .Cast<StorePackageRecommendation>()
            .OrderBy(result => result.TotalPrice.MinorUnits)
            .ThenBy(result => result.ExcessInRequiredUnit)
            .ToList();

        var best = recommendations.FirstOrDefault();
        Money? storeSavings = null;
        if (recommendations.Count > 1 && recommendations[1].TotalPrice.MinorUnits > recommendations[0].TotalPrice.MinorUnits)
        {
            storeSavings = new Money(
                recommendations[1].TotalPrice.MinorUnits - recommendations[0].TotalPrice.MinorUnits,
                normalizedCurrency);
        }

        return new PackageComparisonResult(best, recommendations, storeSavings);
    }

    private static StorePackageRecommendation? FindBestForStore(
        Quantity required,
        IReadOnlyList<PackageOfferCandidate> storeCandidates,
        string currency)
    {
        var options = storeCandidates
            .Where(candidate => QuantityConverter.CanConvert(candidate.NetQuantity.Unit, required.Unit))
            .Select(candidate => new Option(
                candidate.PackageDefinitionId,
                QuantityConverter.Convert(candidate.NetQuantity, required.Unit).Amount,
                candidate.Price.MinorUnits))
            .Where(option => option.Size > 0)
            .GroupBy(option => option.PackageId)
            .Select(group => group.OrderBy(option => option.Price).First())
            .ToList();
        if (options.Count == 0) return null;

        var upperBound = required.Amount + options.Max(option => option.Size);
        var states = new Dictionary<decimal, State> { [0m] = new(0, 0, new int[options.Count]) };
        var pending = new SortedSet<decimal> { 0m };

        while (pending.Count > 0)
        {
            var amount = pending.Min;
            pending.Remove(amount);
            var state = states[amount];
            if (amount >= required.Amount) continue;

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                var nextAmount = amount + option.Size;
                if (nextAmount > upperBound) continue;

                var nextCost = checked(state.Cost + option.Price);
                var nextCount = state.PackageCount + 1;
                if (states.TryGetValue(nextAmount, out var existing) &&
                    (existing.Cost < nextCost || existing.Cost == nextCost && existing.PackageCount <= nextCount))
                {
                    continue;
                }

                var counts = (int[])state.Counts.Clone();
                counts[index]++;
                states[nextAmount] = new State(nextCost, nextCount, counts);
                pending.Add(nextAmount);
                if (states.Count > MaximumStates)
                {
                    throw new InvalidOperationException("Package comparison produced too many combinations.");
                }
            }
        }

        var winner = states
            .Where(pair => pair.Key >= required.Amount)
            .OrderBy(pair => pair.Value.Cost)
            .ThenBy(pair => pair.Key - required.Amount)
            .ThenBy(pair => pair.Value.PackageCount)
            .FirstOrDefault();
        if (winner.Value is null) return null;

        var packages = winner.Value.Counts
            .Select((count, index) => new { count, index })
            .Where(entry => entry.count > 0)
            .Select(entry => new RecommendedPackage(options[entry.index].PackageId, entry.count))
            .ToList();
        var smallest = options.OrderBy(option => option.Size).ThenBy(option => option.Price).First();
        var smallestCount = checked((int)Math.Ceiling(required.Amount / smallest.Size));
        var smallestCost = checked(smallest.Price * smallestCount);
        Money? savings = smallestCost > winner.Value.Cost
            ? new Money(smallestCost - winner.Value.Cost, currency)
            : null;

        return new StorePackageRecommendation(
            storeCandidates[0].StoreId,
            packages,
            new Quantity(winner.Key, required.Unit),
            new Money(winner.Value.Cost, currency),
            winner.Key - required.Amount,
            savings);
    }

    private sealed record Option(Guid PackageId, decimal Size, long Price);
    private sealed record State(long Cost, int PackageCount, int[] Counts);
}
