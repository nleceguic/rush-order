output "redis_id"       { value = azurerm_redis_cache.this.id }
output "redis_hostname" { value = azurerm_redis_cache.this.hostname }
output "redis_ssl_port" { value = azurerm_redis_cache.this.ssl_port }

output "redis_primary_access_key" {
  sensitive = true
  value     = azurerm_redis_cache.this.primary_access_key
}

output "redis_connection_string" {
  sensitive = true
  value     = "${azurerm_redis_cache.this.hostname}:${azurerm_redis_cache.this.ssl_port},password=${azurerm_redis_cache.this.primary_access_key},ssl=True,abortConnect=False"
}
