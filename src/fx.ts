import { MAX_PARTICLES } from "./const";
import { clamp, rand } from "./math";

export type Particle = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  life: number;
  max: number;
  size: number;
  r: number;
  g: number;
  b: number;
  spark: boolean;
};

export type Floater = {
  x: number;
  y: number;
  text: string;
  life: number;
  color: string;
  scale: number;
};

export type Ring = {
  x: number;
  y: number;
  r: number;
  max: number;
  life: number;
  color: string;
};

export class FxBus {
  particles: Particle[] = [];
  floaters: Floater[] = [];
  rings: Ring[] = [];
  private at = 0;
  shake = 0;
  flash = 0;
  punch = 1;
  chroma = 0;
  slow = 1;

  reset(): void {
    this.particles.length = 0;
    this.floaters.length = 0;
    this.rings.length = 0;
    this.at = 0;
    this.shake = 0;
    this.flash = 0;
    this.punch = 1;
    this.chroma = 0;
    this.slow = 1;
  }

  tick(dt: number): void {
    this.shake = Math.max(0, this.shake - dt * 7.5);
    this.flash = Math.max(0, this.flash - dt * 3.4);
    this.chroma = Math.max(0, this.chroma - dt * 2.8);
    this.punch += (1 - this.punch) * (1 - Math.pow(0.0008, dt));
    this.slow += (1 - this.slow) * (1 - Math.pow(0.0002, dt));
    if (this.slow > 0.98) this.slow = 1;

    for (const p of this.particles) {
      if (p.life <= 0) continue;
      p.life -= dt;
      p.vx *= 0.985;
      p.vy += (p.spark ? 80 : 420) * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
    }
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i]!;
      f.life -= dt;
      f.y -= 38 * dt;
      f.scale += dt * 0.25;
      if (f.life <= 0) this.floaters.splice(i, 1);
    }
    for (let i = this.rings.length - 1; i >= 0; i--) {
      const ring = this.rings[i]!;
      ring.life -= dt;
      ring.r += (ring.max - ring.r) * (1 - Math.pow(0.002, dt));
      if (ring.life <= 0) this.rings.splice(i, 1);
    }
  }

  burst(
    x: number,
    y: number,
    n: number,
    r: number,
    g: number,
    b: number,
    speed: number,
    spark = false,
  ): void {
    for (let i = 0; i < n; i++) {
      const a = rand(0, Math.PI * 2);
      const s = rand(0.2, 1) * speed;
      const next: Particle = {
        x,
        y,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s - (spark ? 80 : 30),
        life: rand(0.22, spark ? 0.7 : 0.55),
        max: 0.55,
        size: rand(spark ? 1.2 : 2.4, spark ? 3.2 : 6.5),
        r,
        g,
        b,
        spark,
      };
      next.max = next.life;
      if (this.particles.length < MAX_PARTICLES) this.particles.push(next);
      else {
        this.particles[this.at] = next;
        this.at = (this.at + 1) % MAX_PARTICLES;
      }
    }
  }

  ring(x: number, y: number, max: number, color: string, life = 0.45): void {
    this.rings.push({ x, y, r: 8, max, life, color });
    if (this.rings.length > 8) this.rings.shift();
  }

  floater(x: number, y: number, text: string, color: string, scale = 1): void {
    this.floaters.push({ x, y, text, life: 0.9, color, scale });
    if (this.floaters.length > 12) this.floaters.shift();
  }

  impact(mag: number): void {
    this.shake = Math.min(1.35, this.shake + mag);
    this.punch = Math.min(1.09, this.punch + mag * 0.05);
    this.flash = Math.min(0.7, this.flash + mag * 0.22);
  }

  hitStop(amount: number): void {
    this.slow = Math.min(this.slow, clamp(1 - amount, 0.12, 1));
    this.chroma = Math.min(1, this.chroma + amount * 1.4);
  }

  draw(ctx: CanvasRenderingContext2D): void {
    for (const ring of this.rings) {
      const u = clamp(ring.life / 0.45, 0, 1);
      ctx.strokeStyle = ring.color;
      ctx.globalAlpha = u * 0.85;
      ctx.lineWidth = 3 * u + 1;
      ctx.beginPath();
      ctx.arc(ring.x, ring.y, ring.r, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      const u = p.life / p.max;
      ctx.fillStyle = `rgba(${p.r | 0},${p.g | 0},${p.b | 0},${u})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size * (p.spark ? 1 : u), 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    for (const f of this.floaters) {
      ctx.globalAlpha = clamp(f.life * 1.7, 0, 1);
      ctx.fillStyle = f.color;
      ctx.font = `800 ${Math.round(15 * f.scale)}px system-ui, sans-serif`;
      ctx.fillText(f.text, f.x, f.y);
    }
    ctx.globalAlpha = 1;
    ctx.textBaseline = "alphabetic";
  }
}
