const KEY = "flinch-settings";

export type Settings = {
  sfx: boolean;
  bgm: boolean;
  vibration: boolean;
};

const DEFAULTS: Settings = { sfx: true, bgm: true, vibration: true };

export function loadSettings(): Settings {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return { ...DEFAULTS };
    const parsed = JSON.parse(raw) as Partial<Settings>;
    return {
      sfx: parsed.sfx !== false,
      bgm: parsed.bgm !== false,
      vibration: parsed.vibration !== false,
    };
  } catch {
    return { ...DEFAULTS };
  }
}

export function saveSettings(s: Settings): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* ignore */
  }
}

export function applySettings(
  audio: { apply: (s: Settings) => void },
  s: Settings,
): void {
  saveSettings(s);
  audio.apply(s);
}
