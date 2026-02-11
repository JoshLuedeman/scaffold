@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('Principal ID to grant Storage Blob Data Contributor role')
param containerAppPrincipalId string = ''

var storageName = replace(replace('${prefix}stor', '-', ''), '_', '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: length(storageName) > 24 ? substring(storageName, 0, 24) : storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
  }
}

// Storage Blob Data Contributor role: ba92f5b4-2d11-453d-a403-e96b0029c9fe
resource storageBlobContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(containerAppPrincipalId)) {
  name: guid(storageAccount.id, containerAppPrincipalId, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output storageAccountId string = storageAccount.id
output storageAccountName string = storageAccount.name
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
