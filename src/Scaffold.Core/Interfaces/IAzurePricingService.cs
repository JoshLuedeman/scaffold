using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IAzurePricingService
{
    Task<List<RegionPricing>> GetPricingForTierAsync(string serviceTier, string computeSize, int storageGb);
    Task<List<string>> GetAvailableRegionsAsync();
}
