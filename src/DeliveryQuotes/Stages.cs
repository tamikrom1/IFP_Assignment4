using System.Collections.Immutable;

namespace DeliveryQuotes;

public static class Stages
{
    public static Selection SelectDeliverable(IEnumerable<DeliveryRequest> requests, ServiceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(limits);

        var eligible = ImmutableArray.CreateBuilder<DeliveryRequest>();
        var excluded = ImmutableArray.CreateBuilder<DeliveryRequest>();
        foreach (var r in requests)
        {
            if (r.DistanceKm <= limits.MaxDistanceKm && r.WeightKg <= limits.MaxWeightKg)
                eligible.Add(r);
            else
                excluded.Add(r);
        }

        return new Selection(eligible.ToImmutable(), excluded.ToImmutable());
    }

    public static ImmutableArray<PricedRequest> Price(
        IEnumerable<DeliveryRequest> requests, Func<DeliveryRequest, decimal> costRule)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(costRule);

        return requests.Select(r => new PricedRequest(r, costRule(r))).ToImmutableArray();
    }

    public static QuoteReport BuildReport(
        string policyName, IEnumerable<PricedRequest> priced, IEnumerable<DeliveryRequest> excluded)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        ArgumentNullException.ThrowIfNull(priced);
        ArgumentNullException.ThrowIfNull(excluded);

        var quotes = priced
            .Select(p => new DeliveryQuote(p.Request.Id, p.Cost))
            .OrderBy(q => q.Cost).ThenBy(q => q.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        return new QuoteReport(policyName, quotes, quotes.Sum(q => q.Cost),
            excluded.Select(r => r.Id).ToImmutableArray());
    }
}
