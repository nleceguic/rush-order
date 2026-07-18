using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Tables.DTOs;
using RushOrder.Application.Tables.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/qr")]
public sealed class QrController : ApiController
{
    // GET /api/v1/qr/{qrCode}
    // Resolves a table's QR code into the restaurant/table info the client
    // needs before it can place an order (table id, restaurant id, VAT/payment
    // settings) — this is what the PWA calls right after scanning a QR.
    [HttpGet("{qrCode}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TablePublicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByQrCode(string qrCode, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetTableByQrCodeQuery(qrCode), ct);
        if (result is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=30";
        return OkData(result);
    }
}
