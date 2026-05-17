import { useEffect, useState } from "react";
import {
  MsalProvider,
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import type { IPublicClientApplication } from "@azure/msal-browser";
import { initializeMsal, loginRequest } from "~/auth/msal";

function Loading() {
  return (
    <main className="flex items-center justify-center min-h-screen">
      <p className="text-gray-500 text-lg animate-pulse">読み込み中...</p>
    </main>
  );
}

function SignInScreen() {
  const { instance } = useMsal();
  return (
    <main className="flex items-center justify-center min-h-screen">
      <div className="text-center space-y-6">
        <h1 className="text-3xl font-bold">株売買シミュレーター</h1>
        <p className="text-gray-500">続けるにはサインインしてください</p>
        <button
          onClick={() => instance.loginRedirect(loginRequest)}
          className="bg-blue-600 hover:bg-blue-700 text-white font-bold py-3 px-8 rounded-lg text-lg"
        >
          サインイン
        </button>
      </div>
    </main>
  );
}

function AccountHeader() {
  const { instance, accounts } = useMsal();
  const name = accounts[0]?.name ?? accounts[0]?.username ?? "";
  return (
    <div className="flex items-center justify-end gap-3 bg-gray-100 px-4 py-2 text-sm">
      <span>{name}</span>
      <button
        onClick={() => instance.logoutRedirect()}
        className="text-blue-600 hover:underline"
      >
        サインアウト
      </button>
    </div>
  );
}

export function AuthGate({ children }: { children: React.ReactNode }) {
  const [instance, setInstance] = useState<IPublicClientApplication | null>(null);

  useEffect(() => {
    let mounted = true;
    initializeMsal()
      .then((msal) => {
        if (mounted) setInstance(msal);
      })
      .catch(() => {
        /* SSR 拒否やブラウザ未対応時はローディングのまま */
      });
    return () => {
      mounted = false;
    };
  }, []);

  if (!instance) {
    return <Loading />;
  }

  return (
    <MsalProvider instance={instance}>
      <UnauthenticatedTemplate>
        <SignInScreen />
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <AccountHeader />
        {children}
      </AuthenticatedTemplate>
    </MsalProvider>
  );
}
