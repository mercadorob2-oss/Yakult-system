package com.example.yakultscanner

enum class ScanMode {
    None,
    Dispatch,
    Serial
}

object ScanRouting {
    @Volatile
    var mode: ScanMode = ScanMode.None
        internal set

    @Volatile
    var currentRoute: String? = null
        internal set

    @Volatile
    var honeywellArmed: Boolean = false
        internal set

    private fun baseRoute(route: String?): String? {
        if (route.isNullOrBlank()) return null
        return route.substringBefore('?').substringBefore('/')
    }

    internal fun computeMode(current: String?, previous: String?): ScanMode {
        return when (baseRoute(current)) {
            "direct_serial_scan", "local_serial_scan_report", "batch_serial_entry", "serial_set_identifier", "transmittal_scan", "gatepass_scan" -> ScanMode.Serial
            "home", "details", "details_readonly", "dispatch_item_details" -> ScanMode.Dispatch
            "scanner_device", "scanner_camera" -> {
                val prev = baseRoute(previous)
                if (prev == "direct_serial_scan" || prev == "local_serial_scan_report" || prev == "batch_serial_entry" || prev == "serial_set_identifier" || prev == "transmittal_scan" || prev == "gatepass_scan") {
                    ScanMode.Serial
                } else {
                    ScanMode.Dispatch
                }
            }
            else -> ScanMode.None
        }
    }

    internal fun computeHoneywellArmed(current: String?): Boolean {
        return when (baseRoute(current)) {
            "scanner_device",
            "scanner_camera",
            "direct_serial_scan",
            "local_serial_scan_report",
            "batch_serial_entry",
            "serial_set_identifier",
            "transmittal_scan",
            "gatepass_scan" -> true
            else -> false
        }
    }

    fun updateFromRoutes(current: String?, previous: String?) {
        currentRoute = current
        mode = computeMode(current, previous)
        honeywellArmed = computeHoneywellArmed(current)
    }
}


