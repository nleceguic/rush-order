variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "sku_name" {
  type        = string
  description = "B_Standard_B1ms | GP_Standard_D2s_v3 | GP_Standard_D4s_v3"
}

variable "postgres_version"      { type = string; default = "16" }
variable "backup_retention_days" { type = number; default = 7 }
variable "storage_mb"            { type = number; default = 32768 }

variable "high_availability_mode" {
  type    = string
  default = "Disabled"
  validation {
    condition     = contains(["Disabled", "ZoneRedundant", "SameZone"], var.high_availability_mode)
    error_message = "Must be Disabled, ZoneRedundant, or SameZone."
  }
}

variable "enable_read_replica" { type = bool;   default = false }
variable "replica_location"    { type = string; default = "westeurope" }

variable "delegated_subnet_id" { type = string }
variable "private_dns_zone_id" { type = string }

variable "administrator_login"    { type = string; default = "rushorderadmin" }
variable "administrator_password" { type = string; sensitive = true }

variable "tags" { type = map(string); default = {} }
