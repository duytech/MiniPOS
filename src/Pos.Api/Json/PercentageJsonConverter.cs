using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pos.Domain.Common;

namespace Pos.Api.Json;

/// <summary>
/// A percentage is a JSON number (<c>8.875</c> means 8.875%). Reading rejects negative rates and more than
/// <see cref="Percentage.MaxDecimalPlaces"/> decimals, which ASP.NET Core turns into a 400.
/// </summary>
public sealed class PercentageJsonConverter : JsonConverter<Percentage>
{
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out var value))
        {
            throw new JsonException("A percentage must be a number.");
        }

        if (value < 0)
        {
            throw new JsonException("A percentage cannot be negative.");
        }

        if (decimal.Round(value, Percentage.MaxDecimalPlaces) != value)
        {
            throw new JsonException($"A percentage can have at most {Percentage.MaxDecimalPlaces} decimal places.");
        }

        return new Percentage(value);
    }

    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.Value.ToString(CultureInfo.InvariantCulture), skipInputValidation: true);
}
