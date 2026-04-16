@description('Resource name prefix')
param prefix string

@description('Azure region')
param location string

@description('Application Insights resource ID')
param appInsightsId string

@description('Log Analytics workspace ID')
param logAnalyticsWorkspaceId string

@description('Email address for alert notifications')
param alertEmailAddress string = ''

// Action Group
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${prefix}-alerts-ag'
  location: 'global'
  properties: {
    groupShortName: 'ScaffoldAG'
    enabled: true
    emailReceivers: !empty(alertEmailAddress) ? [
      {
        name: 'AdminEmail'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ] : []
  }
}

// Alert 1: API 5xx error rate > 5 in 5 minutes
resource api5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-api-5xx-alert'
  location: 'global'
  properties: {
    description: 'API 5xx error rate exceeds threshold'
    severity: 1
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ServerErrors'
          metricName: 'requests/failed'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// Alert 2: Container App restarts (scheduled query)
resource containerRestartAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${prefix}-container-restart-alert'
  location: location
  properties: {
    description: 'Container App has restarted'
    severity: 2
    enabled: true
    scopes: [logAnalyticsWorkspaceId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'ContainerAppSystemLogs_CL | where Reason_s == "BackOff" or Reason_s == "CrashLoopBackOff" | summarize RestartCount = count() by bin(TimeGenerated, 5m)'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

// Alert 3: Migration failure events (custom log query)
resource migrationFailureAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${prefix}-migration-failure-alert'
  location: location
  properties: {
    description: 'Database migration has failed'
    severity: 1
    enabled: true
    scopes: [logAnalyticsWorkspaceId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'AppTraces | where Message contains "Migration failed" or Message contains "migration error" | summarize FailCount = count() by bin(TimeGenerated, 5m)'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

// Alert 4: SignalR connection failures
resource signalrFailureAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${prefix}-signalr-failure-alert'
  location: location
  properties: {
    description: 'SignalR connection failures detected'
    severity: 2
    enabled: true
    scopes: [logAnalyticsWorkspaceId]
    evaluationFrequency: 'PT10M'
    windowSize: 'PT10M'
    criteria: {
      allOf: [
        {
          query: 'AppExceptions | where ExceptionType contains "SignalR" or ExceptionType contains "HubException" | summarize ExceptionCount = count() by bin(TimeGenerated, 10m)'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

// Alert 5: High response time
resource highLatencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-high-latency-alert'
  location: 'global'
  properties: {
    description: 'API response time exceeds 5 seconds'
    severity: 2
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighLatency'
          metricName: 'requests/duration'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 5000
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

output actionGroupId string = actionGroup.id
