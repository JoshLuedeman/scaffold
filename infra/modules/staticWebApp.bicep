@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('Static Web App SKU name')
param skuName string = 'Free'

@description('Static Web App SKU tier')
param skuTier string = 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: '${prefix}-web'
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {}
}

output staticWebAppId string = staticWebApp.id
output defaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppName string = staticWebApp.name
