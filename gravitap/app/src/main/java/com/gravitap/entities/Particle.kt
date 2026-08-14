package com.gravitap.entities

import kotlin.math.*

data class Particle(
    var x: Float,
    var y: Float,
    var vx: Float,
    var vy: Float,
    var size: Float,
    val maxSize: Float,
    var life: Float,           // 1.0 = fresh, 0.0 = dead
    val decay: Float,          // life units per second
    val r: Int,
    val g: Int,
    val b: Int,
    var gravity: Float = 0f    // optional downward gravity (px/s²)
)

/** Floating score/combo label that drifts upward. */
data class FloatLabel(
    val text: String,
    var x: Float,
    var y: Float,
    var life: Float,           // 1.0 → 0.0
    val decay: Float,
    var scale: Float = 0f,     // grows 0→1 then holds
    val isCombo: Boolean = false
)
