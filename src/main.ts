import { AudioBus } from "./audio";
import { WickGame } from "./game";
import "./style.css";

const canvasEl = document.querySelector("#game");
if (!(canvasEl instanceof HTMLCanvasElement)) {
  throw new Error("Missing #game canvas");
}
const canvas = canvasEl;

const audio = new AudioBus();
const game = new WickGame(canvas, audio);

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

let holdPointer: number | null = null;

canvas.addEventListener("pointerdown", (event) => {
  event.preventDefault();
  const p = canvasPoint(event);
  if (game.tapHud(p.x, p.y)) return;
  if (holdPointer !== null) return;
  holdPointer = event.pointerId;
  game.setHolding(true);
});

const releasePointer = (event: PointerEvent): void => {
  if (holdPointer !== null && event.pointerId !== holdPointer) return;
  holdPointer = null;
  game.setHolding(false);
};

window.addEventListener("pointerup", releasePointer);
window.addEventListener("pointercancel", releasePointer);
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

window.addEventListener("keydown", (e) => {
  if (e.code === "KeyM" && !e.repeat) {
    audio.unlock();
    audio.toggleMute();
    return;
  }
  if (e.code === "Space" || e.code === "Enter") {
    e.preventDefault();
    if (!e.repeat) game.setHolding(true);
  }
});
window.addEventListener("keyup", (e) => {
  if (e.code === "Space" || e.code === "Enter") {
    e.preventDefault();
    game.setHolding(false);
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
    holdPointer = null;
    game.setHolding(false);
  } else {
    game.skipClock();
    raf = requestAnimationFrame(loop);
  }
});
