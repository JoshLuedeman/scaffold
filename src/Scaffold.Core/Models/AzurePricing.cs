namespace Scaffold.Core.Models;

public class AzurePriceItem
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RetailPrice { get; set; }
    public string ArmRegionName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string MeterName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPrimaryMeterRegion { get; set; }
    public decimal UnitPrice { get; set; }
}

public class RegionPricing
{
    public string ArmRegionName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal EstimatedMonthlyCostUsd { get; set; }
}
