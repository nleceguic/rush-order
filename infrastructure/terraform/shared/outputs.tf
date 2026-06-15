output "resource_group_name"    { value = azurerm_resource_group.this.name }
output "resource_group_id"      { value = azurerm_resource_group.this.id }
output "location"               { value = azurerm_resource_group.this.location }
output "vnet_id"                { value = azurerm_virtual_network.this.id }
output "vnet_name"              { value = azurerm_virtual_network.this.name }
output "app_service_subnet_id"  { value = azurerm_subnet.app_service.id }
output "postgresql_subnet_id"   { value = azurerm_subnet.postgresql.id }
output "redis_subnet_id"        { value = azurerm_subnet.redis.id }
output "private_endpoints_subnet_id" { value = azurerm_subnet.private_endpoints.id }

output "postgresql_private_dns_zone_id" {
  value = azurerm_private_dns_zone.postgresql.id
}
output "redis_private_dns_zone_id" {
  value = azurerm_private_dns_zone.redis.id
}
