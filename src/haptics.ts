export function haptic(ms: number | number[]): void {
  try {
    navigator.vibrate?.(ms);
  } catch {
    /* ignore */
  }
}

export function hapticKick(): void {
  haptic(11);
}

export function hapticClack(): void {
  haptic(8);
}

export function hapticSplit(): void {
  haptic([12, 18, 22]);
}

export function hapticCore(): void {
  haptic(16);
}

export function hapticDeath(): void {
  haptic(48);
}
