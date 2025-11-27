#!/bin/bash

# deploy-with-chat.sh - Deployment script with GenAI services for Chat UI
# Deploys all resources including Azure OpenAI and AI Search
# 
# Prerequisites:
# - Azure CLI installed and logged in (az login)
# - Set subscription context (az account set --subscription <subscription-id>)
#
# Usage: ./deploy-with-chat.sh

set -e

echo "=========================================="
echo "Expense Management System - Full Deployment with GenAI"
echo "=========================================="
echo ""

# Configuration - Update these values
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user info for SQL admin
echo "Step 1: Getting current user information..."
ADMIN_LOGIN=$(az account show --query user.name -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "  Admin Login: $ADMIN_LOGIN"
echo "  Admin Object ID: $ADMIN_OBJECT_ID"
echo ""

# Create resource group if it doesn't exist
echo "Step 2: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
echo "  ✓ Resource group '$RESOURCE_GROUP' ready"
echo ""

# Deploy infrastructure with GenAI
echo "Step 3: Deploying infrastructure (App Service, Managed Identity, SQL Database, GenAI)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infrastructure/main.bicep \
    --parameters baseName=$BASE_NAME adminLogin="$ADMIN_LOGIN" adminObjectId=$ADMIN_OBJECT_ID deployGenAI=true \
    --query "properties.outputs" -o json)

# Extract outputs
WEB_APP_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppName.value')
WEB_APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppUrl.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
SQL_SERVER_IDENTITY_PRINCIPAL_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerIdentityPrincipalId.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo "  ✓ Infrastructure deployed"
echo "  Web App: $WEB_APP_NAME"
echo "  SQL Server: $SQL_SERVER_NAME"
echo "  Database: $DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model: $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"
echo ""

# Wait for SQL Server to be ready
echo "Step 4: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "  ✓ Wait complete"
echo ""

# Add current IP to firewall
echo "Step 5: Adding current IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "LocalDevelopment" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none 2>/dev/null || echo "  Firewall rule may already exist"
echo "  ✓ Firewall rule added for IP: $MY_IP"
echo ""

# Grant Directory Reader role to SQL Server identity
echo "Step 5b: Granting Directory Reader role to SQL Server identity..."
DIRECTORY_READER_ROLE_ID=$(az rest --method GET --uri "https://graph.microsoft.com/v1.0/directoryRoles?\$filter=roleTemplateId eq '88d8e3e3-8f55-4a1e-953a-9b9898b8876b'" --headers "Content-Type=application/json" --query "value[0].id" -o tsv 2>/dev/null || true)

if [ -z "$DIRECTORY_READER_ROLE_ID" ]; then
    echo "  Activating Directory Reader role..."
    DIRECTORY_READER_ROLE_ID=$(az rest --method POST --uri "https://graph.microsoft.com/v1.0/directoryRoles" --headers "Content-Type=application/json" --body "{\"roleTemplateId\": \"88d8e3e3-8f55-4a1e-953a-9b9898b8876b\"}" --query "id" -o tsv 2>/dev/null || true)
fi

if [ -n "$DIRECTORY_READER_ROLE_ID" ] && [ -n "$SQL_SERVER_IDENTITY_PRINCIPAL_ID" ]; then
    echo "  Assigning Directory Reader role to SQL Server identity..."
    az rest --method POST \
        --uri "https://graph.microsoft.com/v1.0/directoryRoles/${DIRECTORY_READER_ROLE_ID}/members/\$ref" \
        --headers "Content-Type=application/json" \
        --body "{\"@odata.id\": \"https://graph.microsoft.com/v1.0/directoryObjects/${SQL_SERVER_IDENTITY_PRINCIPAL_ID}\"}" 2>/dev/null || echo "  Role may already be assigned"
    echo "  ✓ Directory Reader role granted to SQL Server identity"
else
    echo "  ⚠ Could not assign Directory Reader role (requires admin permissions)"
fi
echo ""

# Install Python packages and run SQL scripts
echo "Step 6: Installing Python packages..."
pip3 install --quiet pyodbc azure-identity
echo "  ✓ Python packages installed"
echo ""

# Update run-sql.py with correct values
echo "Step 7: Configuring SQL scripts..."
cp run-sql.py /tmp/run-sql.py
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" /tmp/run-sql.py && rm -f /tmp/run-sql.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" /tmp/run-sql.py && rm -f /tmp/run-sql.py.bak
echo "  ✓ SQL script configured"
echo ""

# Import database schema
echo "Step 8: Importing database schema..."
python3 /tmp/run-sql.py
echo "  ✓ Database schema imported"
echo ""

# Configure managed identity user in database
echo "Step 9: Configuring managed identity database access..."
cp run-sql-dbrole.py /tmp/run-sql-dbrole.py
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" /tmp/run-sql-dbrole.py && rm -f /tmp/run-sql-dbrole.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" /tmp/run-sql-dbrole.py && rm -f /tmp/run-sql-dbrole.py.bak

# Create script.sql with managed identity name
cat > /tmp/script.sql << EOF
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$MANAGED_IDENTITY_NAME')
BEGIN
    DROP USER [$MANAGED_IDENTITY_NAME];
END
GO
CREATE USER [$MANAGED_IDENTITY_NAME] FROM EXTERNAL PROVIDER;
GO
ALTER ROLE db_datareader ADD MEMBER [$MANAGED_IDENTITY_NAME];
GO
ALTER ROLE db_datawriter ADD MEMBER [$MANAGED_IDENTITY_NAME];
GO
GRANT EXECUTE TO [$MANAGED_IDENTITY_NAME];
GO
EOF

python3 /tmp/run-sql-dbrole.py
echo "  ✓ Managed identity configured in database"
echo ""

# Create stored procedures
echo "Step 10: Creating stored procedures..."
cp run-sql-stored-procs.py /tmp/run-sql-stored-procs.py
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" /tmp/run-sql-stored-procs.py && rm -f /tmp/run-sql-stored-procs.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" /tmp/run-sql-stored-procs.py && rm -f /tmp/run-sql-stored-procs.py.bak
python3 /tmp/run-sql-stored-procs.py
echo "  ✓ Stored procedures created"
echo ""

# Configure App Service settings with OpenAI
echo "Step 11: Configuring App Service settings (including GenAI)..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config appsettings set \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "ConnectionStrings__DefaultConnection=$CONNECTION_STRING" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
        "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    --output none

echo "  ✓ App Service settings configured with GenAI"
echo ""

# Build and deploy application
echo "Step 12: Building application..."
cd src/ExpenseManagement
dotnet publish -c Release -o ./publish
echo "  ✓ Application built"
echo ""

echo "Step 13: Creating deployment package..."
cd publish
zip -r ../../../app.zip ./*
cd ../../..
echo "  ✓ Deployment package created (app.zip)"
echo ""

echo "Step 14: Deploying application to Azure..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --src-path ./app.zip \
    --type zip
echo "  ✓ Application deployed"
echo ""

echo "=========================================="
echo "Full Deployment Complete!"
echo "=========================================="
echo ""
echo "Application URL: $WEB_APP_URL/Index"
echo ""
echo "Features enabled:"
echo "  ✓ Expense Management Dashboard"
echo "  ✓ API with Swagger documentation (/swagger)"
echo "  ✓ AI-powered Chat Interface (on Dashboard)"
echo ""
echo "Note: Navigate to /Index to view the expense management dashboard"
echo "      The AI chat assistant is available on the dashboard"
echo ""
echo "To run locally with Azure AD authentication:"
echo "1. Run 'az login' to authenticate"
echo "2. Update appsettings.Development.json with:"
echo "   Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Default;"
echo "3. Run 'dotnet run' from src/ExpenseManagement"
echo ""
