import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.javidalishov.wick",
  appName: "WICK",
  webDir: "dist",
  backgroundColor: "#0a0608",
  android: {
    backgroundColor: "#0a0608",
    allowMixedContent: false,
  },
};

export default config;
