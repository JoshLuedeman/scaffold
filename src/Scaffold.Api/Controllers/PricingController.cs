using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pricing")]
public class PricingController : ControllerBase
{
    private readonly IAzurePricingService _pricingService;

    public PricingController(IAzurePricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpGet("regions")]
    public async Task<IActionResult> GetRegions()
    {
        var regions = await _pricingService.GetAvailableRegionsAsync();
        return Ok(regions);
    }

    [HttpGet("estimate")]
    public async Task<IActionResult> GetEstimate(
        [FromQuery] string service,
        [FromQuery] string compute,
        [FromQuery] int storageGb = 32)
    {
        var pricing = await _pricingService.GetPricingForTierAsync(service, compute, storageGb);
        var sorted = pricing.OrderBy(p => p.EstimatedMonthlyCostUsd).ToList();
        return Ok(sorted);
    }
}
