package com.example.yakultscanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import com.example.yakultscanner.utils.normalizeSerial

const val HONEYWELL_SCAN_ACTION = "com.example.yakultscanner.SCAN"
const val HONEYWELL_DEFAULT_ACTION = "com.honeywell.decode.intent.action.DECODE_DATA"
const val HONEYWELL_SCAN_DATA_EXTRA = "data"

class HoneywellScanReceiver : BroadcastReceiver() {

    companion object {
        private var lastScanAtMs: Long = 0L
        private const val MAX_PAYLOAD_CHARS: Int = 20_000

        private fun sanitizePayload(raw: String): String? {
            val cleaned = buildString(raw.length.coerceAtMost(MAX_PAYLOAD_CHARS)) {
                for (ch in raw) {
                    if (length >= MAX_PAYLOAD_CHARS) break
                    if (!ch.isISOControl()) append(ch)
                }
            }.trim()

            return cleaned.takeIf { it.isNotEmpty() }
        }

        private fun previewForLog(value: String): String {
            val trimmed = value.trim()
            return if (trimmed.length <= 64) trimmed else trimmed.take(64) + "…(${trimmed.length})"
        }
    }

    override fun onReceive(context: Context, intent: Intent) {
        val action = intent.action
        if (action != HONEYWELL_SCAN_ACTION && action != HONEYWELL_DEFAULT_ACTION) return

        val raw = intent.getStringExtra(HONEYWELL_SCAN_DATA_EXTRA)
            ?: intent.getStringExtra("barcode_data")
            ?: intent.getStringExtra("data_string")
            ?: return

        val scannedValue = sanitizePayload(raw) ?: return

        val navController = GlobalNav.navController ?: return
        val currentRoute = navController.currentBackStackEntry?.destination?.route
        val previousRoute = navController.previousBackStackEntry?.destination?.route

        val now = System.currentTimeMillis()
        if (now - lastScanAtMs < 1200L) return
        lastScanAtMs = now

        // Tighten receiver: only accept scans while user is explicitly in scanner flows.
        val derivedMode = ScanRouting.computeMode(currentRoute, previousRoute)
        val isArmed = ScanRouting.computeHoneywellArmed(currentRoute)
        if (!isArmed) return

        Log.d(
            "HoneywellScanReceiver",
            "Received scan intent action=$action route=$currentRoute mode=$derivedMode value=${previewForLog(scannedValue)}"
        )

        // Prevent scans from hijacking the UI outside intended contexts.
        when (derivedMode) {
            ScanMode.None -> return
            ScanMode.Serial -> {
                val normalized = normalizeSerial(scannedValue)
                if (normalized.isEmpty()) return

                val isScannerScreen = currentRoute == "scanner_device" || currentRoute == "scanner_camera"
                if (isScannerScreen) {
                    navController.currentBackStackEntry?.savedStateHandle?.set(
                        SCAN_SCREEN_EVENT_KEY,
                        normalized
                    )
                    return
                }

                navController.currentBackStackEntry?.savedStateHandle?.set(
                    SCAN_RESULT_SERIAL_KEY,
                    normalized
                )
                return
            }
            ScanMode.Dispatch -> {
                // Only allow auto-navigation while user is in the dispatch scanning flow.
                val allowAutoNavigate = currentRoute == "scanner_device" || currentRoute == "scanner_camera"
                if (!allowAutoNavigate) return
                navController.currentBackStackEntry?.savedStateHandle?.set(
                    SCAN_SCREEN_EVENT_KEY,
                    scannedValue
                )
                return
            }
        }
    }
}
