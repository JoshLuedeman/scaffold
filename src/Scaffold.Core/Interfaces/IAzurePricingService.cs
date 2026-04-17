using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IAzurePricingService
{
    Task<List<RegionPricing>> GetPricingForTierAsync(string serviceTier, string computeSize, int storageGb, DatabasePlatform platform = DatabasePlatform.SqlServer);
    Task<List<string>> GetAvailableRegionsAsync(DatabasePlatform platform = DatabasePlatform.SqlServer);
}
