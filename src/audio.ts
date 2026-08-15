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

  kick(): void {
    this.blip(140, 0.08, "sine", 0.12);
    this.blip(90, 0.1, "triangle", 0.08);
  }

  clack(energy: number): void {
    const f = 180 + Math.min(420, energy * 1.6);
    this.blip(f, 0.05, "square", 0.07);
  }

  split(): void {
    this.blip(720, 0.09, "triangle", 0.11);
    this.blip(1100, 0.07, "sine", 0.06);
  }

  core(): void {
    this.blip(660, 0.1, "sine", 0.13);
    this.blip(990, 0.12, "triangle", 0.08);
  }

  spawn(): void {
    this.blip(260, 0.08, "sine", 0.05);
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
