package com.example.yakultscanner.settings

import android.content.Context
import android.net.Uri
import com.example.yakultscanner.BuildConfig
import com.example.yakultscanner.ConnectionHealthStore

enum class ApiEnvironment(val storageValue: String, val label: String) {
    DEVELOPMENT("development", "Development"),
    PRODUCTION("production", "Production"),
    CUSTOM("custom", "Custom diagnostic URL")
}

data class ApiEnvironmentOption(val environment: ApiEnvironment, val baseUrl: String, val label: String)

/**
 * Persists the scanner's API target. Development and Production are named, approved targets;
 * arbitrary URLs remain possible only through the existing administrator diagnostic path and are
 * marked Custom rather than silently looking like one of the approved environments.
 */
object ApiSettings {
    private const val PREFS_NAME = "yakult_api_settings"
    private const val KEY_BASE_URL = "api_base_url"
    private const val KEY_ENVIRONMENT = "api_environment"
    private const val KEY_OFFLINE_MODE = "offline_mode"

    // These retain the already-configured Gradle endpoints as explicit named choices. Deployment
    // owners update their APK flavor/config when network addresses change; users never edit IIS.
    private const val DEVELOPMENT_URL = "http://192.168.100.186:7014/"
    private const val PRODUCTION_URL = "http://192.168.100.186:7015/"

    private var appContext: Context? = null

    fun init(context: Context) {
        appContext = context.applicationContext
        runCatching { ConnectionHealthStore.updateBaseUrl(apiBaseUrl) }
    }

    val approvedEnvironments: List<ApiEnvironmentOption>
        get() = listOf(
            ApiEnvironmentOption(ApiEnvironment.DEVELOPMENT, DEVELOPMENT_URL, "Development"),
            ApiEnvironmentOption(ApiEnvironment.PRODUCTION, PRODUCTION_URL, "Production")
        )

    val selectedEnvironment: ApiEnvironment
        get() {
            val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val stored = prefs?.getString(KEY_ENVIRONMENT, null)
            ApiEnvironment.values().firstOrNull { it.storageValue == stored }?.let { return it }
            return inferEnvironment(prefs?.getString(KEY_BASE_URL, null))
        }

    val apiBaseUrl: String
        get() {
            val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val stored = prefs?.getString(KEY_BASE_URL, null)?.trim()
            if (!stored.isNullOrBlank()) return normalizeBaseUrl(stored)
            return defaultEnvironmentUrl()
        }

    var isOfflineMode: Boolean
        get() = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)?.getBoolean(KEY_OFFLINE_MODE, false) ?: false
        set(value) {
            appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
                ?.edit()?.putBoolean(KEY_OFFLINE_MODE, value)?.apply()
        }

    fun selectEnvironment(environment: ApiEnvironment) {
        require(environment != ApiEnvironment.CUSTOM) { "Custom requires setBaseUrl" }
        val url = when (environment) {
            ApiEnvironment.DEVELOPMENT -> DEVELOPMENT_URL
            ApiEnvironment.PRODUCTION -> PRODUCTION_URL
            ApiEnvironment.CUSTOM -> error("unreachable")
        }
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        val normalized = normalizeBaseUrl(url)
        prefs.edit()
            .putString(KEY_BASE_URL, normalized)
            .putString(KEY_ENVIRONMENT, environment.storageValue)
            .apply()
        ConnectionHealthStore.updateBaseUrl(normalized)
    }

    /** Administrator diagnostic escape hatch. UI code should label this Custom and never present
     * it as Production; it is retained for existing local-network troubleshooting workflows. */
    fun setBaseUrl(rawUrl: String) {
        val normalized = normalizeBaseUrl(rawUrl)
        val prefs = appContext?.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE) ?: return
        prefs.edit()
            .putString(KEY_BASE_URL, normalized)
            .putString(KEY_ENVIRONMENT, inferEnvironment(normalized).storageValue)
            .apply()
        ConnectionHealthStore.updateBaseUrl(normalized)
    }

    fun buildUrlWithPort(port: Int): String {
        val uri = try { Uri.parse(apiBaseUrl) } catch (_: Exception) { Uri.parse(defaultEnvironmentUrl()) }
        val scheme = uri.scheme ?: "http"
        val host = uri.host ?: "localhost"
        return "$scheme://$host:$port/"
    }

    private fun inferEnvironment(rawUrl: String?): ApiEnvironment {
        val normalized = rawUrl?.let(::normalizeBaseUrl)
        return when {
            normalized == normalizeBaseUrl(DEVELOPMENT_URL) -> ApiEnvironment.DEVELOPMENT
            normalized == normalizeBaseUrl(PRODUCTION_URL) -> ApiEnvironment.PRODUCTION
            else -> ApiEnvironment.CUSTOM
        }
    }

    private fun defaultEnvironmentUrl(): String {
        return when {
            BuildConfig.FLAVOR.equals("prod", ignoreCase = true) -> normalizeBaseUrl(PRODUCTION_URL)
            BuildConfig.FLAVOR.equals("dev", ignoreCase = true) -> normalizeBaseUrl(DEVELOPMENT_URL)
            else -> normalizeBaseUrl(BuildConfig.API_BASE_URL)
        }
    }

    private fun normalizeBaseUrl(url: String): String {
        var trimmed = url.trim()
        if (!trimmed.contains("://")) trimmed = "http://$trimmed"
        if (!trimmed.endsWith("/")) trimmed += "/"
        return trimmed
    }
}
