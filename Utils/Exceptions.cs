namespace ECommerce.Utils;

/// <summary>Base class for business logic exceptions with error codes.</summary>
public abstract class BusinessException : Exception
{
    public string ErrorCode { get; }

    protected BusinessException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class ProductNotFoundException : BusinessException
{
    public ProductNotFoundException(string productId)
        : base($"Product {productId} not found or inactive", "PRODUCT_NOT_FOUND") { }
}

public class InsufficientStockException : BusinessException
{
    public int AvailableQuantity { get; }

    public InsufficientStockException(string productName, int availableQuantity)
        : base($"Insufficient stock for {productName}. Available: {availableQuantity}", "INSUFFICIENT_STOCK")
    {
        AvailableQuantity = availableQuantity;
    }
}

public class CouponExpiredException : BusinessException
{
    public CouponExpiredException()
        : base("Coupon has expired", "COUPON_EXPIRED") { }
}

public class CouponNotFoundException : BusinessException
{
    public CouponNotFoundException()
        : base("Coupon not found", "COUPON_NOT_FOUND") { }
}

public class CouponUsageLimitException : BusinessException
{
    public CouponUsageLimitException()
        : base("Coupon usage limit reached", "COUPON_USAGE_LIMIT") { }
}

public class MinPurchaseAmountException : BusinessException
{
    public MinPurchaseAmountException(decimal minAmount)
        : base($"Minimum purchase amount of ${minAmount} required", "MIN_PURCHASE_NOT_MET") { }
}

public class InvalidOrderStatusException : BusinessException
{
    public InvalidOrderStatusException(string currentStatus, string targetStatus)
        : base($"Cannot transition order from {currentStatus} to {targetStatus}", "INVALID_ORDER_STATUS") { }
}

public class OrderBlockedException : BusinessException
{
    public OrderBlockedException(DateTime blockedUntil)
        : base($"Your ordering privileges are blocked until {blockedUntil:yyyy-MM-dd HH:mm} UTC due to excessive cancellations", "ORDER_BLOCKED") { }
}

public class CartValidationException : BusinessException
{
    public CartValidationException(string message)
        : base(message, "CART_VALIDATION_ERROR") { }
}

public class AmountMismatchException : BusinessException
{
    public AmountMismatchException(decimal expected, decimal provided)
        : base($"Amount mismatch. Expected: {expected}, Provided: {provided}", "AMOUNT_MISMATCH") { }
}

public class DuplicateCouponException : BusinessException
{
    public DuplicateCouponException(string code)
        : base($"Coupon code '{code}' already exists", "DUPLICATE_COUPON") { }
}
