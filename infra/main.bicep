targetScope = 'resourceGroup'

@description('Environment name (dev, prod)')
param environmentName string

@description('Azure region for resources')
param location string = resourceGroup().location

@description('SQL admin password')
@secure()
param sqlAdminPassword string

var prefix = 'scaffold-${environmentName}'

module sql 'modules/sqlDatabase.bicep' = {
  name: 'sql'
  params: {
    prefix: prefix
    location: location
    adminPassword: sqlAdminPassword
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    prefix: prefix
    location: location
  }
}

module containerApp 'modules/containerApp.bicep' = {
  name: 'containerApp'
  params: {
    prefix: prefix
    location: location
    sqlConnectionString: sql.outputs.connectionString
    corsOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
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
