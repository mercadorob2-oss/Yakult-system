package com.example.yakultscanner

import android.content.Context

object PinnedSetsStore {
    private const val PREFS_NAME = "yakult_scanner_pins"
    private const val KEY_PINNED = "pinned_set_codes"

    fun load(context: Context): Set<String> {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        return prefs.getStringSet(KEY_PINNED, emptySet())?.toSet() ?: emptySet()
    }

    fun isPinned(context: Context, setCode: String): Boolean {
        val normalized = normalize(setCode)
        if (normalized.isBlank()) return false
        return load(context).contains(normalized)
    }

    fun toggle(context: Context, setCode: String): Set<String> {
        val normalized = normalize(setCode)
        if (normalized.isBlank()) return load(context)

        val current = load(context).toMutableSet()
        if (!current.add(normalized)) current.remove(normalized)

        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        prefs.edit().putStringSet(KEY_PINNED, current).apply()
        return current.toSet()
    }

    fun clear(context: Context) {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        prefs.edit().remove(KEY_PINNED).apply()
    }

    private fun normalize(setCode: String): String {
        return setCode.trim().uppercase()
    }
}
