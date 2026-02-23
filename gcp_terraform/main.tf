terraform {
  required_version = ">= 1.5.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

locals {
  prefix = "${var.project_name}-${var.environment}"
}

# ──────────────────────────────────────────────
# Enable required APIs
# ──────────────────────────────────────────────

resource "google_project_service" "run" {
  service            = "run.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "artifactregistry" {
  service            = "artifactregistry.googleapis.com"
  disable_on_destroy = false
}

# ──────────────────────────────────────────────
# Artifact Registry (container images)
# ──────────────────────────────────────────────

resource "google_artifact_registry_repository" "main" {
  location      = var.region
  repository_id = "${local.prefix}-repo"
  format        = "DOCKER"

  depends_on = [google_project_service.artifactregistry]
}

# ──────────────────────────────────────────────
# Backend Cloud Run Service (.NET 9)
# ──────────────────────────────────────────────

resource "google_cloud_run_v2_service" "backend" {
  name                = "${local.prefix}-api"
  location            = var.region
  deletion_protection = false

  template {
    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.main.repository_id}/api:${var.api_image_tag}"

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "CORS_ALLOWED_ORIGINS"
        value = var.cors_allowed_origins
      }

      resources {
        limits = {
          cpu    = var.backend_cpu
          memory = var.backend_memory
        }
      }
    }

    scaling {
      min_instance_count = 0
      max_instance_count = var.backend_max_instances
    }
  }

  depends_on = [google_project_service.run]
}

# ──────────────────────────────────────────────
# Frontend Cloud Run Service (Node.js 20)
# ──────────────────────────────────────────────

resource "google_cloud_run_v2_service" "frontend" {
  name                = "${local.prefix}-web"
  location            = var.region
  deletion_protection = false

  template {
    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.main.repository_id}/web:${var.web_image_tag}"

      ports {
        container_port = 8080
      }

      env {
        name  = "PORT"
        value = "8080"
      }

      env {
        name  = "API_BASE_URL"
        value = google_cloud_run_v2_service.backend.uri
      }

      startup_probe {
        http_get {
          path = "/"
        }
      }

      resources {
        limits = {
          cpu    = var.frontend_cpu
          memory = var.frontend_memory
        }
      }
    }

    scaling {
      min_instance_count = 0
      max_instance_count = var.frontend_max_instances
    }
  }

  depends_on = [google_project_service.run]
}

# ──────────────────────────────────────────────
# IAM — Allow unauthenticated access (public)
# ──────────────────────────────────────────────

resource "google_cloud_run_v2_service_iam_member" "backend_public" {
  name     = google_cloud_run_v2_service.backend.name
  location = google_cloud_run_v2_service.backend.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

resource "google_cloud_run_v2_service_iam_member" "frontend_public" {
  name     = google_cloud_run_v2_service.frontend.name
  location = google_cloud_run_v2_service.frontend.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}
