variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "app_service_plan_sku" {
  type        = string
  description = "B1 (dev) | P1v3 (staging) | P2v3 (prod)"
}

variable "app_subnet_id" {
  type        = string
  description = "Subnet with Microsoft.Web/serverFarms delegation"
}

variable "key_vault_id"   { type = string }
variable "key_vault_name" { type = string }

variable "application_insights_connection_string" {
  type    = string
  default = ""
}

variable "docker_image"            { type = string }
variable "docker_registry_url"     { type = string }
variable "docker_registry_username"{ type = string }

variable "enable_autoscaling"     { type = bool; default = false }
variable "enable_deployment_slot" { type = bool; default = false }

variable "tags" { type = map(string); default = {} }
