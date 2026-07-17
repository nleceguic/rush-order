variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "environment"         { type = string }

variable "log_retention_days" {
  type        = number
  description = "30 (dev/staging) | 90 (prod)"
}

# Cold-tier archive for compliance (2 years in prod, 0 elsewhere)
variable "log_archive_days" {
  type        = number
  default     = 0
  description = "Days to keep logs in cold storage (0 = disabled)"
}

variable "alert_email"       { type = string }
variable "slack_webhook_url" { type = string; sensitive = true; default = "" }

# Separate email for non-pager warning alerts (defaults to alert_email)
variable "warning_alert_email" {
  type    = string
  default = ""
}

variable "tags" { type = map(string); default = {} }
