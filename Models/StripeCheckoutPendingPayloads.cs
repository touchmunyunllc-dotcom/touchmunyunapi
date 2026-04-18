using System.Text.Json.Serialization;

namespace ECommerce.Models;

/// <summary>JSON stored in stripe_checkout_pending.payload_json (kind = Registered | Guest).</summary>
public sealed class StripeCheckoutPendingPayload
{
    public const string KindRegistered = "Registered";
    public const string KindGuest = "Guest";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "usd";

    /// <summary>Registered checkout only.</summary>
    [JsonPropertyName("userId")]
    public Guid? UserId { get; set; }

    [JsonPropertyName("couponId")]
    public Guid? CouponId { get; set; }

    [JsonPropertyName("shippingAddressId")]
    public Guid? ShippingAddressId { get; set; }

    /// <summary>Guest checkout only.</summary>
    [JsonPropertyName("guestEmail")]
    public string? GuestEmail { get; set; }

    [JsonPropertyName("guestName")]
    public string? GuestName { get; set; }

    [JsonPropertyName("guestAddress")]
    public GuestPendingAddress? GuestAddress { get; set; }

    [JsonPropertyName("items")]
    public List<PendingCheckoutLineItem> Items { get; set; } = new();
}

public sealed class GuestPendingAddress
{
    [JsonPropertyName("line1")]
    public string Line1 { get; set; } = string.Empty;

    [JsonPropertyName("line2")]
    public string? Line2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
}

public sealed class PendingCheckoutLineItem
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
}
