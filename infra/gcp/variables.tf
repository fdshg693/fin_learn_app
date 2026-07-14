variable "project_id" {
  description = "GCP project ID"
  type        = string
}

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

variable "region" {
  description = "GCP region for all resources"
  type        = string
  default     = "asia-northeast1"
}

variable "cors_allowed_origins" {
  description = "CORS allowed origins for the backend API. Set to the frontend Cloud Run URL after first deploy."
  type        = string
  default     = "*"
}

# ── Backend (Cloud Run) ──────────────────────

variable "backend_cpu" {
  description = "CPU limit for backend Cloud Run service"
  type        = string
  default     = "1"
}

variable "backend_memory" {
  description = "Memory limit for backend Cloud Run service"
  type        = string
  default     = "512Mi"
}

variable "backend_max_instances" {
  description = "Max instance count for backend Cloud Run service"
  type        = number
  default     = 2
}

# ── Frontend (Cloud Run) ─────────────────────

variable "frontend_cpu" {
  description = "CPU limit for frontend Cloud Run service"
  type        = string
  default     = "1"
}

variable "frontend_memory" {
  description = "Memory limit for frontend Cloud Run service"
  type        = string
  default     = "512Mi"
}

variable "frontend_max_instances" {
  description = "Max instance count for frontend Cloud Run service"
  type        = number
  default     = 2
}

# ── Docker image tags ────────────────────────

variable "api_image_tag" {
  description = "Docker image tag for the backend API"
  type        = string
  default     = "latest"
}

variable "web_image_tag" {
  description = "Docker image tag for the frontend web app"
  type        = string
  default     = "latest"
}
