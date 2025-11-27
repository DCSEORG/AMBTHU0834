# Azure Services Architecture Diagram

This diagram shows the Azure services deployed by this solution and how they connect to each other.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                           Resource Group (UKSOUTH)                                │
│                                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────────┐ │
│  │                         User Assigned Managed Identity                       │ │
│  │                    (mid-appmodassist-<unique-suffix>)                       │ │
│  └─────────────────────┬───────────────────────┬───────────────────────────────┘ │
│                        │                       │                                  │
│         ┌──────────────┴──────────────┐       │                                  │
│         ▼                             │       ▼                                  │
│  ┌─────────────────┐           ┌──────┴───────────────────────────────────────┐ │
│  │   App Service   │           │               Azure SQL Server               │ │
│  │    (S1 SKU)     │           │           (Entra ID Auth Only)               │ │
│  │                 │           │                                               │ │
│  │  ┌───────────┐  │           │  ┌────────────────────────────────────────┐  │ │
│  │  │   Web     │  │           │  │           Northwind Database           │  │ │
│  │  │   App     │◄─┼───────────┼──│    - Expenses Table                    │  │ │
│  │  │  (.NET 8) │  │   SQL     │  │    - Users Table                       │  │ │
│  │  │           │  │Connection │  │    - Categories Table                  │  │ │
│  │  │  - API    │  │(Managed   │  │    - Status Table                      │  │ │
│  │  │  - Pages  │  │ Identity) │  │    - Stored Procedures                 │  │ │
│  │  │  - Chat   │  │           │  └────────────────────────────────────────┘  │ │
│  │  └───────────┘  │           │                                               │ │
│  └────────┬────────┘           └───────────────────────────────────────────────┘ │
│           │                                                                       │
│           │ (When GenAI is deployed)                                             │
│           │                                                                       │
│           │  ┌─────────────────────────────────────────────────────────────────┐ │
│           │  │                GenAI Resources (SWEDENCENTRAL)                  │ │
│           │  │                                                                  │ │
│           │  │  ┌────────────────────┐    ┌──────────────────────────────────┐│ │
│           │  │  │  Azure OpenAI      │    │      Azure AI Search            ││ │
│           └──┼──│  (S0 SKU)          │    │      (Basic SKU)                 ││ │
│              │  │                    │    │                                  ││ │
│        Azure │  │  - GPT-4o Model    │    │  - RAG Context Index             ││ │
│        OpenAI│  │  - Capacity: 8     │    │  - Semantic Search               ││ │
│              │  │  - Function Calling│    │                                  ││ │
│              │  └────────────────────┘    └──────────────────────────────────┘│ │
│              │                                                                  │ │
│              └──────────────────────────────────────────────────────────────────┘ │
│                                                                                   │
└───────────────────────────────────────────────────────────────────────────────────┘

                              ▲
                              │ HTTPS
                              │
┌─────────────────────────────┴─────────────────────────────────────────────────────┐
│                                   Users                                            │
│                                                                                    │
│    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│    │  Dashboard   │    │  Expenses    │    │  Approvals   │    │  Chat UI     │  │
│    │    Page      │    │    Page      │    │    Page      │    │   (AI)       │  │
│    └──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘  │
│                                                                                    │
│    ┌────────────────────────────────────────────────────────────────────────────┐ │
│    │                           REST API (/api/*)                                │ │
│    │    - GET /api/expenses         - GET /api/categories                       │ │
│    │    - POST /api/expenses        - GET /api/statuses                        │ │
│    │    - POST /api/expenses/{id}/approve                                       │ │
│    │    - POST /api/expenses/{id}/reject                                        │ │
│    │    - POST /api/chat            - GET /api/dashboard/stats                  │ │
│    └────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                    │
└────────────────────────────────────────────────────────────────────────────────────┘


## Data Flow

1. **User Request** → App Service Web App
2. **Web App** → Uses Managed Identity to authenticate with Azure SQL
3. **Web App** → Calls Stored Procedures for all database operations
4. **Chat UI** → Sends messages to /api/chat endpoint
5. **Chat Service** → Uses Managed Identity to authenticate with Azure OpenAI
6. **Azure OpenAI** → Uses function calling to query database via API
7. **Response** → Formatted and returned to user

## Security

- **No passwords stored** - All authentication uses Managed Identity
- **Entra ID Only** - SQL Server enforces Azure AD authentication only
- **HTTPS** - All traffic encrypted in transit
- **Network Security** - Firewall rules restrict SQL access

## Deployment Scripts

| Script | Description |
|--------|-------------|
| `deploy.sh` | Deploys App Service + SQL Database (no GenAI) |
| `deploy-with-chat.sh` | Deploys everything including Azure OpenAI + AI Search |

## URLs

- **Application**: `https://<app-name>.azurewebsites.net/Index`
- **API Docs**: `https://<app-name>.azurewebsites.net/swagger`
- **Chat UI**: Embedded in Dashboard or `/chatui/index.html`
