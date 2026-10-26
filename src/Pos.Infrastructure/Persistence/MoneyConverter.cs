using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Persistence;

/// <summary>Stores <see cref="Money"/> as numeric(12,2). Only USD exists for now, so the currency is not stored.</summary>
internal sealed class MoneyConverter() : ValueConverter<Money, decimal>(m => m.Amount, a => Money.Usd(a))
{
    public const string ColumnType = "numeric(12,2)";
}
