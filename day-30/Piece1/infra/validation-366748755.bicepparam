using 'main.bicep'

param apiCpu = '0.25'

param apiImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

param apiMaxReplicas = 1

param apiMemory = '0.5Gi'

param apiMinReplicas = 0

param containerAppsEnvironmentResourceId = '/subscriptions/5448e1e9-8c94-4d57-ac78-37733af38ee8/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env'

param entraIdClientId = '9ec5db29-9812-43ac-9d81-8817e84b6cd0'

param environmentName = 'dev'

param location = 'centralindia'

param serviceBusSkuName = 'Standard'

param sqlMaxSizeBytes = 2147483648

param sqlSkuName = 'Basic'

param sqlSkuTier = 'Basic'
