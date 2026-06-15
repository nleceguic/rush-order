variable "environment" { type = string }
variable "location"    { type = string }

variable "vnet_address_space"              { type = list(string); default = ["10.0.0.0/16"] }
variable "app_service_subnet_prefix"       { type = string;       default = "10.0.1.0/24" }
variable "postgresql_subnet_prefix"        { type = string;       default = "10.0.2.0/24" }
variable "redis_subnet_prefix"             { type = string;       default = "10.0.3.0/24" }
variable "private_endpoints_subnet_prefix" { type = string;       default = "10.0.4.0/24" }

variable "tags" { type = map(string); default = {} }
