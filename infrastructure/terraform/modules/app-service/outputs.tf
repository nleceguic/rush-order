output "app_service_id"       { value = azurerm_linux_web_app.this.id }
output "app_service_name"     { value = azurerm_linux_web_app.this.name }
output "app_service_hostname" { value = azurerm_linux_web_app.this.default_hostname }
output "app_service_url"      { value = "https://${azurerm_linux_web_app.this.default_hostname}" }

output "managed_identity_principal_id" {
  value = azurerm_linux_web_app.this.identity[0].principal_id
}

output "staging_slot_hostname" {
  value = var.enable_deployment_slot ? azurerm_linux_web_app_slot.staging[0].default_hostname : null
}
output "staging_slot_url" {
  value = var.enable_deployment_slot ? "https://${azurerm_linux_web_app_slot.staging[0].default_hostname}" : null
}
