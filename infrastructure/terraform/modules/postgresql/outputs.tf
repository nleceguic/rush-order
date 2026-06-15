output "server_id"          { value = azurerm_postgresql_flexible_server.this.id }
output "server_name"        { value = azurerm_postgresql_flexible_server.this.name }
output "server_fqdn"        { value = azurerm_postgresql_flexible_server.this.fqdn }
output "database_name"      { value = azurerm_postgresql_flexible_server_database.rushorder.name }
output "administrator_login"{ value = azurerm_postgresql_flexible_server.this.administrator_login }

output "connection_string" {
  sensitive = true
  value     = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Database=rushorder;Username=${var.administrator_login};Password=${var.administrator_password};SSL Mode=Require;"
}

output "replica_fqdn" {
  value = var.enable_read_replica ? azurerm_postgresql_flexible_server.replica[0].fqdn : null
}
