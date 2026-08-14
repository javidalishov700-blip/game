import type { Settings } from "./settings";

export class AudioBus {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  private bgmGain: GainNode | null = null;
  private tick: OscillatorNode | null = null;
  private tickGain: GainNode | null = null;
  private bgmTimer: number | null = null;
  private settings: Settings = { sfx: true, bgm: true, vibration: true };
  private bgmOn = false;
  private step = 0;

  apply(s: Settings): void {
    this.settings = s;
    if (this.sfxGain) this.sfxGain.gain.value = s.sfx ? 1 : 0;
    if (this.bgmGain) this.bgmGain.gain.value = s.bgm && this.bgmOn ? 0.16 : 0;
    if (!s.bgm) this.setApproach(false);
  }

  unlock(): void {
    if (!this.ctx) {
      const Ctx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext })
          .webkitAudioContext;
      this.ctx = new Ctx();
      this.master = this.ctx.createGain();
      this.master.gain.value = 0.32;
      this.master.connect(this.ctx.destination);
      this.sfxGain = this.ctx.createGain();
      this.bgmGain = this.ctx.createGain();
      this.sfxGain.connect(this.master);
      this.bgmGain.connect(this.master);
      this.sfxGain.gain.value = this.settings.sfx ? 1 : 0;
      this.bgmGain.gain.value = 0;
      this.patchTick();
    }
    if (this.ctx.state === "suspended") void this.ctx.resume();
  }

  startBgm(): void {
    this.unlock();
    this.bgmOn = true;
    if (this.bgmGain) this.bgmGain.gain.value = this.settings.bgm ? 0.16 : 0;
    if (this.bgmTimer !== null) return;
    this.pulseBgm();
    this.bgmTimer = window.setInterval(() => this.pulseBgm(), 640);
  }

  stopBgm(): void {
    this.bgmOn = false;
    if (this.bgmGain) this.bgmGain.gain.value = 0;
    if (this.bgmTimer !== null) {
      window.clearInterval(this.bgmTimer);
      this.bgmTimer = null;
    }
    this.setApproach(false);
  }

  setApproach(on: boolean, urgency = 0): void {
    if (!this.ctx || !this.tick || !this.tickGain || !this.settings.sfx) return;
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

  ghost(): void {
    this.blip(620, 0.1, "sine", 0.1);
  }

  hurt(): void {
    this.blip(90, 0.18, "triangle", 0.16);
  }

  wave(): void {
    this.blip(330, 0.1, "sine", 0.1);
    this.blip(495, 0.14, "triangle", 0.1);
  }

  shatter(): void {
    this.noise(0.35, 0.22, 900);
    this.blip(70, 0.4, "sine", 0.22);
  }

  tapUi(): void {
    this.blip(380, 0.05, "sine", 0.06);
  }

  vibrate(ms: number): void {
    if (!this.settings.vibration) return;
    try {
      navigator.vibrate?.(ms);
    } catch {
      /* ignore */
    }
  }

  private pulseBgm(): void {
    if (!this.ctx || !this.bgmGain || !this.settings.bgm || !this.bgmOn) return;
    const notes = [110, 138.59, 164.81, 146.83];
    const f = notes[this.step % notes.length]!;
    this.step += 1;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = "sine";
    osc.frequency.value = f;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.22, t + 0.04);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.58);
    osc.connect(g);
    g.connect(this.bgmGain);
    osc.start(t);
    osc.stop(t + 0.6);
  }

  private patchTick(): void {
    if (!this.ctx || !this.sfxGain) return;
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
    filter.connect(this.sfxGain);
    this.tick.start();
  }

  private blip(
    freq: number,
    dur: number,
    type: OscillatorType,
    gain: number,
  ): void {
    if (!this.ctx || !this.sfxGain || !this.settings.sfx) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.5), t + dur);
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(g);
    g.connect(this.sfxGain);
    osc.start(t);
    osc.stop(t + dur + 0.02);
  }

  private noise(dur: number, gain: number, freq: number): void {
    if (!this.ctx || !this.sfxGain || !this.settings.sfx) return;
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
    g.connect(this.sfxGain);
    src.start(t);
  }
}
