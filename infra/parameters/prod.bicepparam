// ---------------------------------------------------------------
// Estimated monthly costs (East US, pay-as-you-go, as of 2024):
//   SQL Database Standard S1 (20 DTU)      ~$30/mo
//   Container App (0.5 CPU, 1Gi, 1 min)    ~$35–50/mo (consumption)
//   Static Web App Standard                 ~$9/mo
//   Container Registry Standard             ~$5/mo
//   Log Analytics (PerGB2018, 30-day)       ~$2–5/mo (depends on ingestion)
//   Application Insights                    included with Log Analytics
//   Key Vault (standard)                    ~$0.03/10k ops
//   Storage (Standard_LRS)                  ~$0.02/GB
// Total estimate: ~$85–105/mo base + usage
// ---------------------------------------------------------------

using '../main.bicep'

param environmentName = 'prod'
param sqlAdminPassword = ''
param azureClientId = ''

// Production SKU overrides
param sqlSkuName = 'S1'
param sqlSkuTier = 'Standard'
param sqlSkuCapacity = 20

param containerAppCpuCores = '0.5'
param containerAppMemory = '1Gi'
param containerAppMinReplicas = 1
param containerAppMaxReplicas = 10

param staticWebAppSkuName = 'Standard'
param staticWebAppSkuTier = 'Standard'

param acrSkuName = 'Standard'

param enableVnetIsolation = true
