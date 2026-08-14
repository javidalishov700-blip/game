import { Capacitor } from "@capacitor/core";
import { SplashScreen } from "@capacitor/splash-screen";
import { StatusBar } from "@capacitor/status-bar";
import { AudioBus } from "./audio";
import { FlinchGame, type Hud } from "./game";
import { LEGAL, SHARE_TEXT, type LegalId } from "./legal";
import { applySettings, loadSettings, type Settings } from "./settings";
import "./style.css";

const canvasEl = document.querySelector("#game");
if (!(canvasEl instanceof HTMLCanvasElement)) {
  throw new Error("Missing #game canvas");
}
const canvas = canvasEl;

const hudEl = must("#hud");
const homeBest = must("#home-best");
const overScore = must("#over-score");
const overBest = must("#over-best");
const hudScore = must("#hud-score");
const hudCombo = must("#hud-combo");
const hudLives = must("#hud-lives");
const hudWave = must("#hud-wave");
const toastEl = must("#toast");
const legalTitle = must("#legal-title");
const legalBody = must("#legal-body");

const screens = {
  home: must("#screen-home"),
  pause: must("#screen-pause"),
  over: must("#screen-over"),
  settings: must("#screen-settings"),
  legal: must("#screen-legal"),
};

type Ui = "home" | "play" | "pause" | "over" | "settings" | "legal";
let ui: Ui = "home";
let settingsBack: Ui = "home";
let legalBack: Ui = "settings";
let settings: Settings = loadSettings();
let lastHud: Hud | null = null;
let toastTimer = 0;

const audio = new AudioBus();
const game = new FlinchGame(canvas, audio, {
  onMode: (mode) => {
    if (mode === "over") show("over");
    if (mode === "play" && ui !== "settings") show("play");
  },
  onHud: (hud) => {
    lastHud = hud;
    paintHud(hud);
  },
});

audio.apply(settings);
paintToggles();
paintHud(game.getHud());

function must(sel: string): HTMLElement {
  const el = document.querySelector(sel);
  if (!(el instanceof HTMLElement)) throw new Error(`Missing ${sel}`);
  return el;
}

function show(next: Ui): void {
  ui = next;
  hudEl.classList.toggle("hidden", next !== "play");
  hudEl.setAttribute("aria-hidden", next === "play" ? "false" : "true");
  for (const [name, el] of Object.entries(screens)) {
    el.classList.toggle("hidden", name !== next);
  }
}

function paintHud(hud: Hud): void {
  homeBest.textContent = String(hud.best);
  overScore.textContent = String(hud.score);
  overBest.textContent = String(hud.best);
  hudScore.textContent = String(hud.score);
  hudCombo.textContent = `${hud.combo}x`;
  hudCombo.classList.toggle("is-hot", hud.streak > 0);
  hudLives.textContent = Array.from({ length: 3 }, (_, i) => (i < hud.lives ? "♥" : "♡")).join(" ");
  hudWave.textContent = `WAVE ${hud.wave}  ${hud.title}`;
}

function paintToggles(): void {
  must("#sw-sfx").classList.toggle("on", settings.sfx);
  must("#sw-bgm").classList.toggle("on", settings.bgm);
  must("#sw-vibration").classList.toggle("on", settings.vibration);
}

function toast(text: string): void {
  toastEl.textContent = text;
  toastEl.classList.remove("hidden");
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => toastEl.classList.add("hidden"), 1600);
}

async function share(score?: number): Promise<void> {
  const text = score != null ? `I scored ${score} on FLINCH. ${SHARE_TEXT}` : SHARE_TEXT;
  try {
    if (navigator.share) {
      await navigator.share({ title: "FLINCH", text });
      return;
    }
  } catch {
    /* user cancelled or share failed — fall through */
  }
  try {
    await navigator.clipboard.writeText(text);
    toast("COPIED");
  } catch {
    toast(text);
  }
}

function openSettings(from: Ui): void {
  settingsBack = from;
  show("settings");
}

function openLegal(id: LegalId): void {
  legalBack = "settings";
  const page = LEGAL[id];
  legalTitle.textContent = page.title;
  legalBody.innerHTML = page.html;
  show("legal");
}

function fit(): void {
  const vv = window.visualViewport;
  const w = Math.max(1, Math.round(vv?.width ?? window.innerWidth));
  const h = Math.max(1, Math.round(vv?.height ?? window.innerHeight));
  const dpr = Math.min(window.devicePixelRatio || 1, 2.5);
  canvas.style.width = `${w}px`;
  canvas.style.height = `${h}px`;
  game.resize(w, h, dpr);
}

fit();
window.addEventListener("resize", fit);
window.visualViewport?.addEventListener("resize", fit);

function canvasPoint(event: PointerEvent): { x: number; y: number } {
  const r = canvas.getBoundingClientRect();
  return { x: event.clientX - r.left, y: event.clientY - r.top };
}

canvas.addEventListener("pointerdown", (event) => {
  if (ui !== "play") return;
  event.preventDefault();
  audio.unlock();
  const p = canvasPoint(event);
  game.tap(p.x, p.y);
});
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

must("#btn-play").addEventListener("click", () => {
  audio.unlock();
  audio.tapUi();
  game.startRun();
  show("play");
});
must("#btn-settings").addEventListener("click", () => {
  audio.unlock();
  audio.tapUi();
  openSettings("home");
});
must("#btn-pause").addEventListener("click", () => {
  audio.tapUi();
  game.pause();
  show("pause");
});
must("#btn-resume").addEventListener("click", () => {
  audio.tapUi();
  game.resume();
  show("play");
});
must("#btn-replay").addEventListener("click", () => {
  audio.unlock();
  audio.tapUi();
  game.replay();
  show("play");
});
must("#btn-home").addEventListener("click", () => {
  audio.tapUi();
  game.goHome();
  show("home");
});
must("#btn-pause-settings").addEventListener("click", () => {
  audio.tapUi();
  openSettings("pause");
});
must("#btn-over-replay").addEventListener("click", () => {
  audio.unlock();
  audio.tapUi();
  game.replay();
  show("play");
});
must("#btn-over-home").addEventListener("click", () => {
  audio.tapUi();
  game.goHome();
  show("home");
});
must("#btn-share").addEventListener("click", () => {
  audio.tapUi();
  void share(lastHud?.score ?? 0);
});
must("#btn-settings-share").addEventListener("click", () => {
  audio.tapUi();
  void share();
});
must("#btn-settings-back").addEventListener("click", () => {
  audio.tapUi();
  if (settingsBack === "pause") {
    show("pause");
    return;
  }
  if (settingsBack === "play") {
    game.resume();
    show("play");
    return;
  }
  show("home");
});
must("#btn-legal-back").addEventListener("click", () => {
  audio.tapUi();
  show(legalBack);
});

document.querySelectorAll<HTMLElement>("[data-toggle]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const key = btn.dataset.toggle as keyof Settings | undefined;
    if (!key) return;
    settings = { ...settings, [key]: !settings[key] };
    applySettings(audio, settings);
    paintToggles();
    audio.unlock();
    audio.tapUi();
    if (key === "bgm") {
      if (settings.bgm && (game.getMode() === "play" || game.getMode() === "pause")) {
        audio.startBgm();
      } else if (!settings.bgm) {
        audio.stopBgm();
      }
    }
  });
});

document.querySelectorAll<HTMLElement>("[data-legal]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const id = btn.dataset.legal as LegalId | undefined;
    if (!id || !(id in LEGAL)) return;
    audio.tapUi();
    openLegal(id);
  });
});

window.addEventListener("keydown", (e) => {
  if (e.code === "Escape") {
    if (ui === "play") {
      game.pause();
      show("pause");
    } else if (ui === "pause") {
      game.resume();
      show("play");
    }
    return;
  }
  if (ui !== "play") return;
  const laneKeys: Record<string, number> = {
    Digit1: 0,
    Digit2: 1,
    Digit3: 2,
    Digit4: 3,
    Digit5: 4,
    Digit6: 5,
  };
  const lane = laneKeys[e.code];
  if (lane != null && !e.repeat) {
    e.preventDefault();
    game.tapLaneIndex(lane);
  }
});

let raf = 0;
const loop = (t: number): void => {
  game.tick(t);
  raf = requestAnimationFrame(loop);
};
raf = requestAnimationFrame(loop);

document.addEventListener("visibilitychange", () => {
  if (document.hidden) {
    cancelAnimationFrame(raf);
    if (game.getMode() === "play" && ui === "play") {
      game.pause();
      show("pause");
    }
  } else {
    game.skipClock();
    raf = requestAnimationFrame(loop);
  }
});

async function bootNative(): Promise<void> {
  if (!Capacitor.isNativePlatform()) return;
  try {
    await StatusBar.setOverlaysWebView({ overlay: true });
    await StatusBar.hide();
  } catch {
    /* web / missing plugin */
  }
  try {
    await SplashScreen.hide();
  } catch {
    /* web / missing plugin */
  }
}

void bootNative();
