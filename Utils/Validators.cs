using ECommerce.DTOs;
using FluentValidation;

namespace ECommerce.Utils;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, and one number");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(255).WithMessage("Name must not exceed 255 characters");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters")
            .Matches(@"^\+?[\d\s\-\(\)]+$").WithMessage("Invalid phone number format")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

public class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetRequest>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");
    }
}

public class VerifyPasswordResetValidator : AbstractValidator<VerifyPasswordResetRequest>
{
    public VerifyPasswordResetValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, and one number");
    }
}

public class AddToCartRequestValidator : AbstractValidator<AddToCartRequest>
{
    public AddToCartRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100");
    }
}

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100");
    }
}

public class ApplyCouponRequestValidator : AbstractValidator<ApplyCouponRequest>
{
    public ApplyCouponRequestValidator()
    {
        RuleFor(x => x.CouponCode)
            .NotEmpty().WithMessage("Coupon code is required")
            .MaximumLength(50).WithMessage("Coupon code must not exceed 50 characters");
    }
}

public class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.AddressLine1)
            .NotEmpty().WithMessage("Address line 1 is required")
            .MaximumLength(255).WithMessage("Address line 1 must not exceed 255 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required")
            .MaximumLength(100).WithMessage("State must not exceed 100 characters");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("Postal code is required")
            .MaximumLength(20).WithMessage("Postal code must not exceed 20 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters")
            .When(x => x.Phone != null);
    }
}

public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.AddressLine1)
            .MaximumLength(255).WithMessage("Address line 1 must not exceed 255 characters")
            .When(x => x.AddressLine1 != null);

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City must not exceed 100 characters")
            .When(x => x.City != null);

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State must not exceed 100 characters")
            .When(x => x.State != null);

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code must not exceed 20 characters")
            .When(x => x.PostalCode != null);

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters")
            .When(x => x.Country != null);

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters")
            .When(x => x.Phone != null);
    }
}

public class GuestCheckoutPreviewRequestValidator : AbstractValidator<GuestCheckoutPreviewRequest>
{
    public GuestCheckoutPreviewRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required")
            .Must(items => items.Count > 0).WithMessage("At least one item is required");
    }
}

public class GuestCheckoutRequestValidator : AbstractValidator<GuestCheckoutRequest>
{
    public GuestCheckoutRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(255).WithMessage("Name must not exceed 255 characters");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required")
            .Must(items => items.Count > 0).WithMessage("At least one item is required");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("Total amount must be greater than 0");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter code");

        RuleFor(x => x.ShippingAddress)
            .NotNull().WithMessage("Shipping address is required");
    }
}

public class CreateSlideRequestValidator : AbstractValidator<CreateSlideRequest>
{
    public CreateSlideRequestValidator()
    {
        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _)).WithMessage("Image URL must be a valid URL");

        RuleFor(x => x.Alt)
            .NotEmpty().WithMessage("Alt text is required")
            .MaximumLength(255).WithMessage("Alt text must not exceed 255 characters");

        RuleFor(x => x.Title)
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters")
            .When(x => x.Title != null);

        RuleFor(x => x.CtaText)
            .MaximumLength(100).WithMessage("CTA text must not exceed 100 characters")
            .When(x => x.CtaText != null);

        RuleFor(x => x.CtaLink)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("CTA link must be a valid URL")
            .When(x => x.CtaLink != null);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order must be 0 or greater")
            .When(x => x.Order.HasValue);
    }
}

public class UpdateSlideRequestValidator : AbstractValidator<UpdateSlideRequest>
{
    public UpdateSlideRequestValidator()
    {
        RuleFor(x => x.ImageUrl)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Image URL must be a valid URL")
            .When(x => x.ImageUrl != null);

        RuleFor(x => x.Alt)
            .MaximumLength(255).WithMessage("Alt text must not exceed 255 characters")
            .When(x => x.Alt != null);

        RuleFor(x => x.Title)
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters")
            .When(x => x.Title != null);

        RuleFor(x => x.CtaText)
            .MaximumLength(100).WithMessage("CTA text must not exceed 100 characters")
            .When(x => x.CtaText != null);

        RuleFor(x => x.CtaLink)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("CTA link must be a valid URL")
            .When(x => x.CtaLink != null);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order must be 0 or greater")
            .When(x => x.Order.HasValue);
    }
}
