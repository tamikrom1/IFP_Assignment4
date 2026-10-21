namespace DeliveryQuotes;

public static class DeliveryWorkflow
{
    public static QuoteReport Run(IEnumerable<DeliveryRequest> requests, ServiceLimits limits, PricingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var selection = Stages.SelectDeliverable(requests, limits);
        var priced = Stages.Price(selection.Eligible, policy.CostRule);
        return Stages.BuildReport(policy.Name, priced, selection.Excluded);
    }
}
