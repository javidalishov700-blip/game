package com.gravitap.entities

import android.graphics.*
import kotlin.math.*

class Portal(
    private val colorIndex: Int,
    private val screenW: Float,
    private val screenH: Float,
    ballX: Float,
    ballY: Float,
    private val level: Int
) {
    val color = Constants.PORTAL_COLORS[colorIndex % Constants.PORTAL_COLORS.size]

    var x: Float = 0f
    var y: Float = 0f
    private var vx = 0f
    private var vy = 0f

    private val createdAt  = System.currentTimeMillis()
    private val lifetime   = computeLifetime()
    private var expired    = false

    // Pulsing animation
    private var pulsePhase = 0f
    private val rotPhase   = (Math.random() * Math.PI * 2).toFloat()

    // Portal surface area scales from 0→1 on spawn (pop-in animation)
    private var spawnProgress = 0f

    private val hexPath   = Path()
    private val hexPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val glowPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val innerPaint= Paint(Paint.ANTI_ALIAS_FLAG)
    private val timerPaint= Paint(Paint.ANTI_ALIAS_FLAG)

    init {
        // Spawn at random position that is clear of the ball and screen edges
        val margin = Constants.PORTAL_SPAWN_MARGIN
        val clearSq = Constants.PORTAL_SPAWN_CLEAR * Constants.PORTAL_SPAWN_CLEAR
        var px = margin + Math.random().toFloat() * (screenW - margin * 2)
        var py = margin + Math.random().toFloat() * (screenH - margin * 2)
        var tries = 0
        while (tries < 40 && (px - ballX).pow(2) + (py - ballY).pow(2) < clearSq) {
            px = margin + Math.random().toFloat() * (screenW - margin * 2)
            py = margin + Math.random().toFloat() * (screenH - margin * 2)
            tries++
        }
        x = px; y = py

        // Movement for portal (starts at PORTAL_MOVE_START level)
        if (level >= Constants.PORTAL_MOVE_START) {
            val angle = Math.random().toFloat() * 2f * PI.toFloat()
            val speed = min(
                (level - Constants.PORTAL_MOVE_START) * Constants.PORTAL_MOVE_PER_LVL,
                Constants.PORTAL_MAX_SPEED
            )
            vx = cos(angle) * speed
            vy = sin(angle) * speed
        }

        hexPaint.style  = Paint.Style.STROKE
        hexPaint.strokeWidth = 3.5f
        glowPaint.style = Paint.Style.FILL
        innerPaint.style = Paint.Style.STROKE
        innerPaint.strokeWidth = 1.8f

        timerPaint.style     = Paint.Style.STROKE
        timerPaint.strokeCap = Paint.Cap.ROUND
        timerPaint.strokeWidth = 6f
        timerPaint.color      = Color.argb(200, 255, 80, 80)
    }

    private fun computeLifetime(): Long {
        if (level < Constants.PORTAL_LIFE_LEVEL) return Long.MAX_VALUE
        val reduction = (level - Constants.PORTAL_LIFE_LEVEL) * Constants.PORTAL_LIFE_DECAY
        return maxOf(Constants.PORTAL_LIFETIME_BASE - reduction, Constants.PORTAL_MIN_LIFETIME)
    }

    fun update(dtS: Float) {
        pulsePhase += dtS * (2f * PI.toFloat() / (Constants.PORTAL_PULSE_PERIOD / 1000f))

        // Pop-in animation
        if (spawnProgress < 1f) {
            spawnProgress = min(spawnProgress + dtS * 4f, 1f)
        }

        // Movement
        if (vx != 0f || vy != 0f) {
            x += vx * dtS; y += vy * dtS
            val r = Constants.PORTAL_RADIUS
            val m = Constants.PORTAL_SPAWN_MARGIN * 0.5f
            if (x - r < m)           { x = r + m;           vx =  abs(vx) }
            if (x + r > screenW - m) { x = screenW - r - m; vx = -abs(vx) }
            if (y - r < m)           { y = r + m;           vy =  abs(vy) }
            if (y + r > screenH - m) { y = screenH - r - m; vy = -abs(vy) }
        }

        // Lifetime expiry check
        if (lifetime != Long.MAX_VALUE) {
            val elapsed = System.currentTimeMillis() - createdAt
            if (elapsed >= lifetime) expired = true
        }
    }

    /** Returns true when the ball center is within the hit radius. */
    fun checkHit(bx: Float, by: Float): Boolean {
        if (expired) return false
        val dx = bx - x; val dy = by - y
        val hr = Constants.PORTAL_HIT_RADIUS * spawnProgress
        return dx * dx + dy * dy <= hr * hr
    }

    fun isExpired() = expired
    fun lifetimeFraction(): Float {
        if (lifetime == Long.MAX_VALUE) return 1f
        val elapsed = System.currentTimeMillis() - createdAt
        return (1f - elapsed.toFloat() / lifetime).coerceIn(0f, 1f)
    }

    fun draw(canvas: Canvas, shakeX: Float, shakeY: Float, globalTime: Long) {
        if (expired) return
        val cx = x + shakeX
        val cy = y + shakeY
        val r  = Constants.PORTAL_RADIUS * spawnProgress
        val pulse = 1f + sin(pulsePhase) * 0.12f
        val rp = r * pulse

        val alpha = (spawnProgress * 255).toInt()
        val cr = Color.red(color); val cg = Color.green(color); val cb = Color.blue(color)

        // Outer glow layers
        for (i in 5 downTo 0) {
            glowPaint.color = Color.argb((12 + i * 8) * alpha / 255, cr, cg, cb)
            canvas.drawCircle(cx, cy, rp + i * 11f, glowPaint)
        }

        // Hexagon outline (rotates slowly)
        hexPaint.color = Color.argb(alpha, cr, cg, cb)
        val hexAngle = (globalTime.toFloat() / 4200f * rotPhase)
        drawHexagon(canvas, cx, cy, rp * 1.28f, hexAngle, hexPaint)

        // Inner spinning hexagon (counter-rotation)
        innerPaint.color = Color.argb((alpha * 0.55f).toInt(), cr, cg, cb)
        drawHexagon(canvas, cx, cy, rp * 0.75f, -hexAngle * 1.5f, innerPaint)

        // Filled center with alpha
        glowPaint.color = Color.argb((alpha * 0.22f).toInt(), cr, cg, cb)
        canvas.drawCircle(cx, cy, rp * 0.65f, glowPaint)

        // Timer arc (only when lifetime is finite)
        if (lifetime != Long.MAX_VALUE) {
            val frac = lifetimeFraction()
            val urgencyAlpha = if (frac < 0.3f) (255 * (1f - frac / 0.3f) * 0.85f + 80).toInt() else 100
            timerPaint.color = Color.argb(urgencyAlpha, 255, 80, 80)
            val sweepAngle = 360f * frac
            canvas.drawArc(
                cx - rp * 1.55f, cy - rp * 1.55f, cx + rp * 1.55f, cy + rp * 1.55f,
                -90f, sweepAngle, false, timerPaint
            )
        }
    }

    private fun drawHexagon(canvas: Canvas, cx: Float, cy: Float, r: Float, rotAngle: Float, paint: Paint) {
        hexPath.reset()
        for (i in 0..5) {
            val angle = (PI / 3.0 * i + rotAngle).toFloat()
            val px = cx + r * cos(angle)
            val py = cy + r * sin(angle)
            if (i == 0) hexPath.moveTo(px, py) else hexPath.lineTo(px, py)
        }
        hexPath.close()
        canvas.drawPath(hexPath, paint)
    }
}
