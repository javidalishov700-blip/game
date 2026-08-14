export class AudioBus {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private muted = false;

  get isMuted(): boolean {
    return this.muted;
  }

  unlock(): void {
    if (!this.ctx) {
      const Ctx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new Ctx();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : 0.3;
      this.master.connect(this.ctx.destination);
    }
    if (this.ctx.state === "suspended") void this.ctx.resume();
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.master) this.master.gain.value = this.muted ? 0 : 0.3;
    return this.muted;
  }

  invert(): void {
    this.sweep(180, 640, 0.14, "sawtooth", 0.11);
    this.blip(90, 0.1, "triangle", 0.08);
  }

  contagion(): void {
    this.blip(520, 0.08, "square", 0.09);
    this.blip(780, 0.07, "sine", 0.06);
  }

  annihilate(): void {
    this.sweep(140, 40, 0.22, "sawtooth", 0.16);
    this.blip(880, 0.12, "triangle", 0.12);
    this.blip(1320, 0.08, "sine", 0.07);
  }

  core(): void {
    this.blip(660, 0.1, "sine", 0.13);
    this.blip(990, 0.12, "triangle", 0.08);
  }

  snap(): void {
    this.blip(210, 0.07, "square", 0.1);
  }

  detonate(): void {
    this.sweep(300, 70, 0.16, "triangle", 0.12);
  }

  rift(): void {
    this.blip(70, 0.18, "sine", 0.1);
  }

  spawn(): void {
    this.blip(240, 0.08, "sine", 0.05);
  }

  lose(): void {
    this.sweep(220, 55, 0.4, "triangle", 0.16);
    this.blip(80, 0.32, "sine", 0.12);
  }

  private blip(freq: number, dur: number, type: OscillatorType, gain: number): void {
    if (!this.ctx || !this.master || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.62), t + dur);
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(g);
    g.connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.02);
  }

  private sweep(
    from: number,
    to: number,
    dur: number,
    type: OscillatorType,
    gain: number,
  ): void {
    if (!this.ctx || !this.master || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(from, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(30, to), t + dur);
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(g);
    g.connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.03);
  }
}
