#!/bin/bash
set -e

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║          Scaffold — Post-Provision Setup             ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# Run EF Core database migration
echo "Running database migrations..."
CONNECTION_STRING=$(azd env get-value SERVICE_API_CONNECTION_STRING 2>/dev/null || \
                    azd env get-value SQL_CONNECTION_STRING 2>/dev/null || echo "")

if [ -z "$CONNECTION_STRING" ]; then
    # Try building connection string from individual components
    SQL_FQDN=$(azd env get-value SQL_SERVER_FQDN 2>/dev/null || echo "")
    SQL_DB=$(azd env get-value SQL_DATABASE_NAME 2>/dev/null || echo "")
    if [ -n "$SQL_FQDN" ] && [ -n "$SQL_DB" ]; then
        CONNECTION_STRING="Server=tcp:${SQL_FQDN},1433;Database=${SQL_DB};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;"
    fi
fi

if [ -n "$CONNECTION_STRING" ]; then
    # Install EF tool if not present
    dotnet tool restore 2>/dev/null || dotnet tool install --global dotnet-ef 2>/dev/null || true

    # Run migrations from the Infrastructure project (where DbContext lives)
    dotnet ef database update \
        --project src/Scaffold.Infrastructure \
        --startup-project src/Scaffold.Api \
        --connection "$CONNECTION_STRING" \
        --verbose

    if [ $? -eq 0 ]; then
        echo "  ✅ Database migrations applied successfully"
    else
        echo "  ❌ Database migration failed!"
        exit 1
    fi
else
    echo "  ⚠️  Could not determine connection string. Skipping database migration."
    echo "     Run manually: dotnet ef database update --project src/Scaffold.Infrastructure --startup-project src/Scaffold.Api"
fi

echo ""

# Get deployed resource URLs from azd environment
WEB_URL=$(azd env get-value SERVICE_WEB_ENDPOINT_URL 2>/dev/null || echo "")
API_URL=$(azd env get-value SERVICE_API_ENDPOINT_URL 2>/dev/null || echo "")
CLIENT_ID=$(azd env get-value AZURE_CLIENT_ID 2>/dev/null || echo "")

# Fallback: try to get from Bicep outputs
if [ -z "$WEB_URL" ]; then
    WEB_HOSTNAME=$(azd env get-value STATIC_WEB_APP_HOSTNAME 2>/dev/null || echo "")
    if [ -n "$WEB_HOSTNAME" ]; then
        WEB_URL="https://${WEB_HOSTNAME}"
    fi
fi

if [ -z "$API_URL" ]; then
    API_FQDN=$(azd env get-value CONTAINER_APP_FQDN 2>/dev/null || echo "")
    if [ -n "$API_FQDN" ]; then
        API_URL="https://${API_FQDN}"
    fi
fi

# Update App Registration redirect URIs with actual deployment URLs
if [ -n "$CLIENT_ID" ] && [ -n "$WEB_URL" ]; then
    echo "Updating App Registration redirect URIs..."
    echo "  Web URL: $WEB_URL"
    
    # Set SPA redirect URIs — include both production URL and localhost for dev
    az ad app update \
        --id "$CLIENT_ID" \
        --spa-redirect-uris "$WEB_URL" "${WEB_URL}/" "http://localhost:3000" "http://localhost:5173" \
        > /dev/null 2>&1
    
    echo "  ✅ Redirect URIs updated"
    echo ""
else
    echo "⚠️  Could not determine deployment URLs. You may need to manually"
    echo "   update the App Registration redirect URIs."
    echo ""
fi

# Update API audience if needed
if [ -n "$CLIENT_ID" ]; then
    API_URI="api://${CLIENT_ID}"
    echo "  API Audience: $API_URI"
    echo ""
fi

# Output deployment summary
echo "╔══════════════════════════════════════════════════════╗"
echo "║              Deployment Complete! 🚀                 ║"
echo "╠══════════════════════════════════════════════════════╣"
if [ -n "$WEB_URL" ]; then
echo "║  Web App:     $WEB_URL"
fi
if [ -n "$API_URL" ]; then
echo "║  API:         $API_URL"
fi
echo "║  Client ID:   ${CLIENT_ID:-Not configured}"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

if [ -n "$WEB_URL" ]; then
    echo "Open $WEB_URL in your browser to get started."
    echo ""
fi

# Remind about admin consent if app was just created
APP_NAME=$(azd env get-value SCAFFOLD_APP_NAME 2>/dev/null || echo "")
if [ -n "$APP_NAME" ]; then
    echo "┌──────────────────────────────────────────────────────┐"
    echo "│  📋 REMINDER: Grant admin consent for API permissions │"
    echo "│                                                      │"
    echo "│  Run:                                                │"
    echo "│  az ad app permission admin-consent --id $CLIENT_ID  │"
    echo "└──────────────────────────────────────────────────────┘"
    echo ""
fi
