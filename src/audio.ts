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
        (window as unknown as { webkitAudioContext: typeof AudioContext })
          .webkitAudioContext;
      this.ctx = new Ctx();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : 0.28;
      this.master.connect(this.ctx.destination);
    }
    if (this.ctx.state === "suspended") void this.ctx.resume();
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.master) this.master.gain.value = this.muted ? 0 : 0.28;
    return this.muted;
  }

  shot(): void {
    this.blip(520, 0.07, "square", 0.1);
  }

  pop(n: number): void {
    const f = 420 + Math.min(n, 8) * 70;
    this.blip(f, 0.11, "triangle", 0.16);
    this.blip(f * 1.5, 0.08, "sine", 0.08);
  }

  chain(): void {
    this.blip(880, 0.12, "sine", 0.14);
    this.blip(1320, 0.1, "triangle", 0.08);
  }

  swap(): void {
    this.blip(300, 0.06, "sine", 0.08);
  }

  pest(): void {
    this.blip(980, 0.08, "square", 0.1);
  }

  duelStart(): void {
    this.blip(196, 0.18, "sawtooth", 0.1);
  }

  duelWin(): void {
    this.blip(523, 0.1, "triangle", 0.14);
    this.blip(784, 0.14, "sine", 0.12);
  }

  duelLose(): void {
    this.blip(140, 0.22, "triangle", 0.14);
  }

  lose(): void {
    this.blip(180, 0.28, "sine", 0.16);
    this.blip(90, 0.35, "triangle", 0.12);
  }

  drop(): void {
    this.blip(220, 0.05, "sine", 0.05);
  }

  private blip(
    freq: number,
    dur: number,
    type: OscillatorType,
    gain: number,
  ): void {
    if (!this.ctx || !this.master || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.55), t + dur);
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(g);
    g.connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.02);
  }
}
