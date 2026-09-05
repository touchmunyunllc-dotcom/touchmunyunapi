using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Utils;

public static class CustomerAccountGuard
{
    public const string AdminShoppingBlockedMessage =
        "Admin accounts cannot place storefront orders. Use a customer account or guest checkout to test purchases.";

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");

    public static IActionResult? RejectAdminShopping(ClaimsPrincipal user)
    {
        if (!IsAdmin(user))
        {
            return null;
        }

        return new ObjectResult(new { message = AdminShoppingBlockedMessage }) { StatusCode = 403 };
    }
}
