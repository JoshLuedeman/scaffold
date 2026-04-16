@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('Principal ID to grant AcrPull role')
param containerAppPrincipalId string = ''

@description('Container Registry SKU name')
param skuName string = 'Basic'

var acrName = replace(replace('${prefix}acr', '-', ''), '_', '')

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: length(acrName) > 50 ? substring(acrName, 0, 50) : acrName
  location: location
  sku: {
    name: skuName
  }
  properties: {
    adminUserEnabled: false
  }
}

// AcrPull role: 7f951dda-4ed3-4680-a7ca-43fe172d538d
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(containerAppPrincipalId)) {
  name: guid(acr.id, containerAppPrincipalId, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output acrId string = acr.id
output loginServer string = acr.properties.loginServer
output name string = acr.name
