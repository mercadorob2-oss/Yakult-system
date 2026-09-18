package com.example.yakultscanner.utils

/**
 * Normalizes serial numbers so they can be de-duped reliably across manual entry,
 * camera scans, and hardware scanner input.
 *
 * Rules:
 * - Trim leading/trailing whitespace
 * - Remove all internal whitespace
 * - Uppercase (locale-safe)
 */
fun normalizeSerial(raw: String?): String {
    val trimmed = (raw ?: "").trim()
    if (trimmed.isBlank()) return ""
    return trimmed
        .replace("\\s+".toRegex(), "")
        .uppercase(java.util.Locale.ROOT)
}

fun parseNormalizedSerials(raw: String?): List<String> {
    val input = raw.orEmpty()
    if (input.isBlank()) return emptyList()

    val parsed = input
        .split('\n', '\r', ',', ';', '\t')
        .map { normalizeSerial(it) }
        .filter { it.isNotEmpty() }

    if (parsed.isEmpty()) return emptyList()

    val deduped = LinkedHashSet<String>(parsed.size)
    parsed.forEach { deduped.add(it) }
    return deduped.toList()
}

