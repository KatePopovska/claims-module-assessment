# Azure infrastructure

Bicep templates that create everything the Claims Module needs inside an **existing resource group**.

```
iac/
  main.bicep                 entry point (targetScope = resourceGroup)
  main.bicepparam            parameter values; secrets come from environment variables
  bicepconfig.json           linter rules (security rules are errors)
  modules/
    monitoring.bicep         Log Analytics workspace + Application Insights
    storage.bicep            Storage account + private claim-documents container
    sql.bicep                Azure SQL logical server, database, auditing
    sqlfirewall.bicep        SQL firewall rules for the web app's outbound IPs
    keyvault.bicep           Key Vault (RBAC) + connection-string secrets + audit log
    staticwebapp.bicep       Static Web App for the Angular frontend
    appservice.bicep         Linux App Service plan + API web app (managed identity) + logs
    roleassignments.bicep    Web app identity -> Key Vault Secrets User, Storage Blob Data Contributor
```

Validated on every pull request and push to `main` by `.github/workflows/iac-ci.yml` (**Validate Bicep**: build, lint, parameter build).

## What gets created

| Resource | Default | Notes |
|---|---|---|
| App Service plan + web app | Linux B1, .NET 9 | Always On (keeps the Hangfire GL/SLA jobs running), HTTPS only, TLS 1.2, FTP disabled, basic-auth publishing credentials (FTP and SCM) disabled, health check on `/api/health`, system-assigned identity. HTTP, console, platform and audit logs go to Log Analytics |
| Static Web App | Free | Frontend hosting; its URL is set as the API's allowed CORS origin |
| Azure SQL server + database | Basic (5 DTU) | Application data and Hangfire tables. **Entra-only authentication** — SQL logins and passwords are disabled; an Entra group or user is the server admin, and the API connects with its managed identity as a least-privileged database user. Firewall allows only the web app's outbound IPs (plus your IP if `CLIENT_IP_ADDRESS` is set) — not the "all Azure services" rule, which would admit every Azure tenant. Server auditing goes to Log Analytics |
| Storage account | Standard LRS, StorageV2 | Private `claim-documents` container, no public blob access, 7-day soft delete for blobs and containers. **Shared-key access disabled** — the API uses its managed identity and signs download links with a user-delegation key; there is no account key in use anywhere. Cross-tenant replication disabled |
| Key Vault | Standard, RBAC | Holds **no secrets** today (the app needs none). Kept for future secrets; the web app already has *Key Vault Secrets User*. Audit events go to Log Analytics |
| Log Analytics + Application Insights | 30-day retention | Receives the diagnostics above; Application Insights wired to the web app via `APPLICATIONINSIGHTS_CONNECTION_STRING` |

Names get a stable 6-character suffix derived from the resource group Id, so globally unique names (SQL server, Key Vault, storage, web app, static site) don't collide and redeployments update the same resources.

API app settings set by the template: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection` (managed-identity connection string with no credentials: `Authentication=Active Directory Managed Identity`), `Storage__Provider=AzureBlob`, `Storage__AzureBlob__ServiceUri` (the blob endpoint), `Cors__AllowedOrigins__0` (the Static Web App URL), and the Application Insights settings.

The deployment takes **no secrets as input**.

## Prerequisites

- Azure CLI 2.60+ (`az version`); it brings its own Bicep. The standalone `bicep` on your PATH isn't used.
- `sqlcmd` (the Go version, `winget install sqlcmd` / `brew install sqlcmd`) for the one-time database grant.
- Signed in to the right subscription: `az login`, then `az account set --subscription <subscription-id>`.
- On the resource group: **Owner**, or **Contributor + User Access Administrator** — the template creates role assignments.
- An **Entra SQL administrator**. A group is recommended (admins can change without redeploying); add yourself to it so you can run the one-time database grant:
  ```bash
  az ad group create --display-name sql-claims-admins --mail-nickname sql-claims-admins
  az ad group member add --group sql-claims-admins --member-id "$(az ad signed-in-user show --query id -o tsv)"
  az ad group show --group sql-claims-admins --query id -o tsv      # -> SQL_ENTRA_ADMIN_OBJECT_ID
  ```
  To use your own account instead: `az ad signed-in-user show --query "{id:id, upn:userPrincipalName}"`, and set `sqlEntraAdminPrincipalType = 'User'` in `main.bicepparam`.

## Deploy

| Variable | Required | Value |
|---|---|---|
| `SQL_ENTRA_ADMIN_OBJECT_ID` | yes | Object id of the Entra group or user |
| `SQL_ENTRA_ADMIN_LOGIN` | yes | Group name (e.g. `sql-claims-admins`) or user principal name |
| `CLIENT_IP_ADDRESS` | no | Your public IP, to add a firewall rule for this machine. Manual use only; the pipelines never set it |

Everything else — regions (`appServiceLocation`, `sqlLocation`, `staticWebAppLocation`), `appServicePlanSkuName` and `sqlEntraAdminPrincipalType` — is set in `main.bicepparam`. The Static Web App is only offered in `eastus2`, `centralus`, `westus2`, `westeurope` and `eastasia`; its content is served from the global edge network regardless.

Bash:

```bash
export SQL_ENTRA_ADMIN_OBJECT_ID='<object-id>'
export SQL_ENTRA_ADMIN_LOGIN='sql-claims-admins'
export CLIENT_IP_ADDRESS="$(curl -s https://api.ipify.org)"

az deployment group what-if --resource-group <your-rg> --template-file iac/main.bicep --parameters iac/main.bicepparam
az deployment group create  --resource-group <your-rg> --template-file iac/main.bicep --parameters iac/main.bicepparam
```

PowerShell:

```powershell
$env:SQL_ENTRA_ADMIN_OBJECT_ID = '<object-id>'
$env:SQL_ENTRA_ADMIN_LOGIN = 'sql-claims-admins'
$env:CLIENT_IP_ADDRESS = (Invoke-RestMethod https://api.ipify.org)

az deployment group what-if --resource-group <your-rg> --template-file iac/main.bicep --parameters iac/main.bicepparam
az deployment group create  --resource-group <your-rg> --template-file iac/main.bicep --parameters iac/main.bicepparam
```

### If a region is refused

`RequestDisallowedByAzure … The selected region is currently not accepting new customers` means the region isn't open to your subscription. The template is fine; pick another region:

- **Static Web App**: change `staticWebAppLocation` in `main.bicepparam` to another of its five regions and re-run `what-if`.
- **App Service**: change `appServiceLocation` in `main.bicepparam`. Also check the App Service plan quota for the new region (`SubscriptionIsOverQuotaForSku`); quotas are per region.
- **Azure SQL** (`ProvisioningDisabled: Provisioning is restricted in this region`) — the service most often restricted, and only detected when the server is created, not by `what-if`. Change `sqlLocation` in `main.bicepparam` and re-run `create`; only the SQL server and database move, everything else stays put. The SQL server name includes a hash of its region, so a new region also gets a new name — necessary because a failed creation leaves the old name reserved for a while (`InvalidResourceLocation … already exists in location …` even though `az sql server list` shows nothing). The API reaches it over the Azure backbone; the extra cross-region latency is a few milliseconds within one geography.
- **Everything else** is created in the resource group's region. To move all of it, add a second `--parameters` after the `.bicepparam` file, e.g. `--parameters iac/main.bicepparam --parameters location=northeurope` — but **only before anything has been created**: existing resources can't change region, so after a partial deployment either use the per-service settings above or delete the resource group and start again.

`what-if` shows what would change without changing anything. The deployment is idempotent: running it again updates the same resources.

Outputs: `apiUrl`, `frontendUrl`, `webAppName`, `webAppPrincipalId`, `staticWebAppName`, `sqlServerName`, `sqlServerFullyQualifiedDomainName`, `sqlDatabaseName`, `keyVaultName`, `storageAccountName`.

## Deployment lifecycle

### One-time bootstrap (manual)

Done once, by an Owner of the resource group. Nothing here runs EF migrations — the first migration happens in `backend-cd.yml`.

**1. Deployment identity and OIDC trust** — PowerShell, signed in with `az login` to the subscription that holds the resource group:

```powershell
$rg     = 'rg-claims-assessment'
$repo   = 'KatePopovska/claims-module-assessment'
$sub    = az account show --query id -o tsv
$tenant = az account show --query tenantId -o tsv
$scope  = "/subscriptions/$sub/resourceGroups/$rg"

# App registration + service principal used by iac-cd.yml and backend-cd.yml
$appId = az ad app create --display-name 'github-claims-cd' --query appId -o tsv
az ad sp create --id $appId
$spId = az ad sp show --id $appId --query id -o tsv

# Trust only GitHub Actions runs of this repository's "production" environment
@{
    name      = 'github-production'
    issuer    = 'https://token.actions.githubusercontent.com'
    subject   = "repo:${repo}:environment:production"
    audiences = @('api://AzureADTokenExchange')
} | ConvertTo-Json | Set-Content -Path github-fic.json -Encoding utf8
az ad app federated-credential create --id $appId --parameters github-fic.json
Remove-Item github-fic.json

# Deploy resources and the API, manage the temporary SQL firewall rule
az role assignment create --assignee-object-id $spId --assignee-principal-type ServicePrincipal --role Contributor --scope $scope

# Create role assignments, restricted to the two roles the template assigns
$roles = '4633458b-17de-408a-b874-0445c86b69e6, ba92f5b4-2d11-453d-a403-e96b0029c9fe'
$condition = "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$roles})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$roles}))"
az role assignment create --assignee-object-id $spId --assignee-principal-type ServicePrincipal `
  --role 'Role Based Access Control Administrator' --scope $scope --condition $condition --condition-version 2.0

# SQL administrator: lets backend-cd.yml run EF migrations
az ad group member add --group sql-claims-admins --member-id $spId

"AZURE_CLIENT_ID=$appId"; "AZURE_TENANT_ID=$tenant"; "AZURE_SUBSCRIPTION_ID=$sub"
```

**2. GitHub environment** — *Settings → Environments → New environment → `production`*:

- *Deployment branches and tags*: **Selected branches** → `main`.
- *Required reviewers* (optional): yourself, to approve each deployment.
- *Environment variables* (none are secrets):

| Variable | Used by | Value |
|---|---|---|
| `AZURE_CLIENT_ID` | iac-cd, backend-cd, frontend-cd | printed by step 1 |
| `AZURE_TENANT_ID` | iac-cd, backend-cd, frontend-cd | printed by step 1 |
| `AZURE_SUBSCRIPTION_ID` | iac-cd, backend-cd, frontend-cd | printed by step 1 |
| `AZURE_RESOURCE_GROUP` | iac-cd, backend-cd, frontend-cd | the resource group name |
| `SQL_ENTRA_ADMIN_OBJECT_ID` | iac-cd | object id of the Entra SQL admin group |
| `SQL_ENTRA_ADMIN_LOGIN` | iac-cd | name of that group |
| `AZURE_WEBAPP_NAME` | backend-cd | deployment output `webAppName` |
| `AZURE_SQL_SERVER_NAME` | backend-cd | deployment output `sqlServerName` (the short name, not the FQDN) |
| `AZURE_SQL_DATABASE_NAME` | backend-cd | deployment output `sqlDatabaseName` |
| `AZURE_STATIC_WEB_APP_NAME` | frontend-cd | deployment output `staticWebAppName` |

The Static Web App deployment token is not stored in GitHub: frontend-cd signs in through OIDC and reads it at deploy time with `az staticwebapp secrets list` (allowed by the identity's Contributor role), masking it in the log.

Regions, SKUs and the admin principal type are not variables: they live in `main.bicepparam`. The three resource names are explicit so the backend deployment always targets exactly these resources, even if more are added to the resource group later; if a name is wrong, the workflow fails at *Resolve configured Azure resources* before touching anything. Update them if the resources are ever recreated under new names.

**3. Database access for the API** — run `iac/scripts/grant-sql-access.sql` once, after the App Service managed identity exists. It creates the database user for the API's identity with `db_datareader`, `db_datawriter` and `db_ddladmin` (Hangfire creates its own tables on first start). Run it as a member of the Entra SQL admin group, from a machine allowed through the SQL firewall — the pipelines don't open the firewall for you here. Use the same names as the GitHub variables:

```powershell
$rg        = 'rg-claims-assessment'
$sqlServer = '<AZURE_SQL_SERVER_NAME>'
$sqlDb     = '<AZURE_SQL_DATABASE_NAME>'
$webApp    = '<AZURE_WEBAPP_NAME>'
$sqlFqdn   = az sql server show --resource-group $rg --name $sqlServer --query fullyQualifiedDomainName -o tsv
$principal = az webapp identity show --resource-group $rg --name $webApp --query principalId -o tsv
$myIp      = Invoke-RestMethod https://api.ipify.org

az sql server firewall-rule create --resource-group $rg --server $sqlServer --name bootstrap-grant --start-ip-address $myIp --end-ip-address $myIp
sqlcmd -S $sqlFqdn -d $sqlDb --authentication-method ActiveDirectoryDefault `
  -v WebAppName=$webApp WebAppPrincipalId=$principal -i iac/scripts/grant-sql-access.sql
az sql server firewall-rule delete --resource-group $rg --server $sqlServer --name bootstrap-grant
```

The script creates the user by the identity's object id, so no Entra directory lookup is needed, and prints the user's roles at the end. It is idempotent. Keep it for recovery: re-run it if the web app (and therefore its managed identity) is ever recreated. The recurring pipelines never run it.

### Recurring deployment (automatic)

| Change pushed to `main` | Workflow | Steps |
|---|---|---|
| `iac/**` | `iac-cd.yml` | OIDC login → `az account show` → Bicep build + lint → what-if → deploy `main.bicep` + `main.bicepparam` |
| `backend/**` | `backend-cd.yml` | restore → Release build → tests → publish → EF migrations bundle → OIDC login → `az account show` → resolve the configured web app / SQL server / database → temporary SQL firewall rule for the runner → apply pending migrations → remove the rule (always) → deploy API → smoke test `/api/health` and `/swagger/v1/swagger.json` |

Both run in the `production` environment and share one concurrency group, so they never deploy at the same time (GitHub doesn't guarantee which runs first: if a backend change needs new infrastructure, merge the IaC change first). Pull requests only run the CI workflows (`backend-ci.yml`, `iac-ci.yml`), which never sign in to Azure. The same steps run for the first and every later deployment; migrations apply only what's pending. No client secrets, publish profiles, SQL passwords or storage keys are used anywhere.

## How the API uses these settings

- **CORS** — the API allows exactly the origins in `Cors:AllowedOrigins`: the Static Web App URL in Azure (`Cors__AllowedOrigins__0`), `http://localhost:4200` in Development (`appsettings.Development.json`).
- **Swagger** — served in every environment at `/swagger`, as the Assessment Brief expects on the public URL.
- **Storage** — with `Storage:AzureBlob:ServiceUri` the API authenticates with its managed identity; `ConnectionString` is only for local emulators such as Azurite. The local-disk fallback (FRS BR-D-03) applies **only in Development**; in any other environment the API refuses to start if Azure storage isn't configured, instead of writing documents to App Service's ephemeral disk.

## Known infrastructure trade-offs

- **SQL firewall and outbound IPs** — the rules use the web app's `possibleOutboundIpAddresses`. Moving the plan to a different pricing tier can change them; re-run the deployment afterwards. The CD pipeline adds a firewall rule for its runner's IP only for the duration of the migration step. Private endpoints with VNet integration would remove public SQL access entirely, at extra cost.
- **Key Vault** — purge protection is off and soft-delete retention is 7 days so the environment can be torn down and recreated. For long-lived production, enable purge protection and 90-day retention.
- **Backups** — SQL uses locally redundant backup storage (cheapest). Use geo-redundant backups and resource locks for real data.
- **Single parameter file** — `environmentName` allows dev/test/prod, but only `main.bicepparam` exists. Add one `.bicepparam` per environment when a second one is needed.

## Cost (rough, pay-as-you-go)

App Service B1 ≈ $13/month, SQL Basic ≈ $5/month, Static Web App Free, Storage/Key Vault/Application Insights a few cents to a few dollars at this volume.

## Tear down

Deleting the resource group removes everything except the Key Vault, which stays soft-deleted for 7 days and blocks reusing its name. To redeploy into a new resource group with the same name within that window:

```bash
az keyvault purge --name <keyVaultName>
```
