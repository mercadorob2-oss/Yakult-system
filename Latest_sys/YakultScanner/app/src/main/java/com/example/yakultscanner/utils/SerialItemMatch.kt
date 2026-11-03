package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallItemLookupDto

sealed interface FetchAssetResult {
    data class Found(val item: CallItemLookupDto) : FetchAssetResult
    data class Ambiguous(val items: List<CallItemLookupDto>) : FetchAssetResult
    data class MissingFixedAsset(val item: CallItemLookupDto) : FetchAssetResult
    data object NotFound : FetchAssetResult
}

private fun normalizedItemSerial(item: CallItemLookupDto): String =
    normalizeSerial(item.serialNumber)

/**
 * Partial serial search used by the replacement screen. Serial numbers are compared using the
 * same normalization as manual entry and scanner input (trim, remove internal whitespace,
 * uppercase), so a typed or scanned value finds the same inventory row.
 */
fun serialMatches(items: List<CallItemLookupDto>, query: String): List<CallItemLookupDto> {
    val needle = normalizeSerial(query)
    if (needle.isEmpty()) return emptyList()

    return items
        .filter { item ->
            val serial = normalizedItemSerial(item)
            serial.isNotEmpty() && serial.contains(needle)
        }
        .sortedWith(compareBy({ normalizedItemSerial(it) }, { it.displayText }))
}

/** Exact serial match, case and whitespace insensitive. Returns every duplicate match. */
fun exactSerialMatches(items: List<CallItemLookupDto>, query: String): List<CallItemLookupDto> {
    val needle = normalizeSerial(query)
    if (needle.isEmpty()) return emptyList()

    return items.filter { normalizedItemSerial(it) == needle }
}

/**
 * Resolves the fixed asset number / description for a serial. Mirrors the desktop
 * "Fetch by serial" behavior: the serial must identify exactly one inventory item, and that
 * item must have a non-blank description.
 */
fun resolveFetchAsset(items: List<CallItemLookupDto>, serial: String): FetchAssetResult {
    val matches = exactSerialMatches(items, serial).distinctBy { it.itemId }
    return when {
        matches.isEmpty() -> FetchAssetResult.NotFound
        matches.size > 1 -> FetchAssetResult.Ambiguous(matches)
        matches[0].fixedAssetNumber.isNullOrBlank() -> FetchAssetResult.MissingFixedAsset(matches[0])
        else -> FetchAssetResult.Found(matches[0])
    }
}
