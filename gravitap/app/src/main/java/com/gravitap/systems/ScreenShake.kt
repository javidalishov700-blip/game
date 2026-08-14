package com.gravitap.systems

import com.gravitap.entities.Constants
import kotlin.random.Random

class ScreenShake {

    var offsetX = 0f; private set
    var offsetY = 0f; private set
    private var intensity = 0f
    private val rng = Random.Default

    fun trigger(amount: Float) {
        intensity = maxOf(intensity, amount)
    }

    fun update() {
        if (intensity < 0.5f) {
            intensity = 0f; offsetX = 0f; offsetY = 0f; return
        }
        offsetX = (rng.nextFloat() * 2f - 1f) * intensity
        offsetY = (rng.nextFloat() * 2f - 1f) * intensity
        intensity *= Constants.SHAKE_DECAY
    }
}
