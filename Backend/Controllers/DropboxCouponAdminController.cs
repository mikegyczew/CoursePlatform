using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/admin/dropbox-coupons")]
public sealed class DropboxCouponAdminController(
    DropboxCouponService couponService
) : ControllerBase
{
    private const string AdminKeyHeader = "X-Coupon-Admin-Key";

    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyList<GeneratedDropboxCouponResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<GeneratedDropboxCouponResponse>>> Generate(
        [FromBody] GenerateDropboxCouponsRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!couponService.IsAdminKeyConfigured)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Generator kuponów nie jest skonfigurowany."
            );
        }

        if (
            !Request.Headers.TryGetValue(AdminKeyHeader, out var suppliedKey)
            || !couponService.IsAdminKeyValid(suppliedKey.ToString())
        )
        {
            return Unauthorized();
        }

        if (request.Count is < 1 or > 100)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Liczba kuponów musi wynosić od 1 do 100."
            );
        }

        var normalizedType = request.Type?.Trim().ToLowerInvariant();
        var type = normalizedType switch
        {
            "week" => DropboxCouponType.Week,
            "month" => DropboxCouponType.Month,
            "forever" => DropboxCouponType.Forever,
            _ => (DropboxCouponType?)null
        };
        if (type is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Typ musi mieć wartość week, month albo forever."
            );
        }

        var coupons = await couponService.GenerateAsync(
            type.Value,
            request.Count,
            cancellationToken
        );
        Response.Headers.CacheControl = "no-store";
        var typeName = type.Value switch
        {
            DropboxCouponType.Week => "week",
            DropboxCouponType.Month => "month",
            DropboxCouponType.Forever => "forever",
            _ => throw new ArgumentOutOfRangeException()
        };

        return Ok(coupons.Select(coupon =>
            new GeneratedDropboxCouponResponse(
                coupon.Code,
                typeName
            )
        ));
    }
}
