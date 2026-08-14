/** Tiny Web Audio synth — no asset files, instant on mobile. */
export class AudioBus {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private holdGain: GainNode | null = null;
  private muted = false;

  get isMuted(): boolean {
    return this.muted;
  }

  get enabled(): boolean {
    return this.ctx !== null && !this.muted;
  }

  unlock(): void {
    if (!this.ctx) {
      const Ctx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext })
          .webkitAudioContext;
      this.ctx = new Ctx();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : 0.32;
      this.master.connect(this.ctx.destination);
      this.patchHoldDrone();
    }
    if (this.ctx.state === "suspended") {
      void this.ctx.resume();
    }
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.master) this.master.gain.value = this.muted ? 0 : 0.32;
    return this.muted;
  }

  setHolding(holding: boolean): void {
    if (!this.ctx || !this.holdGain || this.muted) return;
    const t = this.ctx.currentTime;
    this.holdGain.gain.cancelScheduledValues(t);
    this.holdGain.gain.setTargetAtTime(holding ? 0.045 : 0.0, t, 0.06);
  }

  collect(combo: number): void {
    const notes = [523.25, 587.33, 659.25, 783.99, 880];
    this.blip(notes[Math.min(combo, notes.length - 1)]!, 0.09, "triangle", 0.18);
    this.blip(notes[Math.min(combo, notes.length - 1)]! * 2, 0.05, "sine", 0.08);
  }

  nearMiss(): void {
    this.blip(196, 0.12, "sawtooth", 0.1);
  }

  burn(): void {
    this.noise(0.28, 0.22, 800);
    this.blip(90, 0.35, "sine", 0.28);
  }

  fade(): void {
    this.blip(740, 0.4, "sine", 0.16);
    this.blip(220, 0.5, "triangle", 0.12);
  }

  recapture(): void {
    this.blip(164.81, 0.16, "sine", 0.12);
  }

  ash(): void {
    this.noise(0.18, 0.16, 1400);
    this.blip(110, 0.2, "square", 0.12);
  }

  private patchHoldDrone(): void {
    if (!this.ctx || !this.master) return;
    const oscA = this.ctx.createOscillator();
    const oscB = this.ctx.createOscillator();
    oscA.type = "sine";
    oscB.type = "sine";
    oscA.frequency.value = 55;
    oscB.frequency.value = 82.5;
    this.holdGain = this.ctx.createGain();
    this.holdGain.gain.value = 0;
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 240;
    oscA.connect(this.holdGain);
    oscB.connect(this.holdGain);
    this.holdGain.connect(filter);
    filter.connect(this.master);
    oscA.start();
    oscB.start();
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
