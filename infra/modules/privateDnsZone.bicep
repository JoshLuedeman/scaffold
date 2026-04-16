@description('Private DNS zone name (e.g., privatelink.database.windows.net)')
param zoneName string

@description('VNet ID to link')
param vnetId string

resource dnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: zoneName
  location: 'global'
}

resource vnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: dnsZone
  name: '${zoneName}-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnetId
    }
    registrationEnabled: false
  }
}

output dnsZoneId string = dnsZone.id
