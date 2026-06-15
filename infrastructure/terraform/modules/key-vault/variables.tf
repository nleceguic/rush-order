variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }
variable "tenant_id"           { type = string }

variable "secrets" {
  type        = map(string)
  sensitive   = true
  default     = {}
  description = "Map of secret-name => secret-value to populate in the vault"
}

variable "tags" { type = map(string); default = {} }
