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

module sql 'modules/sqlDatabase.bicep' = {
  name: 'sql'
  params: {
    prefix: prefix
    location: location
    adminPassword: sqlAdminPassword
    skuName: sqlSkuName
    skuTier: sqlSkuTier
    skuCapacity: sqlSkuCapacity
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
