@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('SQL admin password')
@secure()
param adminPassword string

@description('SQL Database SKU name')
param skuName string = 'Basic'

@description('SQL Database SKU tier')
param skuTier string = 'Basic'

@description('SQL Database SKU capacity (DTUs)')
param skuCapacity int = 5

var serverName = '${prefix}-sql'
var dbName = '${prefix}-db'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: 'scaffoldadmin'
    administratorLoginPassword: adminPassword
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: dbName
  location: location
  sku: {
    name: skuName
    tier: skuTier
    capacity: skuCapacity
  }
}

resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output sqlServerId string = sqlServer.id
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;'
