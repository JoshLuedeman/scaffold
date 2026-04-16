@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('SQL connection string (without password)')
param sqlConnectionString string = ''

@description('Allowed CORS origin (Static Web App URL)')
param corsOrigin string = ''

@description('Entra ID Client ID')
param azureClientId string = ''

@description('Entra ID Tenant ID')
param azureTenantId string = ''

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

@description('Log Analytics workspace customer ID for container app environment logs')
param logAnalyticsCustomerId string = ''

@description('Log Analytics workspace shared key for container app environment logs')
@secure()
param logAnalyticsSharedKey string = ''

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: !empty(logAnalyticsCustomerId) ? {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    } : null
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${prefix}-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        corsPolicy: !empty(corsOrigin) ? {
          allowedOrigins: [corsOrigin]
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          allowCredentials: true
        } : null
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(
            !empty(sqlConnectionString) ? [{ name: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }] : [],
            !empty(appInsightsConnectionString) ? [{ name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }] : [],
            !empty(azureClientId) ? [
              { name: 'AzureAd__TenantId', value: azureTenantId }
              { name: 'AzureAd__ClientId', value: azureClientId }
              { name: 'AzureAd__Audience', value: 'api://${azureClientId}' }
            ] : [
              { name: 'DisableAuth', value: 'true' }
            ]
          )
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

output containerAppId string = containerApp.id
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppPrincipalId string = containerApp.identity.principalId
output environmentId string = containerAppEnv.id
