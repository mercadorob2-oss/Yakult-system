package com.example.yakultscanner

const val SCAN_RETURN_ROUTE_KEY = "returnRoute"
const val SCAN_RESULT_SERIAL_KEY = "scanned_serial"
const val SCAN_RESULT_SERIALS_KEY = "scanned_serials"
const val SCAN_SCREEN_EVENT_KEY = "scanner_live_scan"
const val SCAN_RETURN_SERIAL_ROUTE = "return_serial"

fun isSerialReturnRoute(returnRoute: String?): Boolean {
    return returnRoute == SCAN_RETURN_SERIAL_ROUTE
}
