package com.gravitap.entities

import android.graphics.Color

/**
 * Central configuration for all game constants.
 * All physical measurements are in pixels; time values in milliseconds or seconds.
 */
object Constants {

    // ── Screen (populated at runtime by GameView) ────────────────────────────
    var SCREEN_W = 1080f
    var SCREEN_H  = 2400f

    // ── Ball ─────────────────────────────────────────────────────────────────
    const val BALL_RADIUS      = 22f
    const val INITIAL_SPEED    = 480f          // starting speed magnitude (px/s)
    const val MAX_BALL_SPEED   = 1600f
    const val MIN_BALL_SPEED   = 250f
    const val WALL_ELASTICITY  = 0.88f
    const val TRAIL_LENGTH     = 26            // number of trail sample points

    // ── Gravity well ─────────────────────────────────────────────────────────
    const val GRAVITY_STRENGTH  = 1800f        // base attraction constant
    const val GRAVITY_SOFTENING = 90f          // prevents division-by-zero singularity
    const val WELL_CHARGE_MS    = 180L         // phase 1: charge-up animation
    const val WELL_ACTIVE_MS    = 1900L        // phase 2: full gravitational pull
    const val WELL_FADE_MS      = 320L         // phase 3: fade-out
    const val WELL_VISUAL_RADIUS= 128f
    const val MAX_WELLS         = 3            // simultaneous wells allowed

    // ── Portal ───────────────────────────────────────────────────────────────
    const val PORTAL_RADIUS        = 36f
    const val PORTAL_HIT_RADIUS    = 52f       // slightly generous for game-feel
    const val PORTAL_PULSE_PERIOD  = 1100L     // ms per full pulse cycle
    const val PORTAL_SPAWN_MARGIN  = 120f      // min distance from screen edges
    const val PORTAL_SPAWN_CLEAR   = 160f      // min distance from ball at spawn
    const val PORTAL_LIFETIME_BASE = 6500L     // starts at level PORTAL_LIFE_LEVEL
    const val PORTAL_LIFE_DECAY    = 220L      // reduce lifetime per level
    const val PORTAL_MIN_LIFETIME  = 2200L
    const val PORTAL_LIFE_LEVEL    = 5         // lifetime mechanic kicks in at this level
    const val PORTAL_MOVE_PER_LVL  = 42f       // px/s speed added per level
    const val PORTAL_MOVE_START    = 3         // movement starts at this level
    const val PORTAL_MAX_SPEED     = 340f

    // ── Trajectory prediction arc ────────────────────────────────────────────
    const val PREDICT_STEPS        = 70
    const val PREDICT_DT           = 0.024f    // seconds per simulation step (~1.7s lookahead)
    const val PREDICT_SHOW_MS      = 420L      // how long the arc is shown after well placed
    const val PREDICT_DOT_INTERVAL = 5         // draw dot every N steps

    // ── Combo ────────────────────────────────────────────────────────────────
    const val COMBO_WINDOW_MS = 3200L
    const val COMBO_MAX       = 10

    // ── Scoring ──────────────────────────────────────────────────────────────
    const val BASE_SCORE         = 100
    const val LEVEL_BONUS_SCORE  = 25
    const val COMBO_FLOAT_RISE   = 160f        // px/s rise speed of floating combo text
    const val COMBO_FLOAT_LIFE   = 1.1f        // seconds

    // ── Lives ────────────────────────────────────────────────────────────────
    const val INITIAL_LIVES      = 3
    const val MAX_LIVES          = 3
    const val RECHARGE_INTERVAL  = 6           // collect this many portals to earn 1 life

    // ── Particles ────────────────────────────────────────────────────────────
    const val EXPLODE_COUNT       = 32
    const val PARTICLE_LIFE_BASE  = 1.0f
    const val PARTICLE_LIFE_RAND  = 0.6f
    const val PARTICLE_SPEED_BASE = 360f
    const val PARTICLE_SPEED_RAND = 480f
    const val PARTICLE_SIZE_BASE  = 7f
    const val PARTICLE_SIZE_RAND  = 9f

    // ── Well ripple rings ────────────────────────────────────────────────────
    const val RIPPLE_SPEED     = 340f          // px/s expansion
    const val RIPPLE_MAX_RINGS = 3
    const val RIPPLE_INTERVAL  = 320L          // ms between ring spawns

    // ── Screen shake ─────────────────────────────────────────────────────────
    const val SHAKE_BASE     = 22f
    const val SHAKE_COMBO    = 5f
    const val SHAKE_DECAY    = 0.80f
    const val SHAKE_MISS     = 4f

    // ── Ambient stars ────────────────────────────────────────────────────────
    const val AMBIENT_COUNT  = 55

    // ── Difficulty ───────────────────────────────────────────────────────────
    const val LEVEL_THRESHOLD = 5              // portals collected per level

    // ── Colors (ARGB Int) ────────────────────────────────────────────────────
    val COL_BG              = Color.argb(255,   8,   8,  18)
    val COL_GRID            = Color.argb( 22,   0, 238, 187)
    val COL_BALL            = Color.argb(255,   0, 238, 187)
    val COL_BALL_GLOW       = Color.argb( 80,   0, 238, 187)
    val COL_TRAIL_TIP       = Color.argb(160,   0, 238, 187)
    val COL_WELL            = Color.argb(255, 155,  89, 255)
    val COL_WELL_GLOW       = Color.argb( 70, 155,  89, 255)
    val COL_WELL_RING       = Color.argb( 55, 155,  89, 255)
    val COL_PREDICT_DOT     = Color.argb( 65,   0, 238, 187)
    val COL_WHITE           = Color.argb(255, 255, 255, 255)
    val COL_COMBO_TEXT      = Color.argb(255, 255, 215,   0)
    val COL_SCORE_TEXT      = Color.argb(255, 255, 255, 255)
    val COL_OVERLAY         = Color.argb(195,   4,   4,  12)
    val COL_LIVE_ACTIVE     = Color.argb(255,   0, 238, 187)
    val COL_LIVE_EMPTY      = Color.argb( 45,   0, 238, 187)
    val COL_TITLE           = Color.argb(255,   0, 238, 187)
    val COL_TITLE_GLOW      = Color.argb(110,   0, 238, 187)
    val COL_SUBTITLE        = Color.argb(160, 255, 255, 255)
    val COL_BLINK_TEXT      = Color.argb(255,   0, 238, 187)
    val COL_NEW_RECORD      = Color.argb(255, 255, 215,   0)
    val COL_GAMEOVER_TEXT   = Color.argb(255, 255,  80,  80)

    /** Cycling portal color palette: hot pink, gold, neon green, sky blue, orange */
    val PORTAL_COLORS = intArrayOf(
        Color.argb(255, 255,  94, 200),
        Color.argb(255, 255, 215,   0),
        Color.argb(255,   0, 255, 136),
        Color.argb(255,   0, 191, 255),
        Color.argb(255, 255, 106,   0)
    )
}
