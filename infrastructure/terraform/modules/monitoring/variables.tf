variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "log_retention_days" {
  type        = number
  description = "30 (dev/staging) | 90 (prod)"
}

variable "alert_email"       { type = string }
variable "slack_webhook_url" { type = string; sensitive = true; default = "" }

variable "tags" { type = map(string); default = {} }
