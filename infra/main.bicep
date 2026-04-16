targetScope = 'resourceGroup'

@description('Environment name (dev, prod)')
param environmentName string

@description('Azure region for resources')
param location string = resourceGroup().location

@description('SQL admin password')
@secure()
param sqlAdminPassword string

@description('Entra ID Client ID for the App Registration')
param azureClientId string = ''

@description('Entra ID Tenant ID')
param azureTenantId string = tenant().tenantId

// --- SKU / scaling overrides (defaults match dev / free tier) ---

@description('SQL Database SKU name')
param sqlSkuName string = 'Basic'

@description('SQL Database SKU tier')
param sqlSkuTier string = 'Basic'

@description('SQL Database SKU capacity (DTUs)')
param sqlSkuCapacity int = 5

@description('Container App CPU cores')
param containerAppCpuCores string = '0.25'

@description('Container App memory')
param containerAppMemory string = '0.5Gi'

@description('Container App minimum replicas')
param containerAppMinReplicas int = 0

@description('Container App maximum replicas')
param containerAppMaxReplicas int = 3

@description('Static Web App SKU name')
param staticWebAppSkuName string = 'Free'

@description('Static Web App SKU tier')
param staticWebAppSkuTier string = 'Free'

@description('Container Registry SKU name')
param acrSkuName string = 'Basic'

@description('Enable VNet isolation with private endpoints for SQL and Key Vault')
param enableVnetIsolation bool = false

@description('Email address for alert notifications')
param alertEmailAddress string = ''

var prefix = 'scaffold-${environmentName}'

module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    prefix: prefix
    location: location
  }
}

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    prefix: prefix
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// --- VNet isolation (conditional) ---

module networking 'modules/networking.bicep' = if (enableVnetIsolation) {
  name: 'networking'
  params: {
    prefix: prefix
    location: location
  }
}

module sqlDnsZone 'modules/privateDnsZone.bicep' = if (enableVnetIsolation) {
  name: 'sqlDnsZone'
  params: {
    zoneName: 'privatelink.database.windows.net'
    vnetId: networking.outputs.vnetId
  }
}

module kvDnsZone 'modules/privateDnsZone.bicep' = if (enableVnetIsolation) {
  name: 'kvDnsZone'
  params: {
    zoneName: 'privatelink.vaultcore.azure.net'
    vnetId: networking.outputs.vnetId
  }
}

module sql 'modules/sqlDatabase.bicep' = {
  name: 'sql'
  params: {
    prefix: prefix
    location: location
    adminPassword: sqlAdminPassword
    skuName: sqlSkuName
    skuTier: sqlSkuTier
    skuCapacity: sqlSkuCapacity
    enablePublicAccess: !enableVnetIsolation
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    prefix: prefix
    location: location
    skuName: staticWebAppSkuName
    skuTier: staticWebAppSkuTier
  }
}

module containerApp 'modules/containerApp.bicep' = {
  name: 'containerApp'
  params: {
    prefix: prefix
    location: location
    sqlConnectionString: sql.outputs.connectionString
    corsOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
    azureClientId: azureClientId
    azureTenantId: azureTenantId
    appInsightsConnectionString: appInsights.outputs.connectionString
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
    cpuCores: containerAppCpuCores
    memory: containerAppMemory
    minReplicas: containerAppMinReplicas
    maxReplicas: containerAppMaxReplicas
    subnetId: enableVnetIsolation ? networking.outputs.containerAppsSubnetId : ''
  }
}

module acr 'modules/containerRegistry.bicep' = {
  name: 'acr'
  params: {
    prefix: prefix
    location: location
    containerAppPrincipalId: containerApp.outputs.containerAppPrincipalId
    skuName: acrSkuName
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    prefix: prefix
    location: location
    containerAppPrincipalId: containerApp.outputs.containerAppPrincipalId
    enablePublicAccess: !enableVnetIsolation
  }
}

// --- Private endpoints (conditional) ---

module sqlPrivateEndpoint 'modules/privateEndpoint.bicep' = if (enableVnetIsolation) {
  name: 'sqlPrivateEndpoint'
  params: {
    prefix: prefix
    location: location
    endpointName: 'sql'
    targetResourceId: sql.outputs.sqlServerId
    groupId: 'sqlServer'
    subnetId: networking.outputs.privateEndpointsSubnetId
    privateDnsZoneId: sqlDnsZone.outputs.dnsZoneId
  }
}

module kvPrivateEndpoint 'modules/privateEndpoint.bicep' = if (enableVnetIsolation) {
  name: 'kvPrivateEndpoint'
  params: {
    prefix: prefix
    location: location
    endpointName: 'kv'
    targetResourceId: keyVault.outputs.keyVaultId
    groupId: 'vault'
    subnetId: networking.outputs.privateEndpointsSubnetId
    privateDnsZoneId: kvDnsZone.outputs.dnsZoneId
  }
}

module storage 'modules/storageAccount.bicep' = {
  name: 'storage'
  params: {
    prefix: prefix
    location: location
    containerAppPrincipalId: containerApp.outputs.containerAppPrincipalId
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  params: {
    prefix: prefix
    location: location
    appInsightsId: appInsights.outputs.appInsightsId
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    alertEmailAddress: alertEmailAddress
  }
}

output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output storageAccountName string = storage.outputs.storageAccountName
output containerAppFqdn string = containerApp.outputs.containerAppFqdn
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
output acrLoginServer string = acr.outputs.loginServer
output acrName string = acr.outputs.name
output logAnalyticsWorkspaceId string = logAnalytics.outputs.workspaceId
output appInsightsConnectionString string = appInsights.outputs.connectionString
output alertsActionGroupId string = alerts.outputs.actionGroupId
