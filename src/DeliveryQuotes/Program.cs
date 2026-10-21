using DeliveryQuotes;

var path = args.Length > 0 ? args[0] : Path.Combine("data", "deliveries.csv");
var policyArg = args.Length > 1 ? args[1] : "both";

var policies = policyArg.Trim().ToLowerInvariant() == "both"
    ? new[] { PricingPolicies.Standard, PricingPolicies.Express }
    : new[] { PricingPolicies.ByName(policyArg) }.OfType<PricingPolicy>().ToArray();

if (policies.Length == 0)
{
    Console.Error.WriteLine($"Unknown policy '{policyArg}'. Use standard, express or both.");
    return 2;
}

string[] lines;
try
{
    lines = File.ReadAllLines(path);
}
catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or IOException)
{
    Console.Error.WriteLine($"Cannot read input file '{path}' (working directory: {Directory.GetCurrentDirectory()}).");
    Console.Error.WriteLine(e.Message);
    return 1;
}

var parsed = DeliveryParser.ParseDataset(lines);
Console.WriteLine($"Input: {path}");
Console.WriteLine($"Valid requests: {parsed.Requests.Length}, rejected lines: {parsed.Errors.Length}");
foreach (var e in parsed.Errors)
    Console.WriteLine($"  line {e.LineNumber}: {e.Message}  [{e.RawLine}]");

foreach (var policy in policies)
{
    var report = DeliveryWorkflow.Run(parsed.Requests, ServiceLimits.Default, policy);
    Console.WriteLine();
    Console.WriteLine($"=== {report.PolicyName} policy ===");
    if (report.Quotes.IsEmpty)
        Console.WriteLine("  (no quotes)");
    foreach (var q in report.Quotes)
        Console.WriteLine($"  {q.Id,-4} {q.Cost,8}");
    Console.WriteLine($"  Total: {report.TotalCost}");
    Console.WriteLine($"  Excluded (outside service limits): {(report.ExcludedIds.IsEmpty ? "none" : string.Join(", ", report.ExcludedIds))}");
}

return 0;
