import { Capacitor } from "@capacitor/core";
import { SplashScreen } from "@capacitor/splash-screen";
import { StatusBar } from "@capacitor/status-bar";
import { AudioBus } from "./audio";
import { FlinchGame } from "./game";
import "./style.css";

const canvasEl = document.querySelector("#game");
if (!(canvasEl instanceof HTMLCanvasElement)) {
  throw new Error("Missing #game canvas");
}
const canvas = canvasEl;

const audio = new AudioBus();
const game = new FlinchGame(canvas, audio);

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
  event.preventDefault();
  const p = canvasPoint(event);
  game.tap(p.x, p.y);
});
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

window.addEventListener("keydown", (e) => {
  if (e.code === "KeyM" && !e.repeat) {
    audio.unlock();
    audio.toggleMute();
    return;
  }
  if (e.code === "Space" || e.code === "Enter") {
    e.preventDefault();
    if (!e.repeat) {
      const r = canvas.getBoundingClientRect();
      game.tap(r.width * 0.5, r.height * 0.5);
    }
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
