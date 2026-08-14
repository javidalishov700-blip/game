import { AudioBus } from "./audio";
import { PopDrawGame } from "./game";
import "./style.css";

const canvasEl = document.querySelector("#game");
if (!(canvasEl instanceof HTMLCanvasElement)) {
  throw new Error("Missing #game canvas");
}
const canvas = canvasEl;

const audio = new AudioBus();
const game = new PopDrawGame(canvas, audio);

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
canvas.addEventListener("pointermove", (event) => {
  const p = canvasPoint(event);
  game.hover(p.x, p.y);
});
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

window.addEventListener("keydown", (e) => {
  if (e.code === "KeyM" && !e.repeat) {
    audio.unlock();
    audio.toggleMute();
    return;
  }
  if (e.code === "KeyS" || e.code === "ShiftLeft" || e.code === "ShiftRight") {
    e.preventDefault();
    game.swapAmmo();
    return;
  }
  const colKeys: Record<string, number> = {
    Digit1: 0,
    Digit2: 1,
    Digit3: 2,
    Digit4: 3,
    Digit5: 4,
    Numpad1: 0,
    Numpad2: 1,
    Numpad3: 2,
    Numpad4: 3,
    Numpad5: 4,
  };
  if (e.code in colKeys && !e.repeat) {
    e.preventDefault();
    const col = colKeys[e.code]!;
    const x = (col + 0.5) / 5 * canvas.getBoundingClientRect().width;
    const y = canvas.getBoundingClientRect().height * 0.55;
    game.hover(x, y);
    game.tap(x, y);
  }
  if (e.code === "Space" || e.code === "Enter") {
    e.preventDefault();
    if (!e.repeat) {
      const r = canvas.getBoundingClientRect();
      game.tap(r.width * 0.5, r.height * 0.55);
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
  if (document.hidden) cancelAnimationFrame(raf);
  else {
    game.skipClock();
    raf = requestAnimationFrame(loop);
  }
});
