output "storage_account_id"     { value = azurerm_storage_account.this.id }
output "storage_account_name"   { value = azurerm_storage_account.this.name }
output "primary_blob_endpoint"  { value = azurerm_storage_account.this.primary_blob_endpoint }

output "primary_access_key" {
  sensitive = true
  value     = azurerm_storage_account.this.primary_access_key
}

output "cdn_endpoint_hostname" { value = azurerm_cdn_endpoint.product_images.fqdn }
output "cdn_endpoint_url"      { value = "https://${azurerm_cdn_endpoint.product_images.fqdn}" }

output "backup_storage_account_name" { value = azurerm_storage_account.backups.name }
output "backup_storage_account_id"   { value = azurerm_storage_account.backups.id }
