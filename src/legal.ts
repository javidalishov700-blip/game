export type LegalId = "contact" | "terms" | "about" | "privacy";

export const LEGAL: Record<LegalId, { title: string; html: string }> = {
  contact: {
    title: "CONTACT US",
    html: `
      <p>Questions, feedback, or App Store issues:</p>
      <p><a href="mailto:javidalishov700@gmail.com">javidalishov700@gmail.com</a></p>
      <p>We read every note. Please include your device and OS version if something is broken.</p>
    `,
  },
  about: {
    title: "ABOUT US",
    html: `
      <p>FLINCH is an original one-thumb arcade game by Javid Alishov.</p>
      <p>Six lanes. Spikes fly at a glass core. Tap the right wedge when it turns gold. Let ghosts pass. Some spikes lie and switch lanes.</p>
      <p>No accounts. No ads in 1.0. Built to feel expensive in the first five seconds.</p>
      <p>Version 1.0.0 · com.javidalishov.flinch</p>
    `,
  },
  terms: {
    title: "TERMS OF SERVICE",
    html: `
      <p>Last updated 14 August 2026.</p>
      <p>FLINCH is provided for personal, non-commercial play. You may not copy, resell, or republish the game as your own product.</p>
      <p>The game is provided “as is”, without warranty. Scores are stored only on your device and may be lost if you delete the app.</p>
      <p>We may update these terms when the game updates. Continued use means you accept the current terms.</p>
      <p>Contact: <a href="mailto:javidalishov700@gmail.com">javidalishov700@gmail.com</a></p>
    `,
  },
  privacy: {
    title: "PRIVACY POLICY",
    html: `
      <p>Last updated 14 August 2026.</p>
      <p>FLINCH does not require an account and does not include ads, analytics, or third-party trackers in version 1.0.</p>
      <p>Your best score and settings (sound, BGM, vibration) stay on this device. They never leave the device.</p>
      <p>We do not collect your name, email, location, or advertising ID.</p>
      <p>The game runs offline after install. Questions: <a href="mailto:javidalishov700@gmail.com">javidalishov700@gmail.com</a></p>
    `,
  },
};

export const SHARE_TEXT = "Six lanes. Wait for gold. Don't flinch. Play FLINCH.";
