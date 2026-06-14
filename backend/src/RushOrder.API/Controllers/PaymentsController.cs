using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RushOrder.API.Common;
using RushOrder.API.Options;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.Commands;
using RushOrder.Application.Payments.DTOs;
using RushOrder.Domain.Enums;

namespace RushOrder.API.Controllers;

[Route("api/v1/payments")]
public sealed class PaymentsController : ApiController
{
    private readonly IStripeWebhookService _webhookService;
    private readonly string _publishableKey;

    public PaymentsController(
        IStripeWebhookService webhookService,
        IOptions<StripeApiOptions> stripeApiOptions)
    {
        _webhookService = webhookService;
        _publishableKey = stripeApiOptions.Value.PublishableKey;
    }

    // POST /api/v1/payments/intent
    [HttpPost("intent")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateIntent([FromBody] CreateIntentRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(
                new CreatePaymentIntentCommand(req.OrderId, req.Method, req.CustomerId), ct);

            return OkData(new
            {
                clientSecret = result.ClientSecret,
                paymentId = result.PaymentId,
                paymentIntentId = result.PaymentIntentId,
                publishableKey = _publishableKey
            });
        });

    // POST /api/v1/payments/confirm
    [HttpPost("confirm")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ConfirmPaymentResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Confirm([FromBody] ConfirmRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new ConfirmPaymentCommand(req.PaymentIntentId), ct);
            return OkData(result);
        });

    // GET /api/v1/payments/{orderId}
    [HttpGet("{orderId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var payment = await Mediator.Send(
                new Application.Payments.Queries.GetPaymentByOrderIdQuery(orderId), ct);

            return payment is null
                ? NotFoundResponse($"No payment found for order {orderId}.")
                : OkData(payment);
        });

    // POST /api/v1/payments/{id}/refund
    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(
                new RefundPaymentCommand(id, req.Amount, req.Reason ?? "requested_by_customer"), ct);
            return OkData(result);
        });

    // POST /api/v1/payments/split
    [HttpPost("split")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SplitPaymentIntentResult>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Split([FromBody] SplitRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var splits = req.Splits
                .Select(s => new SplitEntry(s.CustomerId, s.Amount))
                .ToList()
                .AsReadOnly();

            var results = await Mediator.Send(
                new CreateSplitPaymentCommand(req.OrderId, req.Method, splits), ct);
            return OkData(results);
        });

    // POST /api/v1/payments/webhook
    // Reads raw body — must be registered before body-reading middleware touches it
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
            return BadRequest("Missing Stripe-Signature header.");

        try
        {
            await _webhookService.ProcessAsync(rawBody, signature, ct);
            return Ok();
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Webhook signature validation failed.");
        }
    }
}

// ── Request DTOs (controller-local, no need to pollute Application layer) ──

public record CreateIntentRequest(Guid OrderId, PaymentMethod Method, Guid? CustomerId = null);
public record ConfirmRequest(string PaymentIntentId);
public record RefundRequest(decimal? Amount = null, string? Reason = null);
public record SplitEntryRequest(Guid? CustomerId, decimal Amount);
public record SplitRequest(Guid OrderId, PaymentMethod Method, IReadOnlyList<SplitEntryRequest> Splits);
