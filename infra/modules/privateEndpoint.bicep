@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('Name suffix for the private endpoint')
param endpointName string

@description('Resource ID of the target service')
param targetResourceId string

@description('Group ID for the private link (e.g., sqlServer, vault)')
param groupId string

@description('Subnet ID for the private endpoint')
param subnetId string

@description('Private DNS zone ID')
param privateDnsZoneId string

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: '${prefix}-pe-${endpointName}'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-pls-${endpointName}'
        properties: {
          privateLinkServiceId: targetResourceId
          groupIds: [groupId]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

output privateEndpointId string = privateEndpoint.id
