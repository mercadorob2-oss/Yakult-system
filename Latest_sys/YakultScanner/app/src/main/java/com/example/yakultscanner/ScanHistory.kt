package com.example.yakultscanner

import android.content.Context
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.example.yakultscanner.data.model.DispatchSet
import java.util.Locale

data class ScanHistoryEntry(
    val timestamp: Long,
    val rawJson: String,
    val setCode: String?,
    val employee: String?,
    val status: String?,
    val imageCount: Int? = null  // Number of images attached to this set
)

private const val HISTORY_FILE_NAME = "scan_history.json"
private const val HISTORY_MAX_SIZE = 50

private fun readScanHistoryEntries(context: Context): MutableList<ScanHistoryEntry> {
    val gson = Gson()
    val listType = object : TypeToken<MutableList<ScanHistoryEntry>>() {}.type
    val file = context.getFileStreamPath(HISTORY_FILE_NAME)

    return if (file != null && file.exists()) {
        try {
            context.openFileInput(HISTORY_FILE_NAME).use { input ->
                val json = input.bufferedReader().readText()
                if (json.isNotBlank()) gson.fromJson(json, listType) ?: mutableListOf() else mutableListOf()
            }
        } catch (_: Exception) {
            mutableListOf()
        }
    } else {
        mutableListOf()
    }
}

private fun writeScanHistoryEntries(context: Context, entries: List<ScanHistoryEntry>) {
    val jsonOut = Gson().toJson(entries)
    context.openFileOutput(HISTORY_FILE_NAME, Context.MODE_PRIVATE).use { output ->
        output.write(jsonOut.toByteArray())
    }
}

private fun rewriteDispatchSetStatus(rawJson: String, newStatus: String): String {
    return runCatching {
        val dispatchSet = Gson().fromJson(rawJson, DispatchSet::class.java)
        if (dispatchSet == null) {
            rawJson
        } else {
            Gson().toJson(dispatchSet.copy(status = newStatus))
        }
    }.getOrDefault(rawJson)
}

fun addScanToHistory(context: Context, rawJson: String, dispatchSet: DispatchSet?) {
    val existing = readScanHistoryEntries(context)

    val newSetCode = dispatchSet?.setCode
    if (!newSetCode.isNullOrBlank()) {
        existing.removeAll { it.setCode == newSetCode }
    } else {
        existing.removeAll { it.rawJson == rawJson }
    }

    val entry = ScanHistoryEntry(
        timestamp = System.currentTimeMillis(),
        rawJson = rawJson,
        setCode = dispatchSet?.setCode,
        employee = dispatchSet?.employee,
        status = dispatchSet?.status,
        imageCount = dispatchSet?.imageCount
    )

    existing.add(0, entry)
    if (existing.size > HISTORY_MAX_SIZE) {
        while (existing.size > HISTORY_MAX_SIZE) existing.removeLast()
    }

    writeScanHistoryEntries(context, existing)
}

fun updateScanHistoryStatus(context: Context, setCode: String?, newStatus: String) {
    val normalizedSetCode = setCode?.trim()
    val normalizedStatus = newStatus.trim()
    if (normalizedSetCode.isNullOrBlank() || normalizedStatus.isBlank()) {
        return
    }

    val existing = readScanHistoryEntries(context)
    if (existing.isEmpty()) {
        return
    }

    var changed = false
    val updated = existing.map { entry ->
        if (entry.setCode.equals(normalizedSetCode, ignoreCase = true)) {
            changed = true
            entry.copy(
                status = normalizedStatus,
                rawJson = rewriteDispatchSetStatus(entry.rawJson, normalizedStatus)
            )
        } else {
            entry
        }
    }

    if (changed) {
        writeScanHistoryEntries(context, updated)
    }
}

fun updateScanHistoryStatuses(context: Context, statusesBySetCode: Map<String, String>) {
    if (statusesBySetCode.isEmpty()) {
        return
    }

    val normalizedStatuses = statusesBySetCode
        .mapNotNull { (setCode, status) ->
            val normalizedSetCode = setCode.trim().takeIf { it.isNotEmpty() }
            val normalizedStatus = status.trim().takeIf { it.isNotEmpty() }
            if (normalizedSetCode == null || normalizedStatus == null) {
                null
            } else {
                normalizedSetCode.uppercase(Locale.getDefault()) to normalizedStatus
            }
        }
        .toMap()

    if (normalizedStatuses.isEmpty()) {
        return
    }

    val existing = readScanHistoryEntries(context)
    if (existing.isEmpty()) {
        return
    }

    var changed = false
    val updated = existing.map { entry ->
        val normalizedSetCode = entry.setCode?.trim()?.uppercase(Locale.getDefault())
        val newStatus = normalizedSetCode?.let { normalizedStatuses[it] }
        if (newStatus.isNullOrBlank()) {
            entry
        } else {
            val rewrittenRawJson = rewriteDispatchSetStatus(entry.rawJson, newStatus)
            if (!entry.status.equals(newStatus, ignoreCase = true) || rewrittenRawJson != entry.rawJson) {
                changed = true
                entry.copy(
                    status = newStatus,
                    rawJson = rewrittenRawJson
                )
            } else {
                entry
            }
        }
    }

    if (changed) {
        writeScanHistoryEntries(context, updated)
    }
}

fun loadScanHistory(context: Context): List<ScanHistoryEntry> {
    return readScanHistoryEntries(context)
}

fun clearScanHistory(context: Context): Boolean {
    return runCatching { context.deleteFile(HISTORY_FILE_NAME) }.getOrDefault(false)
}
