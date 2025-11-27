// Main Bicep Template - Orchestrates deployment of all resources
// Conditionally deploys GenAI resources based on parameter

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Azure AD admin login name (User Principal Name)')
param adminLogin string

@description('Azure AD admin Object ID')
param adminObjectId string

@description('Deploy GenAI resources (Azure OpenAI, AI Search)')
param deployGenAI bool = false

var uniqueSuffix = uniqueString(resourceGroup().id)

// App Service Module
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
  }
}

// Azure SQL Module
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// GenAI Module (conditional)
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: 'swedencentral'
    baseName: baseName
    uniqueSuffix: toLower(uniqueSuffix)
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output resourceGroupName string = resourceGroup().name
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl
output managedIdentityId string = appService.outputs.managedIdentityId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityName string = appService.outputs.managedIdentityName
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output sqlServerIdentityPrincipalId string = azureSql.outputs.sqlServerIdentityPrincipalId

// GenAI outputs (conditional with null-safe operators)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
