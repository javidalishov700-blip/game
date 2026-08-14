export function haptic(ms: number | number[]): void {
  try {
    navigator.vibrate?.(ms);
  } catch {
    /* ignore */
  }
}

export function hapticInvert(): void {
  haptic(12);
}

export function hapticContagion(): void {
  haptic(18);
}

export function hapticAnnihilate(): void {
  haptic([24, 28, 42]);
}

export function hapticCore(): void {
  haptic(14);
}

export function hapticSnap(): void {
  haptic(22);
}

export function hapticDeath(): void {
  haptic(48);
}
