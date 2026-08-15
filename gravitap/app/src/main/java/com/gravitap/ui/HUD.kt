package com.gravitap.ui

import android.graphics.*
import com.gravitap.entities.Constants
import kotlin.math.*

class HUD(private val screenW: Float, private val screenH: Float) {

    private val scorePaint     = Paint(Paint.ANTI_ALIAS_FLAG)
    private val scoreShadow    = Paint(Paint.ANTI_ALIAS_FLAG)
    private val labelPaint     = Paint(Paint.ANTI_ALIAS_FLAG)
    private val livePaint      = Paint(Paint.ANTI_ALIAS_FLAG)
    private val liveRingPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val levelPaint     = Paint(Paint.ANTI_ALIAS_FLAG)
    private val comboBarPaint  = Paint(Paint.ANTI_ALIAS_FLAG)
    private val hudBgPaint     = Paint(Paint.ANTI_ALIAS_FLAG)

    private var displayedScore = 0f

    init {
        scorePaint.typeface  = Typeface.DEFAULT_BOLD
        scorePaint.textAlign = Paint.Align.CENTER
        scorePaint.textSize  = 92f
        scorePaint.color     = Constants.COL_SCORE_TEXT

        scoreShadow.typeface  = Typeface.DEFAULT_BOLD
        scoreShadow.textAlign = Paint.Align.CENTER
        scoreShadow.textSize  = 92f
        scoreShadow.setShadowLayer(24f, 0f, 0f, Color.argb(120, 0, 238, 187))
        scoreShadow.color = Color.argb(180, 200, 255, 240)

        labelPaint.typeface  = Typeface.DEFAULT_BOLD
        labelPaint.textAlign = Paint.Align.CENTER
        labelPaint.textSize  = 30f
        labelPaint.color     = Color.argb(140, 0, 238, 187)
        labelPaint.letterSpacing = 0.25f

        livePaint.style = Paint.Style.FILL
        liveRingPaint.style  = Paint.Style.STROKE
        liveRingPaint.strokeWidth = 2.5f

        levelPaint.typeface  = Typeface.DEFAULT_BOLD
        levelPaint.textAlign = Paint.Align.RIGHT
        levelPaint.textSize  = 32f
        levelPaint.color     = Color.argb(160, 0, 238, 187)

        comboBarPaint.style = Paint.Style.FILL

        hudBgPaint.style = Paint.Style.FILL
    }

    fun update(targetScore: Int, dtS: Float) {
        // Smooth score counter rolling up
        val diff = targetScore - displayedScore
        displayedScore += diff * (1f - exp(-dtS * 10f))
    }

    /** Draw the top HUD (score, level). No screen shake applied here — HUD is fixed. */
    fun drawPlaying(
        canvas: Canvas,
        score: Int,
        level: Int,
        lives: Int,
        combo: Int,
        comboFrac: Float     // 0..1: how much combo time remains
    ) {
        val topY   = 90f
        val cx     = screenW / 2f
        val scoreY = topY + 80f

        // Semi-transparent top bar
        hudBgPaint.color = Color.argb(90, 4, 4, 14)
        canvas.drawRect(0f, 0f, screenW, scoreY + 24f, hudBgPaint)

        // Score label
        canvas.drawText("SCORE", cx, topY, labelPaint)

        // Score value with glow shadow
        canvas.drawText(displayedScore.toInt().toString(), cx, scoreY, scoreShadow)
        canvas.drawText(displayedScore.toInt().toString(), cx, scoreY, scorePaint)

        // Combo timer bar (thin strip under score)
        if (combo > 1 && comboFrac > 0f) {
            val barW   = screenW * 0.55f
            val barH   = 6f
            val barLeft= cx - barW / 2f
            val barTop = scoreY + 14f
            // Background
            comboBarPaint.color = Color.argb(50, 255, 215, 0)
            canvas.drawRoundRect(barLeft, barTop, barLeft + barW, barTop + barH, barH, barH, comboBarPaint)
            // Fill
            comboBarPaint.color = Color.argb((160 + 90 * comboFrac).toInt(), 255, 215, 0)
            canvas.drawRoundRect(barLeft, barTop, barLeft + barW * comboFrac, barTop + barH, barH, barH, comboBarPaint)
        }

        // Lives (bottom-left, row of orbs)
        drawLives(canvas, lives)

        // Level (bottom-right)
        levelPaint.textAlign = Paint.Align.RIGHT
        canvas.drawText("LVL $level", screenW - 36f, screenH - 50f, levelPaint)
    }

    private fun drawLives(canvas: Canvas, lives: Int) {
        val orbR  = 14f
        val startX= 44f
        val y     = screenH - 50f
        for (i in 0 until Constants.MAX_LIVES) {
            val cx = startX + i * (orbR * 2 + 14f)
            if (i < lives) {
                livePaint.color = Constants.COL_LIVE_ACTIVE
                canvas.drawCircle(cx, y, orbR, livePaint)
                // Inner glow
                livePaint.color = Color.argb(120, 200, 255, 240)
                canvas.drawCircle(cx - orbR * 0.25f, y - orbR * 0.25f, orbR * 0.38f, livePaint)
            } else {
                livePaint.color = Constants.COL_LIVE_EMPTY
                canvas.drawCircle(cx, y, orbR, livePaint)
                liveRingPaint.color = Color.argb(70, 0, 238, 187)
                canvas.drawCircle(cx, y, orbR, liveRingPaint)
            }
        }
    }

    /** Full-screen overlay for the main menu. */
    fun drawMenu(canvas: Canvas, bestScore: Int, blinkOn: Boolean, time: Long) {
        val cx  = screenW / 2f
        val cy  = screenH / 2f

        // Big title: G R A V I T A P
        val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            typeface    = Typeface.DEFAULT_BOLD
            textAlign   = Paint.Align.CENTER
            textSize    = 102f
            letterSpacing = 0.15f
        }
        val t = (time.toFloat() / 1200f) % (2f * PI.toFloat())
        val glowAlpha = (130 + 100 * sin(t)).toInt().coerceIn(60, 230)
        titlePaint.setShadowLayer(36f, 0f, 0f, Color.argb(glowAlpha, 0, 238, 187))
        titlePaint.color = Constants.COL_TITLE
        canvas.drawText("GRAVITAP", cx, cy - 200f, titlePaint)
        titlePaint.clearShadowLayer()

        // Subtitle
        val subPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            typeface    = Typeface.DEFAULT
            textAlign   = Paint.Align.CENTER
            textSize    = 34f
            color       = Constants.COL_SUBTITLE
            letterSpacing = 0.08f
        }
        canvas.drawText("TAP THE VOID. BEND GRAVITY.", cx, cy - 130f, subPaint)

        // Description line
        subPaint.textSize = 28f
        subPaint.color = Color.argb(100, 0, 238, 187)
        canvas.drawText("Tap to create gravity wells — guide the ball to portals.", cx, cy - 88f, subPaint)

        // Best score
        if (bestScore > 0) {
            val bsPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                typeface  = Typeface.DEFAULT_BOLD
                textAlign = Paint.Align.CENTER
                textSize  = 44f
                color     = Color.argb(200, 255, 215, 0)
            }
            canvas.drawText("BEST: $bestScore", cx, cy + 60f, bsPaint)
        }

        // Tap to play (blink)
        if (blinkOn) {
            val tapPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                typeface    = Typeface.DEFAULT_BOLD
                textAlign   = Paint.Align.CENTER
                textSize    = 52f
                color       = Constants.COL_BLINK_TEXT
                letterSpacing = 0.12f
            }
            tapPaint.setShadowLayer(18f, 0f, 0f, Color.argb(180, 0, 238, 187))
            canvas.drawText("TAP TO PLAY", cx, cy + 180f, tapPaint)
        }
    }

    /** Full-screen overlay for game over. */
    fun drawGameOver(
        canvas: Canvas,
        score: Int,
        bestScore: Int,
        isNewRecord: Boolean,
        blinkOn: Boolean,
        time: Long
    ) {
        // Dark overlay
        hudBgPaint.color = Constants.COL_OVERLAY
        canvas.drawRect(0f, 0f, screenW, screenH, hudBgPaint)

        val cx = screenW / 2f
        val cy = screenH / 2f

        // Game over
        val goPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            typeface  = Typeface.DEFAULT_BOLD
            textAlign = Paint.Align.CENTER
            textSize  = 90f
            color     = Constants.COL_GAMEOVER_TEXT
        }
        goPaint.setShadowLayer(30f, 0f, 0f, Color.argb(160, 255, 40, 40))
        canvas.drawText("GAME OVER", cx, cy - 200f, goPaint)
        goPaint.clearShadowLayer()

        // Score
        val sPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            typeface  = Typeface.DEFAULT_BOLD
            textAlign = Paint.Align.CENTER
            textSize  = 118f
            color     = Constants.COL_SCORE_TEXT
        }
        sPaint.setShadowLayer(28f, 0f, 0f, Color.argb(130, 0, 238, 187))
        canvas.drawText(score.toString(), cx, cy - 60f, sPaint)
        sPaint.clearShadowLayer()

        val lblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            typeface  = Typeface.DEFAULT_BOLD
            textAlign = Paint.Align.CENTER
            textSize  = 30f
            color     = Color.argb(140, 0, 238, 187)
            letterSpacing = 0.25f
        }
        canvas.drawText("SCORE", cx, cy - 110f, lblPaint)

        // New record
        if (isNewRecord) {
            val t = (time.toFloat() / 600f) % (2f * PI.toFloat())
            val alpha = (180 + 70 * sin(t)).toInt()
            val nrPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                typeface  = Typeface.DEFAULT_BOLD
                textAlign = Paint.Align.CENTER
                textSize  = 48f
                color     = Color.argb(alpha, 255, 215, 0)
            }
            nrPaint.setShadowLayer(20f, 0f, 0f, Color.argb(alpha / 2, 255, 160, 0))
            canvas.drawText("★ NEW RECORD! ★", cx, cy + 60f, nrPaint)
        } else {
            val bsPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                typeface  = Typeface.DEFAULT_BOLD
                textAlign = Paint.Align.CENTER
                textSize  = 40f
                color     = Color.argb(180, 200, 200, 200)
            }
            canvas.drawText("BEST: $bestScore", cx, cy + 60f, bsPaint)
        }

        // Tap to retry (blink)
        if (blinkOn) {
            val retryPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                typeface    = Typeface.DEFAULT_BOLD
                textAlign   = Paint.Align.CENTER
                textSize    = 48f
                color       = Constants.COL_BLINK_TEXT
                letterSpacing = 0.1f
            }
            retryPaint.setShadowLayer(16f, 0f, 0f, Color.argb(160, 0, 238, 187))
            canvas.drawText("TAP TO RETRY", cx, cy + 190f, retryPaint)
        }
    }
}
