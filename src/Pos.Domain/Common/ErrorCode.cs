namespace Pos.Domain.Common;

/// <summary>Why an expected operation failed. Clients branch on these; they say nothing about the transport.</summary>
public enum ErrorCode
{
    ValidationFailed,
    OrderNotFound,
    MenuItemNotFound,
    OrderLineNotFound,
    SplitNotFound,
    ConcurrentChange,
    DataConflict,
    OrderNotOpen,
    LineNotOnOrder,
    DiscountExceedsSubtotal,
    LineQuantityTooLarge,
    InvalidSplit,
}
