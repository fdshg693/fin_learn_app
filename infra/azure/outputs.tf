output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "backend_url" {
  description = "URL of the backend API"
  value       = "https://${azurerm_linux_web_app.backend.default_hostname}"
}

output "frontend_url" {
  description = "URL of the frontend"
  value       = "https://${azurerm_linux_web_app.frontend.default_hostname}"
}

output "backend_app_name" {
  description = "Name of the backend App Service (for az webapp deploy)"
  value       = azurerm_linux_web_app.backend.name
}

output "frontend_app_name" {
  description = "Name of the frontend App Service (for az webapp deploy)"
  value       = azurerm_linux_web_app.frontend.name
}
