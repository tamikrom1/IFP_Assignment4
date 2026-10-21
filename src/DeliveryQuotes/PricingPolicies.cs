namespace DeliveryQuotes;

public static class PricingPolicies
{
    public static readonly PricingPolicy Standard = Linear("Standard", fixedFee: 500m, ratePerKm: 100m);
    public static readonly PricingPolicy Express = Linear("Express", fixedFee: 800m, ratePerKm: 150m);

    public static PricingPolicy Linear(string name, decimal fixedFee, decimal ratePerKm)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Policy name must not be blank.", nameof(name));
        if (fixedFee < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedFee), "Fixed fee must not be negative.");
        if (ratePerKm < 0)
            throw new ArgumentOutOfRangeException(nameof(ratePerKm), "Rate per km must not be negative.");

        return new PricingPolicy(name.Trim(), request => fixedFee + ratePerKm * request.DistanceKm);
    }

    public static PricingPolicy? ByName(string name) => name.Trim().ToLowerInvariant() switch
    {
        "standard" => Standard,
        "express" => Express,
        _ => null
    };
}
