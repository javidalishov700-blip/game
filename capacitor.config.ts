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
  ios: {
    backgroundColor: "#09090b",
    contentInset: "never",
    preferredContentMode: "mobile",
    scheme: "FLINCH",
  },
  plugins: {
    SplashScreen: {
      launchShowDuration: 400,
      launchAutoHide: true,
      backgroundColor: "#09090b",
      showSpinner: false,
    },
    StatusBar: {
      style: "DARK",
      backgroundColor: "#09090b",
    },
  },
};

export default config;
