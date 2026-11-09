namespace Pos.Domain.Common;

/// <summary>Why an expected operation failed. Clients branch on these; they say nothing about the transport.</summary>
public enum ErrorCode
{
    ValidationFailed,
    OrderNotFound,
    MenuItemNotFound,
    OrderItemNotFound,
    SplitNotFound,
    ConcurrentChange,
    DataConflict,
    OrderNotOpen,
    ItemNotOnOrder,
    DiscountExceedsSubtotal,
    ItemQuantityTooLarge,
    InvalidSplit,
}
