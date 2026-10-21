using System.Collections.Immutable;

namespace DeliveryQuotes;

public sealed record DeliveryRequest(string Id, decimal DistanceKm, decimal WeightKg);

public sealed record ServiceLimits(decimal MaxDistanceKm, decimal MaxWeightKg)
{
    public static readonly ServiceLimits Default = new(50m, 30m);
}

public sealed record InputError(int LineNumber, string RawLine, string Message);

public sealed record ParseResult(ImmutableArray<DeliveryRequest> Requests, ImmutableArray<InputError> Errors);

public sealed record Selection(ImmutableArray<DeliveryRequest> Eligible, ImmutableArray<DeliveryRequest> Excluded);

public sealed record PricedRequest(DeliveryRequest Request, decimal Cost);

public sealed record DeliveryQuote(string Id, decimal Cost);

public sealed record QuoteReport(
    string PolicyName,
    ImmutableArray<DeliveryQuote> Quotes,
    decimal TotalCost,
    ImmutableArray<string> ExcludedIds);

public sealed record PricingPolicy(string Name, Func<DeliveryRequest, decimal> CostRule);
