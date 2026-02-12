#!/bin/bash
set -e

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║           Scaffold — Pre-Provision Setup             ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# Check if app registration is already configured
EXISTING_CLIENT_ID=$(azd env get-value AZURE_CLIENT_ID 2>/dev/null || echo "")

if [ -n "$EXISTING_CLIENT_ID" ]; then
    echo "✅ App Registration already configured (Client ID: $EXISTING_CLIENT_ID)"
    echo "   Skipping app registration setup."
    echo ""
    exit 0
fi

echo "Scaffold requires an Entra ID (Azure AD) App Registration for authentication."
echo ""
echo "Do you have an existing App Registration to use?"
read -p "[y/N]: " HAS_APP_REG

if [[ "$HAS_APP_REG" =~ ^[Yy]$ ]]; then
    echo ""
    read -p "Enter the App Registration Client ID: " CLIENT_ID
    read -p "Enter the Tenant ID: " TENANT_ID
    
    if [ -z "$CLIENT_ID" ] || [ -z "$TENANT_ID" ]; then
        echo "❌ Client ID and Tenant ID are required."
        exit 1
    fi
    
    azd env set AZURE_CLIENT_ID "$CLIENT_ID"
    azd env set AZURE_TENANT_ID "$TENANT_ID"
    
    echo ""
    echo "✅ App Registration configured."
    echo "   Client ID: $CLIENT_ID"
    echo "   Tenant ID: $TENANT_ID"
    echo ""
    echo "⚠️  Ensure the following is configured on your App Registration:"
    echo "   • Authentication → Add platform → Single-page application"
    echo "   • Redirect URIs will be updated after deployment"
    echo "   • API Permissions → Microsoft Graph → User.Read (delegated)"
    echo ""
    exit 0
fi

# Create a new App Registration
echo ""
echo "Creating a new App Registration..."
echo ""

# Get environment name and tenant info
ENV_NAME=$(azd env get-value AZURE_ENV_NAME 2>/dev/null || echo "scaffold")
TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

echo "  Subscription: $SUBSCRIPTION_NAME"
echo "  Tenant ID:    $TENANT_ID"
echo ""

APP_NAME="scaffold-${ENV_NAME}"

# Create the app registration with SPA platform
echo "  Creating app registration: $APP_NAME ..."

CLIENT_ID=$(az ad app create \
    --display-name "$APP_NAME" \
    --sign-in-audience "AzureADMyOrg" \
    --query appId -o tsv)

echo "  ✅ App Registration created"
echo "     Client ID: $CLIENT_ID"

# Create service principal
echo "  Creating service principal..."
az ad sp create --id "$CLIENT_ID" > /dev/null 2>&1 || true
echo "  ✅ Service principal created"

# Add Microsoft Graph User.Read permission (delegated)
# Microsoft Graph App ID: 00000003-0000-0000-c000-000000000000
# User.Read permission ID: e1fe6dd8-ba31-4d61-89e7-88639da4683d
echo "  Adding API permissions (Microsoft Graph → User.Read)..."
az ad app permission add \
    --id "$CLIENT_ID" \
    --api "00000003-0000-0000-c000-000000000000" \
    --api-permissions "e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope" \
    > /dev/null 2>&1

echo "  ✅ API permissions added"

# Configure SPA platform with temporary redirect URI (will be updated in postprovision)
echo "  Configuring SPA authentication platform..."
az ad app update \
    --id "$CLIENT_ID" \
    --spa-redirect-uris "http://localhost:3000" \
    > /dev/null 2>&1
echo "  ✅ SPA platform configured"

# Expose an API with the application ID URI
echo "  Configuring API scope..."
API_URI="api://${CLIENT_ID}"
az ad app update \
    --id "$CLIENT_ID" \
    --identifier-uris "$API_URI" \
    > /dev/null 2>&1
echo "  ✅ API scope configured"

# Store values in azd environment
azd env set AZURE_CLIENT_ID "$CLIENT_ID"
azd env set AZURE_TENANT_ID "$TENANT_ID"
azd env set SCAFFOLD_APP_NAME "$APP_NAME"

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║           App Registration Created                   ║"
echo "╠══════════════════════════════════════════════════════╣"
echo "║  App Name:   $APP_NAME"
echo "║  Client ID:  $CLIENT_ID"
echo "║  Tenant ID:  $TENANT_ID"
echo "║  API URI:    $API_URI"
echo "╚══════════════════════════════════════════════════════╝"
echo ""
echo "┌──────────────────────────────────────────────────────┐"
echo "│  ⚠️  ACTION REQUIRED (after deployment):             │"
echo "│                                                      │"
echo "│  An admin must grant consent for API permissions:    │"
echo "│                                                      │"
echo "│  1. Go to Azure Portal → Entra ID → App registrations│"
echo "│  2. Find '$APP_NAME'                                 │"
echo "│  3. API permissions → Grant admin consent            │"
echo "│                                                      │"
echo "│  Or run:                                             │"
echo "│  az ad app permission admin-consent --id $CLIENT_ID  │"
echo "└──────────────────────────────────────────────────────┘"
echo ""
