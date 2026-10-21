using System.Collections.Immutable;
using System.Globalization;

namespace DeliveryQuotes;

public static class DeliveryParser
{
    private const NumberStyles NumberRules = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static bool TryParseLine(string? line, out DeliveryRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (line is null)
        {
            error = "Line is missing.";
            return false;
        }

        var fields = line.Split(',');
        if (fields.Length != 3)
        {
            error = $"Expected 3 comma-separated fields (id,distanceKm,weightKg) but found {fields.Length}.";
            return false;
        }

        var id = fields[0].Trim();
        if (id.Length == 0)
        {
            error = "Id must not be blank.";
            return false;
        }

        if (!TryParseNonNegative(fields[1], "Distance", out var distance, out error)) return false;
        if (!TryParseNonNegative(fields[2], "Weight", out var weight, out error)) return false;

        request = new DeliveryRequest(id, distance, weight);
        return true;
    }

    public static ParseResult ParseDataset(IEnumerable<string>? lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var requests = ImmutableArray.CreateBuilder<DeliveryRequest>();
        var errors = ImmutableArray.CreateBuilder<InputError>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryParseLine(line, out var request, out var error))
                errors.Add(new InputError(lineNumber, line, error!));
            else if (!seen.Add(request!.Id))
                errors.Add(new InputError(lineNumber, line, $"Duplicate id '{request.Id}'."));
            else
                requests.Add(request);
        }

        return new ParseResult(requests.ToImmutable(), errors.ToImmutable());
    }

    private static bool TryParseNonNegative(string text, string name, out decimal value, out string? error)
    {
        error = null;
        var trimmed = text.Trim();
        if (!decimal.TryParse(trimmed, NumberRules, CultureInfo.InvariantCulture, out value))
        {
            error = $"{name} '{trimmed}' is not a valid number.";
            return false;
        }

        if (value < 0)
        {
            error = $"{name} must not be negative.";
            return false;
        }

        return true;
    }
}
