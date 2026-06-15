variable "resource_group_name"      { type = string }
variable "location"                  { type = string }
variable "environment"               { type = string }
variable "account_replication_type"  { type = string; default = "LRS" }
variable "tags"                      { type = map(string); default = {} }
