using System.Globalization;

namespace Ajure.Specification;

/// <summary>Identifier families defined by DOCUMENT-SPEC 2.2 and TRD 6.2.</summary>
public enum SpecIdKind
{
    Goal,
    Persona,
    Journey,
    FunctionalRequirement,
    NonFunctionalRequirement,
    AcceptanceCriterion,
    TechnicalDecision,
    UxDecision,
    Risk
}

/// <summary>Stable identifier parsing and allocation. Identifiers are never reassigned for ordering.</summary>
public static class SpecIds
{
    private const int MinimumDigits = 3;

    private static readonly (SpecIdKind Kind, string Prefix)[] Prefixes =
    [
        (SpecIdKind.Goal, "GOAL"),
        (SpecIdKind.Persona, "P"),
        (SpecIdKind.Journey, "J"),
        (SpecIdKind.FunctionalRequirement, "FR"),
        (SpecIdKind.NonFunctionalRequirement, "NFR"),
        (SpecIdKind.AcceptanceCriterion, "AC"),
        (SpecIdKind.TechnicalDecision, "TD"),
        (SpecIdKind.UxDecision, "UX"),
        (SpecIdKind.Risk, "RISK")
    ];

    public static string Prefix(SpecIdKind kind)
    {
        foreach (var (candidate, prefix) in Prefixes)
        {
            if (candidate == kind)
            {
                return prefix;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown identifier kind.");
    }

    public static string Format(SpecIdKind kind, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix(kind)}-{number.ToString("D" + MinimumDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)}");
    }

    /// <summary>Parses the canonical form only. Padding other than <c>D3</c> is rejected.</summary>
    public static bool TryParse(string? value, out SpecIdKind kind, out int number)
    {
        kind = default;
        number = 0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.IndexOf('-', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        var prefix = value[..separator];
        var digits = value[(separator + 1)..];
        var matched = false;
        foreach (var (candidate, candidatePrefix) in Prefixes)
        {
            if (string.Equals(prefix, candidatePrefix, StringComparison.Ordinal))
            {
                kind = candidate;
                matched = true;
                break;
            }
        }

        if (!matched || digits.Length < MinimumDigits)
        {
            return false;
        }

        if (digits.Length > MinimumDigits && digits[0] == '0')
        {
            return false;
        }

        foreach (var character in digits)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1)
        {
            number = 0;
            return false;
        }

        return true;
    }

    public static bool IsValid(string? value) => TryParse(value, out _, out _);

    public static bool IsRequirementId(string? value) =>
        TryParse(value, out var kind, out _)
        && kind is SpecIdKind.FunctionalRequirement or SpecIdKind.NonFunctionalRequirement;

    /// <summary>Next free number for the kind: highest existing number plus one. Gaps are never reused.</summary>
    public static string Next(SpecIdKind kind, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(existingIds);
        return Format(kind, HighestNumber(kind, existingIds) + 1);
    }

    public static IReadOnlyList<string> Allocate(SpecIdKind kind, IEnumerable<string> existingIds, int count)
    {
        ArgumentNullException.ThrowIfNull(existingIds);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var next = HighestNumber(kind, existingIds) + 1;
        var allocated = new string[count];
        for (var index = 0; index < count; index++)
        {
            allocated[index] = Format(kind, next + index);
        }

        return allocated;
    }

    private static int HighestNumber(SpecIdKind kind, IEnumerable<string> existingIds)
    {
        var highest = 0;
        foreach (var id in existingIds)
        {
            if (TryParse(id, out var parsedKind, out var parsedNumber)
                && parsedKind == kind
                && parsedNumber > highest)
            {
                highest = parsedNumber;
            }
        }

        return highest;
    }
}
