export const API_BASE_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5088";
export const DEFAULT_TIMEOUT_MS = 8000;
export const ORDERBOOK_PAGE_SIZE = 20;

export const ENTRA_CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID ?? "";
export const ENTRA_TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID ?? "";
export const ENTRA_API_SCOPE = import.meta.env.VITE_ENTRA_API_SCOPE ?? "";
export const ENTRA_REDIRECT_URI =
  import.meta.env.VITE_ENTRA_REDIRECT_URI ?? "http://localhost:5173";
export const ENTRA_AUTHORITY = `https://login.microsoftonline.com/${ENTRA_TENANT_ID}`;
