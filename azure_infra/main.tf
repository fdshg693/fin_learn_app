terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

provider "azurerm" {
  features {}
}

locals {
  prefix = "${var.project_name}-${var.environment}"
}

# ──────────────────────────────────────────────
# Resource Group
# ──────────────────────────────────────────────

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
}

# ──────────────────────────────────────────────
# App Service Plans
# ──────────────────────────────────────────────

resource "azurerm_service_plan" "backend" {
  name                = "plan-${local.prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.backend_sku
}

resource "azurerm_service_plan" "frontend" {
  name                = "plan-${local.prefix}-web"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.frontend_sku
}

# ──────────────────────────────────────────────
# Backend App Service (.NET 9)
# ──────────────────────────────────────────────

resource "azurerm_linux_web_app" "backend" {
  name                = "${local.prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.backend.id

  site_config {
    application_stack {
      dotnet_version = "9.0"
    }
    always_on = false
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "CORS_ALLOWED_ORIGINS"   = "https://${local.prefix}-web.azurewebsites.net"
  }
}

# ──────────────────────────────────────────────
# Frontend App Service (Node.js 20 LTS)
# ──────────────────────────────────────────────

resource "azurerm_linux_web_app" "frontend" {
  name                = "${local.prefix}-web"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.frontend.id

  site_config {
    application_stack {
      node_version = "20-lts"
    }
    always_on        = false # F1 tier does not support always_on
    app_command_line = "npx react-router-serve ./build/server/index.js"
  }

  app_settings = {
    "SCM_DO_BUILD_DURING_DEPLOYMENT" = "true"
    "PORT"                           = "8080"
  }
}
