using System.Collections.Immutable;
using DeliveryQuotes;
using Xunit;

namespace DeliveryQuotes.Tests;

public class DeliveryTests
{
    private static readonly string[] Dataset =
    {
        "D1,2,3",    // valid: Standard 700,  Express 1100
        "D2,10,5",   // valid: Standard 1500, Express 2300
        "D3,50,30",  // valid, exactly on both limits (included): Standard 5500, Express 8300
        "D4,60,5",   // valid text, distance above 50 -> excluded by stage 1
        "D5,0,1",    // zero distance is legitimate: Standard 500, Express 800
        "D6,-4,2",   // rejected: negative distance
        "D7,abc,2",  // rejected: malformed number
        "D8,20,45",  // valid text, weight above 30 -> excluded by stage 1
    };

    private static DeliveryRequest R(string id, decimal km, decimal kg) => new(id, km, kg);

    private static ImmutableArray<DeliveryRequest> ParsedDataset() => DeliveryParser.ParseDataset(Dataset).Requests;

    private static string[] Describe(QuoteReport r) => r.Quotes.Select(q => $"{q.Id}:{q.Cost}").ToArray();

    [Fact]
    public void NormalWorkflow_Standard_ProducesDocumentedQuotes()
    {
        var parsed = DeliveryParser.ParseDataset(Dataset);
        Assert.Equal(6, parsed.Requests.Length);
        Assert.Equal(new[] { 6, 7 }, parsed.Errors.Select(e => e.LineNumber).ToArray());

        var report = DeliveryWorkflow.Run(parsed.Requests, ServiceLimits.Default, PricingPolicies.Standard);

        Assert.Equal("Standard", report.PolicyName);
        Assert.Equal(new[] { "D5:500", "D1:700", "D2:1500", "D3:5500" }, Describe(report));
        Assert.Equal(8200m, report.TotalCost);
        Assert.Equal(new[] { "D4", "D8" }, report.ExcludedIds.ToArray());
    }

    [Fact]
    public void NormalWorkflow_Express_ProducesDocumentedQuotes()
    {
        var report = DeliveryWorkflow.Run(ParsedDataset(), ServiceLimits.Default, PricingPolicies.Express);

        Assert.Equal(new[] { "D5:800", "D1:1100", "D2:2300", "D3:8300" }, Describe(report));
        Assert.Equal(12500m, report.TotalCost);
    }

    [Fact]
    public void TwoPolicies_SameInput_DifferentResults_SamePipeline()
    {
        var input = new[] { R("D1", 2, 3) };

        var standard = DeliveryWorkflow.Run(input, ServiceLimits.Default, PricingPolicies.Standard);
        var express = DeliveryWorkflow.Run(input, ServiceLimits.Default, PricingPolicies.Express);

        Assert.Equal(700m, standard.Quotes[0].Cost);
        Assert.Equal(1100m, express.Quotes[0].Cost);
        Assert.True(standard.TotalCost != express.TotalCost);
        Assert.Equal(standard.Quotes[0].Id, express.Quotes[0].Id);
    }

    [Fact]
    public void EmptyDataset_GivesEmptyReport_WithoutCrashing()
    {
        var parsed = DeliveryParser.ParseDataset(Array.Empty<string>());
        Assert.Empty(parsed.Requests);
        Assert.Empty(parsed.Errors);

        var report = DeliveryWorkflow.Run(parsed.Requests, ServiceLimits.Default, PricingPolicies.Standard);
        Assert.Empty(report.Quotes);
        Assert.Equal(0m, report.TotalCost);
        Assert.Empty(report.ExcludedIds);

        Assert.Empty(DeliveryParser.ParseDataset(new[] { "", "   " }).Requests);
    }

    [Fact]
    public void Boundary_ExactLimitsAreIncluded_JustAboveAreExcluded()
    {
        var limits = ServiceLimits.Default;
        var selection = Stages.SelectDeliverable(new[]
        {
            R("onLimit", 50m, 30m),
            R("overDistance", 50.01m, 1m),
            R("overWeight", 1m, 30.01m),
        }, limits);

        Assert.Equal(new[] { "onLimit" }, selection.Eligible.Select(r => r.Id).ToArray());
        Assert.Equal(new[] { "overDistance", "overWeight" }, selection.Excluded.Select(r => r.Id).ToArray());

        var report = DeliveryWorkflow.Run(new[] { R("onLimit", 50m, 30m) }, limits, PricingPolicies.Standard);
        Assert.Equal(5500m, report.Quotes[0].Cost);
    }

    [Fact]
    public void Boundary_ZeroDistance_IsValidAndCostsOnlyTheFixedFee()
    {
        Assert.True(DeliveryParser.TryParseLine("Z,0,0", out var request, out var error));
        Assert.Null(error);
        Assert.Equal(0m, request!.DistanceKm);

        Assert.Equal(500m, PricingPolicies.Standard.CostRule(request));
        Assert.Equal(800m, PricingPolicies.Express.CostRule(request));
    }

    [Theory]
    [InlineData("D1,abc,2")]       // malformed distance
    [InlineData("D1,2,xyz")]       // malformed weight
    [InlineData("D1,-1,2")]        // negative distance
    [InlineData("D1,2,-0.5")]      // negative weight
    [InlineData("D1,1,5,5")]       // comma decimal splits into 4 fields
    [InlineData("D1,2")]           // too few fields
    [InlineData(",2,2")]           // blank id
    [InlineData("  ,2,2")]         // whitespace id
    [InlineData("D1,NaN,2")]       // NaN is not a decimal
    [InlineData("D1,1e3,2")]       // exponent not allowed
    public void InvalidLine_IsRejected_WithMessage_AndNoRequest(string line)
    {
        var ok = DeliveryParser.TryParseLine(line, out var request, out var error);

        Assert.False(ok);
        Assert.Null(request);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void MissingInput_IsRejected_NotTurnedIntoAResult()
    {
        Assert.False(DeliveryParser.TryParseLine(null, out var request, out var error));
        Assert.Null(request);
        Assert.NotNull(error);

        Assert.Throws<ArgumentNullException>(() => DeliveryParser.ParseDataset(null));
        Assert.Throws<ArgumentNullException>(() => Stages.SelectDeliverable(null!, ServiceLimits.Default));
        Assert.Throws<ArgumentNullException>(() => Stages.Price(new[] { R("A", 1, 1) }, null!));
        Assert.Throws<ArgumentNullException>(() => DeliveryWorkflow.Run(new[] { R("A", 1, 1) }, ServiceLimits.Default, null!));
    }

    [Fact]
    public void Dataset_RejectsDuplicates_KeepsLineNumbers_IgnoresBlankLines()
    {
        var parsed = DeliveryParser.ParseDataset(new[] { "", "A,1,1", "   ", "a,2,2", "B,3,3" });

        Assert.Equal(new[] { "A", "B" }, parsed.Requests.Select(r => r.Id).ToArray());
        var error = Assert.Single(parsed.Errors);
        Assert.Equal(4, error.LineNumber);
        Assert.Equal("a,2,2", error.RawLine);
        Assert.Contains("Duplicate", error.Message);
    }

    [Fact]
    public void Stage1_Alone_SplitsInput_AndDoesNotChangeIt()
    {
        var input = new[] { R("A", 10, 1), R("B", 99, 1), R("C", 20, 2) };
        var snapshot = input.ToArray();

        var selection = Stages.SelectDeliverable(input, ServiceLimits.Default);

        Assert.Equal(new[] { "A", "C" }, selection.Eligible.Select(r => r.Id).ToArray());
        Assert.Equal(new[] { "B" }, selection.Excluded.Select(r => r.Id).ToArray());
        Assert.True(input.SequenceEqual(snapshot));
        Assert.Equal(3, input.Length);
    }

    [Fact]
    public void Stage2_Alone_CallsTheDelegateOncePerRequest()
    {
        var calls = new List<string>();
        var input = new[] { R("A", 1, 1), R("B", 2, 1) };

        var priced = Stages.Price(input, r => { calls.Add(r.Id); return r.DistanceKm * 10; });

        Assert.Equal(new[] { "A", "B" }, calls.ToArray());
        Assert.Equal(new[] { 10m, 20m }, priced.Select(p => p.Cost).ToArray());
    }

    [Fact]
    public void Stage3_Alone_OrdersByCostThenId_AndSumsTheTotal()
    {
        var priced = new[]
        {
            new PricedRequest(R("B", 1, 1), 700m),
            new PricedRequest(R("A", 1, 1), 700m),
            new PricedRequest(R("C", 1, 1), 500m),
        };

        var report = Stages.BuildReport("Test", priced, new[] { R("X", 99, 1) });

        Assert.Equal(new[] { "C:500", "A:700", "B:700" }, Describe(report));
        Assert.Equal(1900m, report.TotalCost);
        Assert.Equal(new[] { "X" }, report.ExcludedIds.ToArray());
    }

    [Fact]
    public void Workflow_IsPure_SameInputGivesSameOutput_AndInputIsUntouched()
    {
        var input = ParsedDataset();
        var copy = input.ToArray();

        var first = DeliveryWorkflow.Run(input, ServiceLimits.Default, PricingPolicies.Express);
        var second = DeliveryWorkflow.Run(input, ServiceLimits.Default, PricingPolicies.Express);

        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal(first.TotalCost, second.TotalCost);
        Assert.True(input.SequenceEqual(copy));
        Assert.Equal(6, input.Length);
    }

    [Fact]
    public void Policy_CapturesItsConfiguration_LaterChangesDoNotAffectIt()
    {
        var fixedFee = 500m;
        var rate = 100m;
        var policy = PricingPolicies.Linear("Custom", fixedFee, rate);

        fixedFee = 9999m;
        rate = 1m;

        Assert.Equal(700m, policy.CostRule(R("D1", 2, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingPolicies.Linear("Bad", -1m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingPolicies.Linear("Bad", 500m, -1m));
        Assert.Null(PricingPolicies.ByName("unknown"));
        Assert.Same(PricingPolicies.Express, PricingPolicies.ByName(" EXPRESS "));
    }

    [Fact]
    public void ChangingTheLimits_ChangesTheSelection_WithTheSamePipeline()
    {
        var input = new[] { R("D4", 60, 5) };

        Assert.Empty(DeliveryWorkflow.Run(input, ServiceLimits.Default, PricingPolicies.Standard).Quotes);

        var wider = DeliveryWorkflow.Run(input, new ServiceLimits(60m, 30m), PricingPolicies.Standard);
        Assert.Equal(6500m, wider.Quotes[0].Cost);
    }
}
