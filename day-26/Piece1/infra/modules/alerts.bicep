@description('Resource ID of the existing Application Insights component to alert on')
param applicationInsightsResourceId string

@description('Location for the alert rule (must match a region Azure Monitor alerting supports)')
param location string = resourceGroup().location

@description('Environment name — used to name the alert and in its display name')
param environmentName string

@description('Action group resource IDs to notify when the alert fires')
param actionGroupResourceIds array = []

// Same query as kql/error-rate-alert-query.kql — kept in one place there as the reference copy;
// this is what actually runs.
var errorRateQuery = '''
requests
| where timestamp > ago(5m)
| summarize total = count(), failed = countif(success == false)
| extend errorRatePercent = round(100.0 * failed / total, 2)
| where total >= 10 and errorRatePercent > 5
'''

resource errorRateAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-error-rate-${environmentName}'
  location: location
  properties: {
    displayName: 'QuotesApi error rate > 5% (${environmentName})'
    description: 'Fires when more than 5% of requests fail in a 5-minute window, with at least 10 requests in that window so a quiet period does not read as 100% failed.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsResourceId
    ]
    criteria: {
      allOf: [
        {
          query: errorRateQuery
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          // The query itself already filters down to "only rows where the threshold was
          // crossed" — any row returned at all means the 5-minute window failed the check,
          // so this only has to ask "did the query return anything".
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroupResourceIds
    }
    autoMitigate: true
  }
}

output alertResourceId string = errorRateAlert.id
