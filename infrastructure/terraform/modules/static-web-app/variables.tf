variable "resource_group_name" { type = string }
variable "location"            { type = string; default = "westeurope" }
variable "environment"         { type = string }

variable "sku_tier" {
  type        = string
  default     = "Standard"
  description = "Free | Standard. Standard required for custom domains + preview environments."
}

variable "custom_hostname" {
  type        = string
  default     = ""
  description = "Custom hostname (e.g. app.rushorder.es). Empty = skip."
}

variable "tags" { type = map(string); default = {} }
