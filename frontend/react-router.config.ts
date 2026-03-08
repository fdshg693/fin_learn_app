import type { Config } from "@react-router/dev/config";

export default {
  // SPA mode: データ取得は clientLoader のみのため SSR 不要
  ssr: false,
} satisfies Config;
