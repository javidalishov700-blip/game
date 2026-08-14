export class AudioBus {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private muted = false;
  private tick: OscillatorNode | null = null;
  private tickGain: GainNode | null = null;

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
      this.master.gain.value = this.muted ? 0 : 0.3;
      this.master.connect(this.ctx.destination);
      this.patchTick();
    }
    if (this.ctx.state === "suspended") void this.ctx.resume();
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.master) this.master.gain.value = this.muted ? 0 : 0.3;
    return this.muted;
  }

  setApproach(on: boolean, urgency = 0): void {
    if (!this.ctx || !this.tick || !this.tickGain || this.muted) return;
    const t = this.ctx.currentTime;
    this.tick.frequency.setTargetAtTime(70 + urgency * 220, t, 0.08);
    this.tickGain.gain.setTargetAtTime(on ? 0.03 + urgency * 0.04 : 0, t, 0.06);
  }

  perfect(mult: number): void {
    const f = 420 + Math.min(mult, 8) * 55;
    this.blip(f, 0.12, "triangle", 0.2);
    this.blip(f * 2, 0.08, "sine", 0.08);
  }

  good(): void {
    this.blip(260, 0.08, "sine", 0.1);
  }

  flinch(): void {
    this.blip(140, 0.1, "sawtooth", 0.07);
  }

  shatter(): void {
    this.noise(0.35, 0.22, 900);
    this.blip(70, 0.4, "sine", 0.22);
  }

  private patchTick(): void {
    if (!this.ctx || !this.master) return;
    this.tick = this.ctx.createOscillator();
    this.tick.type = "sine";
    this.tick.frequency.value = 70;
    this.tickGain = this.ctx.createGain();
    this.tickGain.gain.value = 0;
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 320;
    this.tick.connect(this.tickGain);
    this.tickGain.connect(filter);
    filter.connect(this.master);
    this.tick.start();
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
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.5), t + dur);
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(g);
    g.connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.02);
  }

  private noise(dur: number, gain: number, freq: number): void {
    if (!this.ctx || !this.master || this.muted) return;
    const samples = Math.floor(this.ctx.sampleRate * dur);
    const buffer = this.ctx.createBuffer(1, samples, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < samples; i++) data[i] = Math.random() * 2 - 1;
    const src = this.ctx.createBufferSource();
    src.buffer = buffer;
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = freq;
    const g = this.ctx.createGain();
    const t = this.ctx.currentTime;
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(filter);
    filter.connect(g);
    g.connect(this.master);
    src.start(t);
  }
}
