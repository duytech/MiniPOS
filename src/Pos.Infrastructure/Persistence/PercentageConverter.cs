using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Persistence;

/// <summary>Stores <see cref="Percentage"/> as numeric(7,4), matching <see cref="Percentage.MaxDecimalPlaces"/>.</summary>
internal sealed class PercentageConverter() : ValueConverter<Percentage, decimal>(p => p.Value, v => new Percentage(v))
{
    public const string ColumnType = "numeric(7,4)";
}
