import { createWriteStream } from "node:fs";
import { mkdir } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { PNG } from "pngjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

const BG = [9, 9, 11];
const IVORY = [244, 240, 230];
const INK = [22, 20, 28];
const GOLD = [240, 199, 94];

function setRgb(png, x, y, rgb) {
  const i = (png.width * y + x) << 2;
  png.data[i] = rgb[0];
  png.data[i + 1] = rgb[1];
  png.data[i + 2] = rgb[2];
  png.data[i + 3] = 255;
}

function diamond(png, cx, cy, r, rgb) {
  const x0 = Math.max(0, Math.floor(cx - r));
  const x1 = Math.min(png.width - 1, Math.ceil(cx + r));
  const y0 = Math.max(0, Math.floor(cy - r));
  const y1 = Math.min(png.height - 1, Math.ceil(cy + r));
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      if (Math.abs(x - cx) + Math.abs(y - cy) <= r) setRgb(png, x, y, rgb);
    }
  }
}

function fill(png, rgb) {
  for (let y = 0; y < png.height; y++) {
    for (let x = 0; x < png.width; x++) setRgb(png, x, y, rgb);
  }
}

function mark(png, size) {
  fill(png, BG);
  const cx = size / 2;
  const cy = size / 2;
  diamond(png, cx, cy, size * 0.38, IVORY);
  diamond(png, cx, cy, size * 0.27, INK);
  diamond(png, cx, cy, size * 0.12, GOLD);
}

async function writePng(path, png) {
  await mkdir(dirname(path), { recursive: true });
  await new Promise((resolve, reject) => {
    png
      .pack()
      .pipe(createWriteStream(path))
      .on("finish", resolve)
      .on("error", reject);
  });
}

const icon = new PNG({ width: 1024, height: 1024 });
mark(icon, 1024);
await writePng(join(root, "resources/icon.png"), icon);

const touch = new PNG({ width: 180, height: 180 });
mark(touch, 180);
await writePng(join(root, "public/apple-touch-icon.png"), touch);

const web = new PNG({ width: 512, height: 512 });
mark(web, 512);
await writePng(join(root, "public/icon-512.png"), web);

const splash = new PNG({ width: 2732, height: 2732 });
fill(splash, BG);
diamond(splash, 1366, 1366, 280, IVORY);
diamond(splash, 1366, 1366, 198, INK);
diamond(splash, 1366, 1366, 88, GOLD);
await writePng(join(root, "resources/splash.png"), splash);

console.log("Wrote resources/icon.png, resources/splash.png, public icons");
