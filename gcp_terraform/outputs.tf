output "backend_url" {
  description = "URL of the backend API (Cloud Run)"
  value       = google_cloud_run_v2_service.backend.uri
}

output "frontend_url" {
  description = "URL of the frontend (Cloud Run)"
  value       = google_cloud_run_v2_service.frontend.uri
}

output "backend_service_name" {
  description = "Name of the backend Cloud Run service"
  value       = google_cloud_run_v2_service.backend.name
}

output "frontend_service_name" {
  description = "Name of the frontend Cloud Run service"
  value       = google_cloud_run_v2_service.frontend.name
}

output "artifact_registry_repository" {
  description = "Artifact Registry repository path for docker push"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.main.repository_id}"
}
