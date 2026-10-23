namespace SkailUsdQuote.Contracts;

/// <summary>Currencies offered against the US dollar (USD). The first item of <see cref="Currencies.All"/> is the default.</summary>
public enum Currency
{
    BRL,
    EUR,
    CNY,
    JPY,
}

public sealed record CurrencyInfo(Currency Currency, string Code, string Name, string Symbol);

public static class Currencies
{
    public static readonly Currency Default = Currency.BRL;

    public static readonly IReadOnlyList<CurrencyInfo> All =
    [
        new(Currency.BRL, "BRL", "Real", "R$"),
        new(Currency.EUR, "EUR", "Euro", "€"),
        new(Currency.CNY, "CNY", "Yuan", "¥"),
        new(Currency.JPY, "JPY", "Yen", "¥"),
    ];

    public static CurrencyInfo Info(Currency currency) => All.First(c => c.Currency == currency);

    public static bool TryParse(string? code, out Currency currency)
    {
        var match = All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        currency = match?.Currency ?? Default;
        return match is not null;
    }
}
