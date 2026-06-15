variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "sku_name" {
  type        = string
  description = "Basic (dev) | Standard (staging) | Premium (prod — required for geo-replication)"
}
variable "family" {
  type    = string
  default = "C"
  validation {
    condition     = contains(["C", "P"], var.family)
    error_message = "C for Basic/Standard, P for Premium."
  }
}
variable "capacity" {
  type        = number
  description = "0=250MB | 1=1GB | 2=6GB"
}

variable "enable_geo_replication" {
  type        = bool
  default     = false
  description = "Requires Premium SKU"
}
variable "secondary_location" { type = string; default = "westeurope" }

variable "private_endpoints_subnet_id" { type = string }
variable "private_dns_zone_id"         { type = string }

variable "tags" { type = map(string); default = {} }
