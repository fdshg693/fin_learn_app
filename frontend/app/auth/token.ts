import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { getMsalInstance, apiRequest } from "~/auth/msal";

/**
 * アクセストークン文字列を返す。
 * - アクティブアカウントが無い → acquireTokenRedirect（ページ遷移。Promise は解決しない）
 * - silent 成功 → accessToken を返す
 * - InteractionRequiredAuthError → acquireTokenRedirect（同上）
 * いずれのリダイレクト経路でも、遷移しなかった場合の防御として例外を投げる。
 */
export async function getAccessToken(): Promise<string> {
  const msal = getMsalInstance();
  if (!msal) {
    throw new Error("認証が初期化されていません。");
  }

  const account = msal.getActiveAccount();
  if (!account) {
    await msal.acquireTokenRedirect({ scopes: apiRequest.scopes });
    throw new Error("認証へリダイレクトしています。");
  }

  try {
    const result = await msal.acquireTokenSilent({
      scopes: apiRequest.scopes,
      account,
    });
    return result.accessToken;
  } catch (e) {
    if (e instanceof InteractionRequiredAuthError) {
      await msal.acquireTokenRedirect({ scopes: apiRequest.scopes });
      throw new Error("認証へリダイレクトしています。");
    }
    throw e;
  }
}
