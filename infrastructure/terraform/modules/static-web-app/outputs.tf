output "static_site_id"       { value = azurerm_static_site.this.id }
output "default_host_name"    { value = azurerm_static_site.this.default_host_name }
output "app_url"              { value = "https://${azurerm_static_site.this.default_host_name}" }
output "custom_domain_url" {
  value = var.custom_hostname != "" ? "https://${var.custom_hostname}" : null
}

output "api_key" {
  sensitive   = true
  description = "SWA deployment API token — store as GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN"
  value       = azurerm_static_site.this.api_key
}
