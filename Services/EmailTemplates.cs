using System.Net;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Services;

public sealed class EmailBrandOptions
{
    public string FrontendUrl { get; init; } = "https://touchmunyun.com";
    public string LogoUrl { get; init; } = "";
    public string BrandName { get; init; } = "Touch Munyun";
    public string SupportEmail { get; init; } = "TouchMunyunLLC@gmail.com";

    public static EmailBrandOptions FromConfiguration(IConfiguration configuration)
    {
        var frontendUrl = (configuration["FrontendUrl"] ?? "https://touchmunyun.com").TrimEnd('/');
        var siteUrl = (configuration["Email:SiteUrl"] ?? frontendUrl).TrimEnd('/');
        var logoUrl = configuration["Email:LogoUrl"]?.Trim();
        if (string.IsNullOrEmpty(logoUrl))
        {
            logoUrl = $"{siteUrl}/icons/icon-192x192.png";
        }

        return new EmailBrandOptions
        {
            FrontendUrl = frontendUrl,
            LogoUrl = logoUrl,
            BrandName = configuration["Email:FromName"] ?? "Touch Munyun",
            SupportEmail = configuration["Email:FromEmail"]
                ?? configuration["Admin:Email"]
                ?? "TouchMunyunLLC@gmail.com",
        };
    }
}

/// <summary>Table-based, inline-styled HTML for Resend / email clients.</summary>
public static class EmailTemplates
{
    private const string HeaderBg = "#5A5D68";
    private const string AccentRed = "#dc2626";
    private const string TextDark = "#1f2937";
    private const string TextMuted = "#6b7280";
    private const string CardBg = "#ffffff";
    private const string PageBg = "#f3f4f6";

    public static string Encode(string value) => WebUtility.HtmlEncode(value);

    public static string Layout(
        EmailBrandOptions brand,
        string headline,
        string contentHtml,
        string? buttonUrl = null,
        string? buttonText = null,
        bool adminAlert = false)
    {
        var safeHeadline = Encode(headline);
        var safeBrand = Encode(brand.BrandName);
        var logoUrl = Encode(brand.LogoUrl);
        var homeUrl = Encode(brand.FrontendUrl);

        var buttonBlock = string.IsNullOrEmpty(buttonUrl) || string.IsNullOrEmpty(buttonText)
            ? ""
            : $@"
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:24px auto 0;"">
  <tr>
    <td align=""center"" style=""border-radius:8px;background:{AccentRed};"">
      <a href=""{Encode(buttonUrl)}"" target=""blank"" style=""display:inline-block;padding:14px 28px;font-size:15px;font-weight:bold;color:#ffffff;text-decoration:none;border-radius:8px;"">{Encode(buttonText)}</a>
    </td>
  </tr>
</table>";

        var adminBadge = adminAlert
            ? $@"<p style=""margin:8px 0 0;font-size:12px;font-weight:bold;letter-spacing:0.06em;text-transform:uppercase;color:#fca5a5;"">Admin alert</p>"
            : "";

        var year = DateTime.UtcNow.Year;
        var support = Encode(brand.SupportEmail);
        var siteLabel = Encode(brand.FrontendUrl.Replace("https://", "").Replace("http://", ""));

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{safeHeadline}</title>
</head>
<body style=""margin:0;padding:0;background:{PageBg};"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{PageBg};"">
    <tr>
      <td align=""center"" style=""padding:24px 12px;"">
        <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;"">
          <tr>
            <td style=""background:{HeaderBg};border-radius:12px 12px 0 0;padding:28px 24px;text-align:center;"">
              <a href=""{homeUrl}"" style=""text-decoration:none;"">
                <img src=""{logoUrl}"" alt=""{safeBrand}"" width=""72"" height=""72"" style=""display:block;margin:0 auto;border:0;border-radius:12px;"" />
              </a>
              <p style=""margin:14px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:bold;color:#ffffff;"">{safeBrand}</p>
              {adminBadge}
              <p style=""margin:10px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:18px;color:#f3f4f6;"">{safeHeadline}</p>
            </td>
          </tr>
          <tr>
            <td style=""background:{CardBg};padding:32px 28px;border-radius:0 0 12px 12px;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.6;color:{TextDark};"">
              {contentHtml}
              {buttonBlock}
              <p style=""margin:32px 0 0;padding-top:20px;border-top:1px solid #e5e7eb;font-size:13px;color:{TextMuted};text-align:center;"">
                © {year} Touch Munyun LLC<br />
                <a href=""{homeUrl}"" style=""color:{AccentRed};text-decoration:none;"">{siteLabel}</a>
                · <a href=""mailto:{support}"" style=""color:{AccentRed};text-decoration:none;"">{support}</a>
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    public static string Card(string innerHtml) =>
        $@"<div style=""background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;padding:20px;margin:16px 0;"">{innerHtml}</div>";

    public static string DataTable(string rowsHtml) =>
        $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;margin:12px 0;"">{rowsHtml}</table>";

    public static string DataRow(string label, string value, bool shaded = false) =>
        $@"<tr style=""background:{(shaded ? "#f9fafb" : "#ffffff")};"">
  <td style=""padding:10px 8px;font-weight:bold;color:{TextMuted};width:38%;vertical-align:top;"">{Encode(label)}</td>
  <td style=""padding:10px 8px;color:{TextDark};"">{value}</td>
</tr>";

    public static string OrderItemsTableHeader() =>
        @"<tr style=""background:#f3f4f6;"">
  <th align=""left"" style=""padding:10px 8px;font-size:13px;"">Item</th>
  <th align=""center"" style=""padding:10px 8px;font-size:13px;"">Qty</th>
  <th align=""right"" style=""padding:10px 8px;font-size:13px;"">Each</th>
  <th align=""right"" style=""padding:10px 8px;font-size:13px;"">Total</th>
</tr>";

    public static string OrderItemRow(string name, int qty, decimal unitPrice, decimal lineTotal) =>
        $@"<tr>
  <td style=""padding:10px 8px;border-bottom:1px solid #eee;"">{Encode(name)}</td>
  <td align=""center"" style=""padding:10px 8px;border-bottom:1px solid #eee;"">{qty}</td>
  <td align=""right"" style=""padding:10px 8px;border-bottom:1px solid #eee;"">${unitPrice:F2}</td>
  <td align=""right"" style=""padding:10px 8px;border-bottom:1px solid #eee;"">${lineTotal:F2}</td>
</tr>";

    public static string StatusBadge(string status) =>
        $@"<span style=""display:inline-block;padding:6px 14px;background:{AccentRed};color:#fff;border-radius:999px;font-size:13px;font-weight:bold;"">{Encode(status)}</span>";

    public static string Paragraph(string text) =>
        $@"<p style=""margin:0 0 12px;"">{Encode(text)}</p>";

    public static string RichParagraph(string htmlEncodedOrSafeHtml) =>
        $@"<p style=""margin:0 0 12px;"">{htmlEncodedOrSafeHtml}</p>";
}
