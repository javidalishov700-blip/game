package com.gravitap.systems

import android.graphics.*
import com.gravitap.entities.Constants
import com.gravitap.entities.FloatLabel
import com.gravitap.entities.Particle
import kotlin.math.*
import kotlin.random.Random

class ParticleSystem {

    private val particles  = ArrayList<Particle>(256)
    private val labels     = ArrayList<FloatLabel>(32)
    private val rng        = Random.Default

    private val particlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.FILL }
    private val labelPaint    = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        typeface = Typeface.DEFAULT_BOLD
        textAlign = Paint.Align.CENTER
    }

    /** Burst of particles simulating a portal collection explosion. */
    fun explode(cx: Float, cy: Float, color: Int, combo: Int) {
        val cr = Color.red(color); val cg = Color.green(color); val cb = Color.blue(color)
        val count = Constants.EXPLODE_COUNT + combo * 4

        for (i in 0 until count) {
            val angle    = rng.nextFloat() * 2f * PI.toFloat()
            val speed    = Constants.PARTICLE_SPEED_BASE + rng.nextFloat() * Constants.PARTICLE_SPEED_RAND
            val size     = Constants.PARTICLE_SIZE_BASE  + rng.nextFloat() * Constants.PARTICLE_SIZE_RAND
            val life     = Constants.PARTICLE_LIFE_BASE  + rng.nextFloat() * Constants.PARTICLE_LIFE_RAND
            val decay    = 1f / life
            particles.add(Particle(
                x = cx, y = cy,
                vx = cos(angle) * speed,
                vy = sin(angle) * speed,
                size = size, maxSize = size,
                life = 1f, decay = decay,
                r = cr, g = cg, b = cb,
                gravity = 90f
            ))
        }

        // Bright white sparks for extra "juice"
        for (i in 0 until 8) {
            val angle = rng.nextFloat() * 2f * PI.toFloat()
            val speed = 700f + rng.nextFloat() * 500f
            particles.add(Particle(
                x = cx, y = cy,
                vx = cos(angle) * speed,
                vy = sin(angle) * speed,
                size = 4f, maxSize = 4f,
                life = 1f, decay = 2.0f,
                r = 255, g = 255, b = 255,
                gravity = 0f
            ))
        }
    }

    /** Spawn a floating score/combo label at the given position. */
    fun spawnLabel(text: String, cx: Float, cy: Float, isCombo: Boolean = false) {
        val life  = Constants.COMBO_FLOAT_LIFE
        val decay = 1f / life
        labels.add(FloatLabel(text, cx, cy, life = 1f, decay = decay, scale = 0f, isCombo = isCombo))
    }

    /** Small ring burst when gravity well is first tapped. */
    fun wellBurst(cx: Float, cy: Float) {
        for (i in 0 until 12) {
            val angle = (2f * PI.toFloat() / 12) * i
            val speed = 180f + rng.nextFloat() * 80f
            particles.add(Particle(
                x = cx, y = cy,
                vx = cos(angle) * speed,
                vy = sin(angle) * speed,
                size = 3f, maxSize = 3f,
                life = 1f, decay = 3f,
                r = 155, g = 89, b = 255
            ))
        }
    }

    fun update(dtS: Float) {
        val pIter = particles.iterator()
        while (pIter.hasNext()) {
            val p = pIter.next()
            p.life -= p.decay * dtS
            if (p.life <= 0f) { pIter.remove(); continue }
            p.vx *= (1f - 2.5f * dtS)   // air drag
            p.vy *= (1f - 2.5f * dtS)
            p.vy += p.gravity * dtS
            p.x += p.vx * dtS
            p.y += p.vy * dtS
            p.size = p.maxSize * p.life.pow(0.5f)
        }

        val lIter = labels.iterator()
        while (lIter.hasNext()) {
            val l = lIter.next()
            l.life -= l.decay * dtS
            if (l.life <= 0f) { lIter.remove(); continue }
            l.y -= Constants.COMBO_FLOAT_RISE * dtS
            l.scale = min(l.scale + dtS * 8f, 1f)
        }
    }

    fun draw(canvas: Canvas, shakeX: Float, shakeY: Float) {
        for (p in particles) {
            val alpha = (p.life * 255).toInt().coerceIn(0, 255)
            particlePaint.color = Color.argb(alpha, p.r, p.g, p.b)
            canvas.drawCircle(p.x + shakeX, p.y + shakeY, p.size, particlePaint)
        }

        for (l in labels) {
            val alpha = (l.life.pow(0.6f) * 255).toInt().coerceIn(0, 255)
            val textSize = if (l.isCombo) 68f * l.scale else 52f * l.scale
            if (textSize < 2f) continue
            labelPaint.textSize = textSize
            labelPaint.color = if (l.isCombo) Color.argb(alpha, 255, 215, 0)
                               else           Color.argb(alpha, 255, 255, 255)
            // Drop shadow
            labelPaint.setShadowLayer(12f, 0f, 0f,
                if (l.isCombo) Color.argb(alpha / 2, 255, 160, 0)
                else           Color.argb(alpha / 2, 0, 238, 187))
            canvas.drawText(l.text, l.x + shakeX, l.y + shakeY, labelPaint)
            labelPaint.clearShadowLayer()
        }
    }

    fun clear() { particles.clear(); labels.clear() }
}
