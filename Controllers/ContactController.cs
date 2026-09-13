using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using ECommerce.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("strict")]
public class ContactController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContactController> _logger;
    private readonly IRecaptchaService _recaptchaService;
    private readonly IDbConnection _connection;

    public ContactController(
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ContactController> logger,
        IRecaptchaService recaptchaService,
        IDbConnection connection)
    {
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _recaptchaService = recaptchaService;
        _connection = connection;
    }

    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "All fields are required." });
        }

        // Verify reCAPTCHA
        var clientIp = ClientIpHelper.GetClientIpAddress(HttpContext);
        var captchaValid = await _recaptchaService.VerifyAsync(request.CaptchaToken, "contact", clientIp);
        if (!captchaValid)
        {
            return BadRequest(new { message = "CAPTCHA verification failed. Please try again." });
        }

        try
        {
            // Store contact message in database
            await _connection.ExecuteAsync(
                @"INSERT INTO contact_messages (name, email, subject, message)
                  VALUES (@Name, @Email, @Subject, @Message)",
                new { request.Name, request.Email, request.Subject, request.Message });

            var adminEmail = _configuration["AdminSettings:Email"]
                ?? _configuration["Admin:Email"]
                ?? "TouchMunyunLLC@gmail.com";

            var brand = EmailBrandOptions.FromConfiguration(_configuration);
            var inner = EmailTemplates.DataTable(
                EmailTemplates.DataRow("Name", EmailTemplates.Encode(request.Name))
                + EmailTemplates.DataRow(
                    "Email",
                    $@"<a href=""mailto:{EmailTemplates.Encode(request.Email)}"">{EmailTemplates.Encode(request.Email)}</a>",
                    shaded: true)
                + EmailTemplates.DataRow("Subject", EmailTemplates.Encode(request.Subject)))
                + EmailTemplates.Card(
                    $@"<h3 style=""margin:0 0 8px;font-size:15px;"">Message</h3>
<p style=""margin:0;white-space:pre-wrap;"">{EmailTemplates.Encode(request.Message)}</p>")
                + EmailTemplates.Paragraph($"Sent from the contact form at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.");

            var htmlBody = EmailTemplates.Layout(
                brand,
                "Contact form message",
                inner,
                adminAlert: true);

            var textBody = $"Contact Form Submission\n\nName: {request.Name}\nEmail: {request.Email}\nSubject: {request.Subject}\n\nMessage:\n{request.Message}";

            await _emailService.SendEmailAsync(
                adminEmail,
                $"Contact Form: {request.Subject}",
                textBody,
                htmlBody);

            _logger.LogInformation("Contact form submitted by {Email} - Subject: {Subject}", request.Email, request.Subject);

            return Ok(new { message = "Thank you! Your message has been sent. We'll get back to you soon." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact form email from {Email}", request.Email);
            return StatusCode(500, new { message = "Failed to send message. Please try again later or email us directly." });
        }
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<ContactMessage>), 200)]
    public async Task<IActionResult> GetContactMessages([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isRead = null)
    {
        var offset = (page - 1) * pageSize;
        var whereClause = isRead.HasValue ? "WHERE is_read = @IsRead" : "";

        var messages = await _connection.QueryAsync<ContactMessage>(
            $@"SELECT id AS Id, name AS Name, email AS Email, subject AS Subject, 
                      message AS Message, is_read AS IsRead, created_at AS CreatedAt
               FROM contact_messages {whereClause}
               ORDER BY created_at DESC
               LIMIT @PageSize OFFSET @Offset",
            new { IsRead = isRead, PageSize = pageSize, Offset = offset });

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM contact_messages {whereClause}",
            new { IsRead = isRead });

        return Ok(new { messages, totalCount, page, pageSize });
    }

    [HttpPut("{id:guid}/read")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var affected = await _connection.ExecuteAsync(
            "UPDATE contact_messages SET is_read = TRUE WHERE id = @Id",
            new { Id = id });

        if (affected == 0) return NotFound();
        return Ok(new { message = "Marked as read" });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteContactMessage(Guid id)
    {
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM contact_messages WHERE id = @Id",
            new { Id = id });

        if (affected == 0) return NotFound();
        return Ok(new { message = "Contact message deleted" });
    }
}

