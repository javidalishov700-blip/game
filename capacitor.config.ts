import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.javidalishov.flinch",
  appName: "FLINCH",
  webDir: "dist",
  backgroundColor: "#09090b",
  android: {
    backgroundColor: "#09090b",
    allowMixedContent: false,
  },
};

export default config;
