using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pos.Domain.Common;

namespace Pos.Api.Json;

/// <summary>
/// Money is a JSON number with exactly 2 decimals (<c>15.40</c>). Only USD exists for now, so the currency is not part
/// of the payload. Reading rejects amounts that are not whole cents, which ASP.NET Core turns into a 400.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out var amount))
        {
            throw new JsonException("Money must be a number.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new JsonException("Money must have at most 2 decimal places.");
        }

        return Money.Usd(amount);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.Amount.ToString("0.00", CultureInfo.InvariantCulture), skipInputValidation: true);
}
