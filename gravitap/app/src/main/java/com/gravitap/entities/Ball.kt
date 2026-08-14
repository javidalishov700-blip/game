package com.gravitap.entities

import android.graphics.*
import kotlin.math.*

class Ball(screenW: Float, screenH: Float) {

    var x = screenW / 2f
    var y = screenH * 0.4f
    var vx = Constants.INITIAL_SPEED * 0.7f
    var vy = -Constants.INITIAL_SPEED * 0.8f

    // Circular trail buffer
    private val trailX = FloatArray(Constants.TRAIL_LENGTH)
    private val trailY = FloatArray(Constants.TRAIL_LENGTH)
    private var trailHead = 0
    private var trailCount = 0

    private val ballPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val glowPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val trailPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val highlightP = Paint(Paint.ANTI_ALIAS_FLAG)

    init {
        ballPaint.style  = Paint.Style.FILL
        ballPaint.color  = Constants.COL_BALL

        glowPaint.style  = Paint.Style.FILL

        trailPaint.style      = Paint.Style.STROKE
        trailPaint.strokeCap  = Paint.Cap.ROUND
        trailPaint.strokeJoin = Paint.Join.ROUND
        trailPaint.strokeWidth = Constants.BALL_RADIUS * 1.6f

        highlightP.style = Paint.Style.FILL
        highlightP.color = Color.argb(170, 255, 255, 255)
    }

    fun update(dtS: Float, wells: List<GravityWell>, screenW: Float, screenH: Float) {
        // Softened Newtonian gravity from each active well
        for (well in wells) {
            if (!well.isAttracting()) continue
            val dx = well.x - x
            val dy = well.y - y
            val s  = Constants.GRAVITY_SOFTENING
            val dist2 = dx * dx + dy * dy + s * s
            val dist  = sqrt(dist2)
            val mag   = (Constants.GRAVITY_STRENGTH * well.strength) / dist2
            val cappedMag = min(mag, 4200f)
            vx += (dx / dist) * cappedMag * dtS
            vy += (dy / dist) * cappedMag * dtS
        }

        // Speed clamping
        val speed = sqrt(vx * vx + vy * vy)
        if (speed > Constants.MAX_BALL_SPEED) {
            val inv = Constants.MAX_BALL_SPEED / speed
            vx *= inv; vy *= inv
        } else if (speed < Constants.MIN_BALL_SPEED && speed > 0.1f) {
            val inv = Constants.MIN_BALL_SPEED / speed
            vx *= inv; vy *= inv
        }

        x += vx * dtS
        y += vy * dtS

        val r = Constants.BALL_RADIUS
        if (x - r < 0)        { x = r;             vx = abs(vx) * Constants.WALL_ELASTICITY }
        if (x + r > screenW)  { x = screenW - r;   vx = -abs(vx) * Constants.WALL_ELASTICITY }
        if (y - r < 0)        { y = r;             vy = abs(vy) * Constants.WALL_ELASTICITY }
        if (y + r > screenH)  { y = screenH - r;   vy = -abs(vy) * Constants.WALL_ELASTICITY }

        // Push trail sample
        trailX[trailHead] = x
        trailY[trailHead] = y
        trailHead = (trailHead + 1) % Constants.TRAIL_LENGTH
        if (trailCount < Constants.TRAIL_LENGTH) trailCount++
    }

    fun draw(canvas: Canvas, shakeX: Float, shakeY: Float) {
        val cx = x + shakeX
        val cy = y + shakeY
        val r  = Constants.BALL_RADIUS

        // Trail: draw from oldest to newest so newest (closest to ball) is thickest/brightest.
        if (trailCount > 1) {
            val tl = Constants.TRAIL_LENGTH
            for (i in 0 until trailCount - 1) {
                val idxFrom = (trailHead - trailCount + i     + tl * 4) % tl
                val idxTo   = (trailHead - trailCount + i + 1 + tl * 4) % tl
                val frac    = (i + 1).toFloat() / trailCount   // 0→1 where 1 = closest to ball
                val alpha   = (frac * 175).toInt()
                trailPaint.color       = Color.argb(alpha, 0, 238, 187)
                trailPaint.strokeWidth = r * 1.8f * frac
                canvas.drawLine(
                    trailX[idxFrom] + shakeX, trailY[idxFrom] + shakeY,
                    trailX[idxTo]   + shakeX, trailY[idxTo]   + shakeY,
                    trailPaint
                )
            }
        }

        // Multi-layer glow
        for (layer in 5 downTo 1) {
            glowPaint.color = Color.argb(18 + layer * 6, 0, 238, 187)
            canvas.drawCircle(cx, cy, r + layer * 9f, glowPaint)
        }

        // Body
        canvas.drawCircle(cx, cy, r, ballPaint)

        // Inner highlight dot
        canvas.drawCircle(cx - r * 0.28f, cy - r * 0.28f, r * 0.35f, highlightP)
    }

    /** Boost current speed by a multiplier (called on portal hit). */
    fun addBoost(amount: Float) {
        val speed = sqrt(vx * vx + vy * vy)
        if (speed < 0.1f) return
        val target = min(speed + amount, Constants.MAX_BALL_SPEED)
        val factor = target / speed
        vx *= factor
        vy *= factor
    }

    fun speed() = sqrt(vx * vx + vy * vy)

    /** Returns a snapshot of current velocity direction angle in radians. */
    fun angle() = atan2(vy, vx)
}
