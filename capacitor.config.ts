import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.javidalishov.popdraw",
  appName: "POPDRAW",
  webDir: "dist",
  backgroundColor: "#f3d5e4",
  android: {
    backgroundColor: "#f3d5e4",
    allowMixedContent: false,
  },
};

export default config;
