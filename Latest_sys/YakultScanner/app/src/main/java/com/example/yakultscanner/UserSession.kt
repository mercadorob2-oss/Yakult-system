package com.example.yakultscanner

import android.content.Context
import com.example.yakultscanner.api.LoginResponse

// Simple in-memory user session holder for the Android app.
// Later, when we have real REST auth, this can be extended to store tokens, roles, etc.

data class LoggedInUser(
    val userId: Int,
    val username: String,
    val displayName: String,
    val email: String?,
    val token: String,
    val expiresUtc: String?
)

object UserSession {
    private const val PREFS_NAME = "yakult_scanner_session"
    private const val KEY_USER_ID = "user_id"
    private const val KEY_USERNAME = "username"
    private const val KEY_DISPLAY_NAME = "display_name"
    private const val KEY_EMAIL = "email"
    private const val KEY_TOKEN = "token"
    private const val KEY_EXPIRES_UTC = "expires_utc"
    private const val KEY_CACHED_USERNAME = "cached_username"
    private const val KEY_CACHED_PASSWORD_HASH = "cached_password_hash"
    private const val KEY_REMEMBER_ME = "remember_me"
    private const val KEY_LAST_USERNAME = "last_username"

    private var appContext: Context? = null

    // Null when no one is logged in
    var currentUser: LoggedInUser? = null

    val authToken: String?
        get() = currentUser?.token

    fun isLoggedIn(): Boolean = currentUser != null

    fun init(context: Context) {
        appContext = context.applicationContext
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        val token = prefs.getString(KEY_TOKEN, null)
        val username = prefs.getString(KEY_USERNAME, null)
        val displayName = prefs.getString(KEY_DISPLAY_NAME, null)
        if (token.isNullOrBlank() || username.isNullOrBlank() || displayName.isNullOrBlank()) {
            return
        }

        currentUser = LoggedInUser(
            userId = prefs.getInt(KEY_USER_ID, 0),
            username = username,
            displayName = displayName,
            email = prefs.getString(KEY_EMAIL, null),
            token = token,
            expiresUtc = prefs.getString(KEY_EXPIRES_UTC, null)
        )
    }

    fun loginFromApi(loginUsername: String, response: LoginResponse, rememberMe: Boolean = true) {
        val token = response.token?.trim().orEmpty()
        val username = loginUsername.trim()
        val displayName = (response.name ?: "").trim().ifBlank { username }

        currentUser = LoggedInUser(
            userId = response.userId,
            username = username,
            displayName = displayName,
            email = response.email,
            token = token,
            expiresUtc = response.expiresUtc
        )

        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        // "Remember me" off = session lives in memory only; next cold start asks to sign in.
        // Always remember the username for prefill and the checkbox default.
        val edit = prefs.edit()
            .putString(KEY_LAST_USERNAME, username)
            .putBoolean(KEY_REMEMBER_ME, rememberMe)
        if (!rememberMe) {
            edit.remove(KEY_USER_ID)
                .remove(KEY_USERNAME)
                .remove(KEY_DISPLAY_NAME)
                .remove(KEY_EMAIL)
                .remove(KEY_TOKEN)
                .remove(KEY_EXPIRES_UTC)
                .remove(KEY_CACHED_USERNAME)
                .remove(KEY_CACHED_PASSWORD_HASH)
        } else {
            edit.putInt(KEY_USER_ID, response.userId)
                .putString(KEY_USERNAME, currentUser?.username)
                .putString(KEY_DISPLAY_NAME, currentUser?.displayName)
                .putString(KEY_EMAIL, currentUser?.email)
                .putString(KEY_TOKEN, currentUser?.token)
                .putString(KEY_EXPIRES_UTC, currentUser?.expiresUtc)
        }
        edit.apply()
    }

    fun cacheCredentials(username: String, password: String) {
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        prefs.edit()
            .putString(KEY_CACHED_USERNAME, username.trim().lowercase())
            .putString(KEY_CACHED_PASSWORD_HASH, hashPassword(password.trim()))
            .apply()
    }

    fun attemptOfflineLogin(username: String, password: String): Boolean {
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return false
        val cachedUsername = prefs.getString(KEY_CACHED_USERNAME, null)?.lowercase()
        val cachedHash = prefs.getString(KEY_CACHED_PASSWORD_HASH, null)

        if (cachedUsername.isNullOrBlank() || cachedHash.isNullOrBlank()) {
            return false
        }

        if (username.trim().lowercase() != cachedUsername || hashPassword(password.trim()) != cachedHash) {
            return false
        }

        val userId = prefs.getInt(KEY_USER_ID, 0)
        val displayName = prefs.getString(KEY_DISPLAY_NAME, null) ?: username.trim()
        val email = prefs.getString(KEY_EMAIL, null)
        val syntheticToken = "offline_${System.currentTimeMillis()}"

        currentUser = LoggedInUser(
            userId = userId,
            username = username.trim(),
            displayName = displayName,
            email = email,
            token = syntheticToken,
            expiresUtc = null
        )

        prefs.edit()
            .putString(KEY_TOKEN, syntheticToken)
            .putString(KEY_EXPIRES_UTC, null)
            .apply()

        return true
    }

    fun logout() {
        currentUser = null
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        // Keep last username + checkbox default so sign-in stays one tap away.
        val lastUsername = prefs.getString(KEY_LAST_USERNAME, null)
        val rememberMe = prefs.getBoolean(KEY_REMEMBER_ME, true)
        prefs.edit().clear().apply()
        prefs.edit()
            .putString(KEY_LAST_USERNAME, lastUsername)
            .putBoolean(KEY_REMEMBER_ME, rememberMe)
            .apply()
    }

    /** Last signed-in username, for prefill. Never blank. */
    fun lastUsername(): String? {
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return null
        return prefs.getString(KEY_LAST_USERNAME, null)?.trim()?.ifBlank { null }
    }

    /** Saved "Remember me" checkbox default. True when never set. */
    fun rememberMeEnabled(): Boolean {
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return true
        return prefs.getBoolean(KEY_REMEMBER_ME, true)
    }

    /**
     * True when a restored session exists AND its token is still usable.
     * JWT exp claim is checked client-side (server lifetime is 12h); opaque
     * offline tokens (offline_<...>) can't expire-check so are trusted.
     */
    fun isSessionUsable(): Boolean {
        val token = currentUser?.token?.trim().orEmpty()
        if (token.isBlank()) return false
        return !isJwtExpired(token)
    }

    private fun isJwtExpired(token: String): Boolean {
        val parts = token.split(".")
        if (parts.size != 3) return false // not a JWT (e.g. offline token) — trust it
        return try {
            val payload = parts[1]
                .replace('-', '+')
                .replace('_', '/')
                .let { it + "=".repeat((4 - it.length % 4) % 4) }
            val json = String(android.util.Base64.decode(payload, android.util.Base64.DEFAULT), Charsets.UTF_8)
            val exp = Regex("\"exp\"\\s*:\\s*(\\d+)").find(json)?.groupValues?.get(1)?.toLongOrNull()
                ?: return false
            // Small clock-skew grace so borderline tokens still work.
            exp * 1000L < System.currentTimeMillis() - 60_000L
        } catch (_: Exception) {
            false
        }
    }

    private fun hashPassword(password: String): String {
        return try {
            val digest = java.security.MessageDigest.getInstance("SHA-256")
            val hashBytes = digest.digest(password.toByteArray(Charsets.UTF_8))
            hashBytes.joinToString("") { "%02x".format(it) }
        } catch (_: Exception) {
            password
        }
    }
}
