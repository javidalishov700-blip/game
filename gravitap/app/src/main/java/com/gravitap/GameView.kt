package com.gravitap

import android.content.Context
import android.graphics.*
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.view.MotionEvent
import android.view.SurfaceHolder
import android.view.SurfaceView
import com.gravitap.entities.Ball
import com.gravitap.entities.Constants
import com.gravitap.entities.GravityWell
import com.gravitap.entities.Portal
import com.gravitap.systems.ParticleSystem
import com.gravitap.systems.ScreenShake
import com.gravitap.ui.HUD
import java.util.ArrayDeque
import kotlin.math.*
import kotlin.random.Random

class GameView(context: Context) : SurfaceView(context), Runnable, SurfaceHolder.Callback {

    // ── State ─────────────────────────────────────────────────────────────────
    private enum class GameState { MENU, PLAYING, GAME_OVER }
    @Volatile private var gameState = GameState.MENU

    // ── Thread ────────────────────────────────────────────────────────────────
    private var gameThread: Thread? = null
    @Volatile private var running  = false

    // ── Screen ────────────────────────────────────────────────────────────────
    private var screenW = 0f
    private var screenH = 0f

    // ── Entities ──────────────────────────────────────────────────────────────
    private var ball  = Ball(540f, 1200f)
    private val wells   = ArrayList<GravityWell>(Constants.MAX_WELLS)
    private val portals = ArrayList<Portal>(4)

    // ── Systems ───────────────────────────────────────────────────────────────
    private val particles  = ParticleSystem()
    private val shake      = ScreenShake()
    private lateinit var hud: HUD

    // ── Score / progress ──────────────────────────────────────────────────────
    private var score             = 0
    private var combo             = 1
    private var lastHitTime       = 0L
    private var portalsCollected  = 0
    private var level             = 1
    private var lives             = Constants.INITIAL_LIVES
    private var portalColorIdx    = 0

    // ── Best score (SharedPreferences) ────────────────────────────────────────
    private val prefs   by lazy { context.getSharedPreferences("gravitap_v1", Context.MODE_PRIVATE) }
    private var bestScore = 0

    // ── Trajectory prediction ─────────────────────────────────────────────────
    private var prediction       = emptyList<PointF>()
    private var predictionExpiry = 0L
    private val predPaint        = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
        color = Constants.COL_PREDICT_DOT
    }

    // ── Background ────────────────────────────────────────────────────────────
    private val bgPath      = Path()
    private val bgGridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style       = Paint.Style.STROKE
        strokeWidth = 1f
        color       = Constants.COL_GRID
    }
    private val bgFillPaint = Paint().apply { color = Constants.COL_BG }
    private val ambPaint    = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.FILL }

    private data class AmbientDot(var x: Float, var y: Float, var alpha: Float,
                                   val speed: Float, val size: Float)
    private val ambientDots = ArrayList<AmbientDot>(Constants.AMBIENT_COUNT)

    // ── UI blink ──────────────────────────────────────────────────────────────
    private var blinkOn      = true
    private var lastBlinkMs  = 0L

    // ── Input queue (main→game thread) ────────────────────────────────────────
    private val tapQueue = ArrayDeque<PointF>()
    private val tapLock  = Any()

    // ── Haptics ───────────────────────────────────────────────────────────────
    private val vibrator: Vibrator? by lazy {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            (context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? VibratorManager)
                ?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
        }
    }

    // ── Frame time tracking ───────────────────────────────────────────────────

    init {
        holder.addCallback(this)
        isFocusable = true
        bestScore = 0  // will be loaded on surface init
    }

    // ── SurfaceHolder.Callback ────────────────────────────────────────────────

    override fun surfaceCreated(h: SurfaceHolder) {}

    override fun surfaceChanged(h: SurfaceHolder, fmt: Int, w: Int, h2: Int) {
        screenW = w.toFloat(); screenH = h2.toFloat()
        Constants.SCREEN_W = screenW; Constants.SCREEN_H = screenH
        hud = HUD(screenW, screenH)
        buildHexGrid()
        buildAmbientDots()
        bestScore = prefs.getInt("best_score", 0)
        // Initialize a live demo ball so the menu background isn't empty
        ball = Ball(screenW, screenH)
        gameState = GameState.MENU
        resume()
    }

    override fun surfaceDestroyed(h: SurfaceHolder) { pause() }

    // ── Thread lifecycle ──────────────────────────────────────────────────────

    fun resume() {
        if (running) return
        running = true
        gameThread = Thread(this, "GravitAP-Game").also { it.start() }
    }

    fun pause() {
        running = false
        gameThread?.join(2000)
        gameThread = null
    }

    // ── Touch input ───────────────────────────────────────────────────────────

    override fun onTouchEvent(event: MotionEvent): Boolean {
        if (event.actionMasked == MotionEvent.ACTION_DOWN ||
            event.actionMasked == MotionEvent.ACTION_POINTER_DOWN) {
            val idx = event.actionIndex
            synchronized(tapLock) { tapQueue.push(PointF(event.getX(idx), event.getY(idx))) }
        }
        return true
    }

    // ── Main game loop ────────────────────────────────────────────────────────

    override fun run() {
        var lastNs = System.nanoTime()
        while (running) {
            val nowNs = System.nanoTime()
            val dtS   = ((nowNs - lastNs) / 1_000_000_000f).coerceIn(0f, 0.05f)
            lastNs = nowNs

            // Process queued taps
            val taps = ArrayList<PointF>()
            synchronized(tapLock) { while (tapQueue.isNotEmpty()) taps.add(tapQueue.pop()) }
            for (tap in taps) handleTap(tap)

            // Update + Render using a consistent timestamp for the frame
            val nowMs = System.currentTimeMillis()
            val canvas = holder.lockCanvas() ?: continue
            try {
                update(dtS, nowMs)
                render(canvas, nowMs)
            } finally {
                holder.unlockCanvasAndPost(canvas)
            }

            // Throttle to ~60fps
            val frameMs = (System.nanoTime() - nowNs) / 1_000_000
            if (frameMs < 16) Thread.sleep(16 - frameMs)
        }
    }

    // ── Tap dispatch ──────────────────────────────────────────────────────────

    private fun handleTap(tap: PointF) {
        when (gameState) {
            GameState.MENU      -> startNewGame()
            GameState.GAME_OVER -> startNewGame()
            GameState.PLAYING   -> placeGravityWell(tap.x, tap.y)
        }
    }

    // ── Game init ─────────────────────────────────────────────────────────────

    private fun startNewGame() {
        ball             = Ball(screenW, screenH)
        wells.clear()
        portals.clear()
        particles.clear()
        score            = 0
        combo            = 1
        lastHitTime      = 0L
        portalsCollected = 0
        level            = 1
        lives            = Constants.INITIAL_LIVES
        portalColorIdx   = 0
        prediction       = emptyList()
        predictionExpiry = 0L

        // Spawn first portal
        spawnPortal()
        gameState = GameState.PLAYING
    }

    private fun spawnPortal() {
        portals.add(Portal(portalColorIdx, screenW, screenH, ball.x, ball.y, level))
        portalColorIdx = (portalColorIdx + 1) % Constants.PORTAL_COLORS.size
    }

    // ── Place gravity well ────────────────────────────────────────────────────

    private fun placeGravityWell(tx: Float, ty: Float) {
        // Evict oldest well if at capacity
        if (wells.size >= Constants.MAX_WELLS) wells.removeAt(0)
        val well = GravityWell(tx, ty)
        wells.add(well)
        particles.wellBurst(tx, ty)
        haptic(1)

        // Compute trajectory prediction immediately
        refreshPrediction()
        predictionExpiry = System.currentTimeMillis() + Constants.PREDICT_SHOW_MS
    }

    // ── Trajectory prediction ─────────────────────────────────────────────────

    private fun refreshPrediction() {
        val pts = ArrayList<PointF>(Constants.PREDICT_STEPS)
        var px = ball.x; var py = ball.y
        var pvx = ball.vx; var pvy = ball.vy
        val activeWells = wells.filter { it.isAttracting() }
        if (activeWells.isEmpty()) { prediction = emptyList(); return }

        repeat(Constants.PREDICT_STEPS) {
            for (w in activeWells) {
                val dx = w.x - px; val dy = w.y - py
                val s2 = Constants.GRAVITY_SOFTENING * Constants.GRAVITY_SOFTENING
                val d2 = dx * dx + dy * dy + s2
                val d  = sqrt(d2)
                val mag = min(Constants.GRAVITY_STRENGTH * w.strength / d2, 4200f)
                pvx += (dx / d) * mag * Constants.PREDICT_DT
                pvy += (dy / d) * mag * Constants.PREDICT_DT
            }
            val sp = sqrt(pvx * pvx + pvy * pvy)
            if (sp > Constants.MAX_BALL_SPEED) { pvx = pvx / sp * Constants.MAX_BALL_SPEED; pvy = pvy / sp * Constants.MAX_BALL_SPEED }
            px += pvx * Constants.PREDICT_DT; py += pvy * Constants.PREDICT_DT
            val r = Constants.BALL_RADIUS
            if (px - r < 0) { px = r; pvx = abs(pvx) * Constants.WALL_ELASTICITY }
            if (px + r > screenW) { px = screenW - r; pvx = -abs(pvx) * Constants.WALL_ELASTICITY }
            if (py - r < 0) { py = r; pvy = abs(pvy) * Constants.WALL_ELASTICITY }
            if (py + r > screenH) { py = screenH - r; pvy = -abs(pvy) * Constants.WALL_ELASTICITY }
            pts.add(PointF(px, py))
        }
        prediction = pts
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private fun update(dtS: Float, nowMs: Long) {
        // Blink ticker (applies to all states)
        if (nowMs - lastBlinkMs > 620) { blinkOn = !blinkOn; lastBlinkMs = nowMs }

        // Menu: run a live demo ball (no wells, no portals)
        if (gameState == GameState.MENU) {
            ball.update(dtS, emptyList(), screenW, screenH)
            particles.update(dtS)
            shake.update()
            for (dot in ambientDots) { dot.y -= dot.speed * dtS; if (dot.y < -5f) dot.y = screenH + 5f }
            return
        }

        // Game over: just keep effects alive
        if (gameState == GameState.GAME_OVER) {
            particles.update(dtS)
            shake.update()
            hud.update(score, dtS)
            for (dot in ambientDots) { dot.y -= dot.speed * dtS; if (dot.y < -5f) dot.y = screenH + 5f }
            return
        }

        // ── PLAYING ──────────────────────────────────────────────────────────

        // Update & prune wells
        for (w in wells) w.update(dtS)
        wells.removeAll { it.isDead() }

        // Update ball (gravity wells applied inside)
        ball.update(dtS, wells, screenW, screenH)

        // Update portals; check hits
        val portalIter = portals.iterator()
        while (portalIter.hasNext()) {
            val p = portalIter.next()
            p.update(dtS)
            if (p.checkHit(ball.x, ball.y)) {
                onPortalHit(p)
                portalIter.remove()
            } else if (p.isExpired()) {
                onPortalExpired()
                portalIter.remove()
            }
        }

        // Ensure at least 1 portal on screen (plus extra at higher levels)
        val maxPortals = 1 + (level - 1) / 5
        while (portals.size < maxPortals.coerceAtMost(3)) spawnPortal()

        // Combo window timer
        val comboFrac = if (lastHitTime > 0L) {
            ((Constants.COMBO_WINDOW_MS - (nowMs - lastHitTime)).toFloat() / Constants.COMBO_WINDOW_MS).coerceIn(0f, 1f)
        } else 1f
        if (comboFrac <= 0f && combo > 1) combo = 1

        // Ambient dots
        for (dot in ambientDots) {
            dot.y -= dot.speed * dtS
            if (dot.y < -5f) dot.y = screenH + 5f
        }

        // Systems
        particles.update(dtS)
        shake.update()
        hud.update(score, dtS)

        // Prediction expiry
        if (nowMs > predictionExpiry) prediction = emptyList()
    }

    // ── Portal hit callback ───────────────────────────────────────────────────

    private fun onPortalHit(p: Portal) {
        val nowMs = System.currentTimeMillis()

        // Combo
        if (lastHitTime > 0L && nowMs - lastHitTime <= Constants.COMBO_WINDOW_MS) {
            combo = (combo + 1).coerceAtMost(Constants.COMBO_MAX)
        } else {
            combo = 1
        }
        lastHitTime = nowMs

        // Score
        val gained = Constants.BASE_SCORE * level * combo + portalsCollected / 5 * Constants.LEVEL_BONUS_SCORE
        score += gained
        portalsCollected++

        // Level
        level = 1 + portalsCollected / Constants.LEVEL_THRESHOLD

        // Life recharge
        if (portalsCollected % Constants.RECHARGE_INTERVAL == 0 && lives < Constants.MAX_LIVES) {
            lives++
            particles.spawnLabel("+LIFE", p.x, p.y - 160f, true)
        }

        // Effects
        ball.addBoost(38f)
        particles.explode(p.x, p.y, p.color, combo)
        particles.spawnLabel("+$gained", p.x, p.y - 55f, false)
        if (combo > 1) particles.spawnLabel("×$combo COMBO", p.x, p.y - 115f, true)

        val shakeAmt = Constants.SHAKE_BASE + Constants.SHAKE_COMBO * (combo - 1).toFloat()
        shake.trigger(shakeAmt)
        haptic(combo)

        // Update best
        if (score > bestScore) {
            bestScore = score
            prefs.edit().putInt("best_score", bestScore).apply()
        }

        // Refresh prediction arc to show new trajectory
        refreshPrediction()
        predictionExpiry = System.currentTimeMillis() + Constants.PREDICT_SHOW_MS
    }

    // ── Portal expired callback ───────────────────────────────────────────────

    private fun onPortalExpired() {
        if (gameState != GameState.PLAYING) return
        lives = (lives - 1).coerceAtLeast(0)
        shake.trigger(Constants.SHAKE_MISS)
        haptic(0)
        if (lives <= 0) {
            gameState = GameState.GAME_OVER
        }
    }

    // ── Render ────────────────────────────────────────────────────────────────

    private fun render(canvas: Canvas, nowMs: Long) {
        val sx = shake.offsetX
        val sy = shake.offsetY

        // Background
        canvas.drawRect(0f, 0f, screenW, screenH, bgFillPaint)
        canvas.drawPath(bgPath, bgGridPaint)

        // Ambient drift dots
        for (dot in ambientDots) {
            ambPaint.color = Color.argb((dot.alpha * 255).toInt(), 0, 238, 187)
            canvas.drawCircle(dot.x, dot.y, dot.size, ambPaint)
        }

        when (gameState) {
            GameState.MENU -> {
                renderMenuDemo(canvas, nowMs, sx, sy)
                hud.drawMenu(canvas, bestScore, blinkOn, nowMs)
            }
            GameState.PLAYING -> {
                renderGame(canvas, nowMs, sx, sy, drawHUD = true)
            }
            GameState.GAME_OVER -> {
                renderGame(canvas, nowMs, sx, sy, drawHUD = false)
                val isNewRecord = score >= bestScore && score > 0
                hud.drawGameOver(canvas, score, bestScore, isNewRecord, blinkOn, nowMs)
            }
        }
    }

    private fun renderGame(canvas: Canvas, nowMs: Long, sx: Float, sy: Float, drawHUD: Boolean) {
        // Trajectory prediction dots
        if (prediction.isNotEmpty()) {
            val age = ((predictionExpiry - nowMs).toFloat() / Constants.PREDICT_SHOW_MS).coerceIn(0f, 1f)
            for ((i, pt) in prediction.withIndex()) {
                if (i % Constants.PREDICT_DOT_INTERVAL != 0) continue
                val alpha = (Constants.COL_PREDICT_DOT.ushr(24) * age * (i.toFloat() / prediction.size) * 1.4f).toInt().coerceIn(0, 200)
                predPaint.color = Color.argb(alpha, 0, 238, 187)
                predPaint.style = Paint.Style.FILL
                canvas.drawCircle(pt.x + sx, pt.y + sy, 5f, predPaint)
            }
        }

        // Wells
        for (w in wells) w.draw(canvas, sx, sy)

        // Portals
        for (p in portals) p.draw(canvas, sx, sy, nowMs)

        // Ball (on top of everything except particles and HUD)
        ball.draw(canvas, sx, sy)

        // Particle effects (always on top of ball)
        particles.draw(canvas, sx, sy)

        // HUD drawn only during active play (not on top of game-over overlay)
        if (drawHUD) {
            val comboFrac = if (lastHitTime > 0L) {
                ((Constants.COMBO_WINDOW_MS - (nowMs - lastHitTime)).toFloat() / Constants.COMBO_WINDOW_MS).coerceIn(0f, 1f)
            } else 1f
            hud.drawPlaying(canvas, score, level, lives, combo, comboFrac)
        }
    }

    /** Gentle demo animation on menu screen — ball bouncing, portals pulsing. */
    private fun renderMenuDemo(canvas: Canvas, nowMs: Long, sx: Float, sy: Float) {
        // Animate demo portals
        for (p in portals) p.draw(canvas, 0f, 0f, nowMs)
        ball.draw(canvas, 0f, 0f)
        particles.draw(canvas, sx, sy)
    }

    // ── Background hex grid builder ───────────────────────────────────────────

    private fun buildHexGrid() {
        if (screenW == 0f) return
        bgPath.reset()
        val r     = 58f
        val hexH  = r * sqrt(3f) / 2f
        val cols  = (screenW / (r * 1.5f)).toInt() + 3
        val rows  = (screenH / (hexH * 2f)).toInt() + 3
        for (row in -1..rows) {
            for (col in -1..cols) {
                val cx = col * r * 1.5f
                val cy = row * hexH * 2f + if (col % 2 == 0) 0f else hexH
                for (i in 0..5) {
                    val a0 = (Math.PI / 3.0 * i - Math.PI / 6).toFloat()
                    val a1 = (Math.PI / 3.0 * (i + 1) - Math.PI / 6).toFloat()
                    bgPath.moveTo(cx + r * cos(a0), cy + r * sin(a0))
                    bgPath.lineTo(cx + r * cos(a1), cy + r * sin(a1))
                }
            }
        }
    }

    // ── Ambient dots builder ──────────────────────────────────────────────────

    private fun buildAmbientDots() {
        if (screenW == 0f) return
        ambientDots.clear()
        val rng = Random.Default
        repeat(Constants.AMBIENT_COUNT) {
            ambientDots.add(AmbientDot(
                x     = rng.nextFloat() * screenW,
                y     = rng.nextFloat() * screenH,
                alpha = rng.nextFloat() * 0.25f + 0.03f,
                speed = rng.nextFloat() * 22f + 8f,
                size  = rng.nextFloat() * 2.2f + 0.5f
            ))
        }
    }

    // ── Haptic feedback ───────────────────────────────────────────────────────

    private fun haptic(comboLevel: Int) {
        val vib = vibrator ?: return
        if (!vib.hasVibrator()) return
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                val (dur, amp) = when {
                    comboLevel == 0 -> 18L to 60
                    comboLevel == 1 -> 30L to 120
                    comboLevel in 2..3 -> 55L to 200
                    else             -> 80L to VibrationEffect.DEFAULT_AMPLITUDE
                }
                vib.vibrate(VibrationEffect.createOneShot(dur, amp))
            } else {
                @Suppress("DEPRECATION")
                vib.vibrate(if (comboLevel >= 3) 55L else 25L)
            }
        } catch (_: Exception) {}
    }
}
