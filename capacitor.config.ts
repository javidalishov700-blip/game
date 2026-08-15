import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.javidalishov.clack",
  appName: "CLACK",
  webDir: "dist",
  backgroundColor: "#05040c",
  android: {
    backgroundColor: "#05040c",
    allowMixedContent: false,
  },
};

export default config;
