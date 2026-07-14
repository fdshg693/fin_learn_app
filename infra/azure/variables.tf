variable "project_name" {
  description = "Base name for all resources"
  type        = string
  default     = "finlearn"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "japaneast"
}

variable "backend_sku" {
  description = "App Service Plan SKU for backend"
  type        = string
  default     = "B1"
}

variable "frontend_sku" {
  description = "App Service Plan SKU for frontend"
  type        = string
  default     = "B1"
}
