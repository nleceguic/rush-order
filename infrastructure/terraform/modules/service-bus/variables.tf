variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "sku" {
  type        = string
  description = "Standard (dev/staging) | Premium (prod)"
  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku)
    error_message = "Must be Basic, Standard, or Premium."
  }
}

variable "tags" { type = map(string); default = {} }
