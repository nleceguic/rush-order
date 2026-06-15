# Rush Order — Azure Infrastructure (Terraform)

## Structure

```
terraform/
├── environments/
│   ├── dev/        # B1 App Service · Burstable PgSQL · Basic Redis · Standard SB
│   ├── staging/    # P1v3 · GP D2s_v3 PgSQL · Standard C1 Redis · Standard SB
│   └── prod/       # P2v3 autoscale · GP D4s_v3 HA PgSQL · Premium P1 Redis · Premium SB
│                   # + Front Door WAF · blue/green slot · read replica · geo-replication
├── modules/
│   ├── app-service/   App Service Plan + Linux Web App (Docker) + autoscale + slots
│   ├── postgresql/    Flexible Server PgSQL 16 + optional HA + optional read replica
│   ├── redis/         Azure Cache for Redis + private endpoint + optional geo-replication
│   ├── service-bus/   Namespace + 4 queues + restaurant-events topic
│   ├── storage/       Storage Account + 3 containers + CDN
│   ├── monitoring/    Log Analytics + Application Insights + Action Group
│   └── key-vault/     Key Vault (RBAC) + secret population
└── shared/            Resource Group + VNet/Subnets + Private DNS Zones + NSGs
```

## Prerequisites

| Tool | Version |
|---|---|
| Terraform | ≥ 1.7.0 |
| Azure CLI | ≥ 2.60 |
| azurerm provider | ~> 3.110 |

### One-time bootstrap (run once per subscription)

```bash
# Create the remote state storage account
az group create -n rush-order-tfstate-rg -l northeurope
az storage account create \
  -n rushordertfstate \
  -g rush-order-tfstate-rg \
  -l northeurope \
  --sku Standard_LRS \
  --allow-blob-public-access false
az storage container create \
  -n tfstate \
  --account-name rushordertfstate

# Authenticate Terraform
az login
az account set --subscription "<SUBSCRIPTION_ID>"
```

---

## Usage — per environment

All commands are run from the environment directory, e.g. `environments/dev/`.

### 1. `terraform init`

```bash
cd environments/dev

terraform init \
  -backend-config="resource_group_name=rush-order-tfstate-rg" \
  -backend-config="storage_account_name=rushordertfstate" \
  -backend-config="container_name=tfstate" \
  -backend-config="key=dev.terraform.tfstate"
```

Or simply (values already in `main.tf` backend block):

```bash
terraform init
```

### 2. `terraform plan`

```bash
# Minimal — uses terraform.tfvars for non-secret values
terraform plan \
  -var="pg_admin_password=$PG_ADMIN_PASSWORD" \
  -var="jwt_secret=$JWT_SECRET" \
  -out=tfplan
```

For **prod**, all secrets come from environment variables:

```bash
export TF_VAR_pg_admin_password="..."
export TF_VAR_jwt_secret="..."
export TF_VAR_stripe_key="..."
export TF_VAR_sendgrid_key="..."
export TF_VAR_vapid_public_key="..."
export TF_VAR_vapid_private_key="..."
export TF_VAR_docker_registry_password="..."
export TF_VAR_slack_webhook_url="..."

terraform plan -out=tfplan
```

### 3. `terraform apply`

```bash
terraform apply tfplan
```

Or non-interactive (CI):

```bash
terraform apply -auto-approve tfplan
```

---

## Environment differences

| Setting | dev | staging | prod |
|---|---|---|---|
| App Service SKU | B1 | P1v3 | P2v3 |
| Autoscaling | ✗ | ✗ | ✓ min 2 / max 10 / CPU > 70% |
| Deployment slot | ✗ | ✗ | ✓ staging (blue/green) |
| PostgreSQL SKU | B_Standard_B1ms | GP_Standard_D2s_v3 | GP_Standard_D4s_v3 |
| PgSQL HA | Disabled | Disabled | ZoneRedundant |
| PgSQL backup | 7 days | 7 days | 35 days |
| PgSQL read replica | ✗ | ✗ | ✓ West Europe |
| Redis SKU | Basic C0 | Standard C1 | Premium P1 |
| Redis geo-replication | ✗ | ✗ | ✓ West + North Europe |
| Service Bus SKU | Standard | Standard | Premium |
| Log retention | 30 days | 30 days | 90 days |
| Front Door + WAF | ✗ | ✗ | ✓ DefaultRuleSet 2.0 |
| KV purge protection | ✗ | ✗ | ✓ |

---

## Key architectural notes

### No circular dependency on monitoring
Application Insights and Log Analytics are created in the `monitoring` module with no dependency on App Service. The App Service module receives the APPI connection string as input. Metric alerts are defined directly in each environment's `main.tf`, which has access to both `module.monitoring.action_group_id` and `module.app_service.app_service_id`.

### Key Vault references in App Service
Secrets are never stored in plain text in App Service config. App settings use the format:

```
@Microsoft.KeyVault(VaultName=rush-order-prod-kv;SecretName=JwtSecret)
```

The App Service Managed Identity is granted `Key Vault Secrets User` RBAC at the vault scope.

### Redis geo-replication requires Premium SKU
The spec listed Standard C2 for prod but geo-replication is a Premium-only feature. The prod environment uses Premium P1 (equivalent capacity, ~6GB, geo-replication enabled).

### PostgreSQL uses VNet injection (not private endpoint)
PostgreSQL Flexible Server uses `delegated_subnet_id` + `private_dns_zone_id` for private-only access. It is never reachable from the public internet. App Service reaches it via VNet integration.

### Blue/green deployment (prod)
```bash
# Deploy new image to staging slot
az webapp deployment source config-zip \
  -g rush-order-prod-rg -n rush-order-prod-app \
  --slot staging --src app.zip

# Swap slots (zero-downtime)
az webapp deployment slot swap \
  -g rush-order-prod-rg -n rush-order-prod-app \
  --slot staging --target-slot production
```

---

## Destroy

```bash
# Non-prod only — prod has resource group deletion protection
terraform destroy -auto-approve
```

> **Warning:** The prod Key Vault has `purge_protection_enabled = true`. Once applied, the vault and its secrets cannot be permanently deleted for 90 days. Plan accordingly before first prod apply.

---

## CI/CD (GitHub Actions snippet)

```yaml
- name: Terraform Apply
  working-directory: infrastructure/terraform/environments/${{ env.ENVIRONMENT }}
  env:
    ARM_CLIENT_ID:       ${{ secrets.ARM_CLIENT_ID }}
    ARM_CLIENT_SECRET:   ${{ secrets.ARM_CLIENT_SECRET }}
    ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
    ARM_TENANT_ID:       ${{ secrets.ARM_TENANT_ID }}
    TF_VAR_pg_admin_password: ${{ secrets.PG_ADMIN_PASSWORD }}
    TF_VAR_jwt_secret:        ${{ secrets.JWT_SECRET }}
    TF_VAR_stripe_key:        ${{ secrets.STRIPE_KEY }}
  run: |
    terraform init
    terraform apply -auto-approve
```
