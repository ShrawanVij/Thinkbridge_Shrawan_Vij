@description('Environment name, e.g. dev or prod')
param environmentName string

@description('Location for the alert rule')
param location string = resourceGroup().location

@description('Resource ID of the existing Application Insights component (from Day 22-25\'s monitoring module) to alert on')
param applicationInsightsResourceId string

@description('Action group resource IDs to notify when the error-rate alert fires')
param actionGroupResourceIds array = []

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  params: {
    applicationInsightsResourceId: applicationInsightsResourceId
    location: location
    environmentName: environmentName
    actionGroupResourceIds: actionGroupResourceIds
  }
}

output alertResourceId string = alerts.outputs.alertResourceId
