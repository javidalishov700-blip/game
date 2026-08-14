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

const onDown = (event: Event): void => {
  event.preventDefault();
  game.setHolding(true);
};
const onUp = (event: Event): void => {
  event.preventDefault();
  game.setHolding(false);
};

canvas.addEventListener("pointerdown", onDown);
window.addEventListener("pointerup", onUp);
window.addEventListener("pointercancel", onUp);
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

window.addEventListener("keydown", (e) => {
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
    game.setHolding(false);
  } else {
    game.skipClock();
    raf = requestAnimationFrame(loop);
  }
});
