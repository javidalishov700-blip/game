package com.gravitap.entities

import android.graphics.*
import kotlin.math.*

class GravityWell(
    val x: Float,
    val y: Float
) {
    enum class Phase { CHARGE, ACTIVE, FADE, DEAD }

    private val createdAt = System.currentTimeMillis()
    private val chargeEnd = createdAt + Constants.WELL_CHARGE_MS
    private val activeEnd = chargeEnd  + Constants.WELL_ACTIVE_MS
    private val fadeEnd   = activeEnd  + Constants.WELL_FADE_MS

    var phase = Phase.CHARGE
        private set

    /** 0..1 strength used by physics engine */
    var strength = 0f
        private set

    private var lastRippleTime = createdAt
    private val rippleRings = mutableListOf<Float>()   // each entry = current radius

    private val wellPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val ringPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val corePaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val crossPaint = Paint(Paint.ANTI_ALIAS_FLAG)

    init {
        wellPaint.style = Paint.Style.FILL
        ringPaint.style = Paint.Style.STROKE
        ringPaint.strokeWidth = 2.5f
        corePaint.style = Paint.Style.FILL
        corePaint.color = Constants.COL_WELL
        crossPaint.style      = Paint.Style.STROKE
        crossPaint.strokeCap  = Paint.Cap.ROUND
        crossPaint.strokeWidth = 3f
        crossPaint.color = Color.argb(180, 155, 89, 255)
    }

    fun update(dtS: Float) {
        val now = System.currentTimeMillis()
        phase = when {
            now < chargeEnd -> Phase.CHARGE
            now < activeEnd -> Phase.ACTIVE
            now < fadeEnd   -> Phase.FADE
            else            -> Phase.DEAD
        }

        strength = when (phase) {
            Phase.CHARGE -> {
                val t = (now - createdAt).toFloat() / Constants.WELL_CHARGE_MS.toFloat()
                t * t   // quadratic ease-in
            }
            Phase.ACTIVE -> 1f
            Phase.FADE   -> {
                val t = (fadeEnd - now).toFloat() / Constants.WELL_FADE_MS.toFloat()
                t
            }
            Phase.DEAD   -> 0f
        }

        // Spawn ripple rings periodically while active
        if (phase == Phase.ACTIVE && now - lastRippleTime > Constants.RIPPLE_INTERVAL) {
            lastRippleTime = now
            if (rippleRings.size < Constants.RIPPLE_MAX_RINGS) rippleRings.add(0f)
        }

        // Expand ripple rings
        val maxRadius = Constants.WELL_VISUAL_RADIUS * 1.8f
        var ri = 0
        while (ri < rippleRings.size) {
            rippleRings[ri] += Constants.RIPPLE_SPEED * dtS
            if (rippleRings[ri] > maxRadius) rippleRings.removeAt(ri) else ri++
        }
    }

    fun isAttracting() = phase == Phase.ACTIVE || (phase == Phase.CHARGE && strength > 0.2f)
    fun isDead() = phase == Phase.DEAD

    fun draw(canvas: Canvas, shakeX: Float, shakeY: Float) {
        if (phase == Phase.DEAD) return
        val cx = x + shakeX
        val cy = y + shakeY
        val baseRadius = Constants.WELL_VISUAL_RADIUS * strength

        // Outer glow halo
        for (i in 4 downTo 0) {
            wellPaint.color = Color.argb((15 + i * 8).coerceAtMost(60), 155, 89, 255)
            canvas.drawCircle(cx, cy, baseRadius + i * 14f, wellPaint)
        }

        // Expanding ripple rings
        for (r in rippleRings) {
            val alpha = ((1f - r / (Constants.WELL_VISUAL_RADIUS * 1.8f)) * 80 * strength).toInt()
            ringPaint.color = Color.argb(alpha.coerceAtLeast(0), 155, 89, 255)
            canvas.drawCircle(cx, cy, r, ringPaint)
        }

        // Main circle
        ringPaint.color = Color.argb((200 * strength).toInt(), 155, 89, 255)
        ringPaint.strokeWidth = 2.5f
        canvas.drawCircle(cx, cy, baseRadius, ringPaint)

        // Inner ring
        ringPaint.color = Color.argb((130 * strength).toInt(), 155, 89, 255)
        canvas.drawCircle(cx, cy, baseRadius * 0.55f, ringPaint)

        // Core dot
        corePaint.color = Color.argb((240 * strength).toInt(), 155, 89, 255)
        canvas.drawCircle(cx, cy, 10f * strength + 4f, corePaint)

        // Cross-hair lines
        val armLen = baseRadius * 0.38f
        crossPaint.alpha = (180 * strength).toInt()
        canvas.drawLine(cx - armLen, cy, cx + armLen, cy, crossPaint)
        canvas.drawLine(cx, cy - armLen, cx, cy + armLen, crossPaint)
    }
}
