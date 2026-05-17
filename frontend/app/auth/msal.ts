import { PublicClientApplication, type Configuration } from "@azure/msal-browser";
import {
  ENTRA_CLIENT_ID,
  ENTRA_AUTHORITY,
  ENTRA_REDIRECT_URI,
  ENTRA_API_SCOPE,
} from "~/config";

const configuration: Configuration = {
  auth: {
    clientId: ENTRA_CLIENT_ID,
    authority: ENTRA_AUTHORITY,
    redirectUri: ENTRA_REDIRECT_URI,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
};

export const loginRequest = { scopes: ["openid", "profile"] };
export const apiRequest = { scopes: [ENTRA_API_SCOPE] };

let instance: PublicClientApplication | null = null;
let initPromise: Promise<PublicClientApplication> | null = null;

/** SSR 中は null。ブラウザでは未生成なら生成して同一インスタンスを返す。 */
export function getMsalInstance(): PublicClientApplication | null {
  if (typeof window === "undefined") return null;
  if (!instance) {
    instance = new PublicClientApplication(configuration);
  }
  return instance;
}

/**
 * MSAL v3/v4 は明示 initialize() が必須。初期化＋リダイレクト応答処理＋
 * アクティブアカウント設定を一度だけ行い、結果を使い回す。
 */
export function initializeMsal(): Promise<PublicClientApplication> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("MSAL is browser-only"));
  }
  if (!initPromise) {
    const msal = getMsalInstance()!;
    initPromise = msal
      .initialize()
      .then(() => msal.handleRedirectPromise())
      .then((result) => {
        if (result?.account) {
          msal.setActiveAccount(result.account);
        } else if (!msal.getActiveAccount()) {
          const accounts = msal.getAllAccounts();
          if (accounts.length > 0) {
            msal.setActiveAccount(accounts[0]);
          }
        }
        return msal;
      });
  }
  return initPromise;
}
