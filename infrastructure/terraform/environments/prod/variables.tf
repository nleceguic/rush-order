variable "subscription_id" { type = string }
variable "tenant_id"       { type = string }
variable "location"        { type = string; default = "northeurope" }
variable "alert_email"     { type = string }

variable "slack_webhook_url"        { type = string; sensitive = true }
variable "pg_admin_password"        { type = string; sensitive = true }
variable "jwt_secret"               { type = string; sensitive = true }
variable "stripe_key"               { type = string; sensitive = true }
variable "sendgrid_key"             { type = string; sensitive = true }
variable "vapid_public_key"         { type = string; sensitive = true }
variable "vapid_private_key"        { type = string; sensitive = true }
variable "docker_registry_url"      { type = string; default = "https://rushorderacr.azurecr.io" }
variable "docker_registry_username" { type = string; default = "rushorderacr" }
variable "docker_registry_password" { type = string; sensitive = true }
variable "docker_image"             { type = string; default = "rushorderacr.azurecr.io/rush-order-api:prod" }
