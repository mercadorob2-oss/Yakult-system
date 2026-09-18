package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallFieldVisitDto
import com.example.yakultscanner.api.CallTicketHistoryDto
import com.example.yakultscanner.api.CallTicketNoteDto
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

/**
 * 1:1 port of the desktop field-work Sign-Off rules
 * (WpfFieldWorkReportDetailDialog.BuildReportModelAsync +
 * FieldVisitCompletionPdfGenerator.HumanizeResolutionLines).
 *
 * Pure Kotlin/JVM only (no android.* imports) so it stays unit-testable.
 *
 * Known intentional deviations from desktop:
 * - Mobile history DTOs carry no per-event Note, so timeline Details is
 *   always "(ChangedBy)" (desktop renders "Note (ChangedBy)" when present).
 * - API date strings are parsed best-effort; unparseable values fall back
 *   to the raw string instead of throwing.
 * - No UTC conversion is applied (desktop converts server UTC to local);
 *   device-local rendering matches every other mobile screen.
 */
data class SignOffTimelineRow(
    val status: String,
    val details: String,
    val changedAtRaw: String?
)

data class FilteredTimeline(
    val rows: List<SignOffTimelineRow>,
    /** Total history events considered (pre-filter count), for "+N earlier" overflow. */
    val totalCount: Int
)

data class SignOffPhotoBytes(
    val fileName: String?,
    val uploadedBy: String?,
    val uploadedAt: String?,
    /** Raw base64 image (thumbnail preferred, full-size fallback). Decoded by the caller. */
    val bytes: String
)

data class TicketSignOffBundle(
    val visits: List<CallFieldVisitDto> = emptyList(),
    /** All attachments on file across visits (may exceed [thumbs]). */
    val totalPhotoCount: Int = 0,
    /** Up to 6 thumbnails, order preserved. */
    val thumbs: List<SignOffPhotoBytes> = emptyList(),
    /** Customer signature base64 for the current visit, null when absent. */
    val signatureBytes: String? = null,
    val currentVisit: CallFieldVisitDto? = null
)

object SignOffParity {

    const val TIMELINE_MAX_ROWS = 15
    const val EVIDENCE_MAX_PHOTOS = 6
    const val BUNDLE_MAX_THUMBS = 6

    private val keyFields = setOf("status", "fieldvisitstatus", "signoff", "fieldvisitsignature")
    private val statusWords = listOf("completed", "cancelled", "scheduled", "solved", "closed")
    private val finalStatuses = setOf("solved", "closed", "resolved (temporary)")

    private val parsePatterns = listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'" to true,
        "yyyy-MM-dd'T'HH:mm:ss'Z'" to true,
        "yyyy-MM-dd'T'HH:mm:ssXXX" to true,
        "yyyy-MM-dd'T'HH:mm:ss" to false,
        "yyyy-MM-dd HH:mm:ss" to false,
        "yyyy-MM-dd" to false,
        "M/d/yyyy h:mm a" to false,
        "M/d/yyyy" to false
    )

    private fun parseDate(raw: String?): Date? {
        val text = raw?.trim()
        if (text.isNullOrEmpty()) return null
        for ((pattern, isUtc) in parsePatterns) {
            try {
                val sdf = SimpleDateFormat(pattern, Locale.US).apply {
                    isLenient = false
                    if (isUtc) timeZone = TimeZone.getTimeZone("UTC")
                }
                return sdf.parse(text)
            } catch (_: Exception) {
                // try next pattern
            }
        }
        return null
    }

    /** "MMM d, yyyy h:mm a" (desktop grid format); blank input yields [fallback]. */
    fun formatDateTime(raw: String?, fallback: String = "-"): String {
        if (raw.isNullOrBlank()) return fallback
        val parsed = parseDate(raw) ?: return raw.trim()
        return SimpleDateFormat("MMM d, yyyy h:mm a", Locale.US).format(parsed)
    }

    /** "MMM d, yyyy" (desktop sign-off card format); blank input yields [fallback]. */
    fun formatDate(raw: String?, fallback: String = "-"): String {
        if (raw.isNullOrBlank()) return fallback
        val parsed = parseDate(raw) ?: return raw.trim()
        return SimpleDateFormat("MMM d, yyyy", Locale.US).format(parsed)
    }

    /** "MMM d, h:mm a" (desktop note format); blank input yields empty string. */
    fun formatNoteDate(raw: String?): String {
        if (raw.isNullOrBlank()) return ""
        val parsed = parseDate(raw) ?: return raw.trim()
        return SimpleDateFormat("MMM d, h:mm a", Locale.US).format(parsed)
    }

    /** "N day(s)" from a creation timestamp; unparseable yields "—". */
    fun ageLabel(createdAtRaw: String?): String {
        val parsed = parseDate(createdAtRaw) ?: return "—"
        val days = ((System.currentTimeMillis() - parsed.time) / 86_400_000L).coerceAtLeast(0)
        return "$days day" + if (days == 1L) "" else "s"
    }

    /**
     * Desktop timeline filter, in order: drop blank events, drop noise
     * (change text < 3 chars or pure digits), keep key fields or key
     * transitions, dedup case-insensitively, cap at [TIMELINE_MAX_ROWS].
     */
    fun filterTimeline(history: List<CallTicketHistoryDto>): FilteredTimeline {
        val rows = mutableListOf<SignOffTimelineRow>()
        val seen = HashSet<String>()
        for (h in history) {
            val old = h.oldValue?.trim().orEmpty()
            val new = h.newValue?.trim().orEmpty()
            // Desktop also requires a note here; mobile DTOs carry none.
            if (old.isBlank() && new.isBlank()) continue
            val change = when {
                old.isBlank() -> new.ifBlank { "Updated" }
                new.isBlank() -> old
                else -> "$old → $new"
            }
            val trimmed = change.trim()
            if (trimmed.length < 3) continue
            if (trimmed.matches(Regex("^\\d+$"))) continue
            val field = h.fieldName?.trim().orEmpty()
            val isKeyField = keyFields.contains(field.lowercase())
            val newIsStatusWord = statusWords.any { new.lowercase().contains(it) }
            val isKeyTransition = change.contains("→") && newIsStatusWord
            if (!isKeyField && !isKeyTransition) continue
            val rowStatus = when {
                new.isBlank() -> old
                old.isBlank() -> new
                else -> "$old → $new"
            }
            val by = h.changedBy?.trim().takeIf { !it.isNullOrEmpty() } ?: "System"
            val key = "$rowStatus|($by)".lowercase()
            if (!seen.add(key)) continue
            if (rows.size >= TIMELINE_MAX_ROWS) continue
            rows += SignOffTimelineRow(
                status = rowStatus,
                details = "($by)",
                changedAtRaw = h.changedAt
            )
        }
        return FilteredTimeline(rows = rows, totalCount = history.size)
    }

    /** Desktop ResolutionTypeLabel mapping. */
    fun resolutionTypeLabel(status: String?, history: List<CallTicketHistoryDto>): String {
        val tempNow = status?.trim().equals("Resolved (Temporary)", ignoreCase = true)
        val resVal = history.firstOrNull {
            it.fieldName?.trim().equals("ResolutionType", ignoreCase = true)
        }?.newValue?.trim().orEmpty()
        return when {
            tempNow && resVal.equals("Service Only", ignoreCase = true) -> "TEMPORARY SERVICE"
            tempNow && resVal.isBlank() -> "TEMPORARY"
            tempNow -> "TEMPORARY REPLACEMENT"
            resVal.isBlank() -> "SERVICE ONLY"
            else -> resVal.uppercase()
        }
    }

    /**
     * Desktop resolution meta: history Resolution* lines plus Resolution /
     * Replacement note lines, ordered responsible-person → department → type.
     */
    fun resolutionMeta(
        history: List<CallTicketHistoryDto>,
        notes: List<CallTicketNoteDto>
    ): List<String> {
        val raw = history
            .filter { it.fieldName?.startsWith("Resolution", ignoreCase = true) == true }
            .map { "${it.fieldName?.trim().orEmpty()}: ${it.newValue?.trim().orEmpty()}" }
            .toMutableList()
        raw += notes
            .filter {
                it.noteType.equals("Resolution", ignoreCase = true) ||
                    it.noteType.equals("Replacement", ignoreCase = true)
            }
            .map { "[${it.noteType}] ${(it.noteText ?: "").trim()}" }
        fun group(prefix: String) = humanizeResolutionLines(
            raw.filter { it.trimStart().startsWith(prefix, ignoreCase = true) }
        )
        return group("ResolutionResponsiblePerson") +
            group("ResolutionDepartment") +
            group("ResolutionType")
    }

    /** Desktop card body: solution + meta joined with "  •  ", or "—". */
    fun buildResolutionBody(
        solution: String?,
        history: List<CallTicketHistoryDto>,
        notes: List<CallTicketNoteDto>
    ): String {
        val parts = mutableListOf<String>()
        if (!solution.isNullOrBlank()) parts += solution.trim()
        parts += resolutionMeta(history, notes).filter { it.isNotBlank() }
        return if (parts.isEmpty()) "—" else parts.joinToString("  •  ")
    }

    /** Desktop note lines: "[Type] Text (By, MMM d, h:mm a)". */
    fun notesLines(notes: List<CallTicketNoteDto>): List<String> {
        return notes.map { n ->
            val by = n.createdBy?.trim().takeIf { !it.isNullOrEmpty() } ?: "—"
            val at = formatNoteDate(n.createdAt)
            val suffix = if (at.isEmpty()) "($by)" else "($by, $at)"
            "[${n.noteType?.trim().takeIf { !it.isNullOrEmpty() } ?: "Note"}] ${(n.noteText ?: "").trim()} $suffix"
        }.filter { it.isNotBlank() }
    }

    fun humanizeResolutionLines(src: List<String>): List<String> {
        val dst = mutableListOf<String>()
        for (raw in src) {
            if (raw.isBlank()) continue
            var s = raw.trim()
                .replace("[Resolution]", "Resolution")
                .replace("[Replacement]", "Replacement")
            if (s.startsWith("Resolution", ignoreCase = true)) {
                s = s.substring("Resolution".length).trimStart()
                if (s.startsWith(":")) s = s.substring(1).trimStart()
                if (s.isEmpty()) continue
            }
            val colon = s.indexOf(':')
            if (colon > 0) {
                val keyRaw = s.substring(0, colon).trim()
                var value = s.substring(colon + 1).trim()
                val key = splitCamel(keyRaw)
                if (key.contains("person", ignoreCase = true) ||
                    key.contains("responsible", ignoreCase = true)
                ) {
                    if (value.isNotBlank()) value = titleCase(value.lowercase())
                }
                if (key.isBlank() && value.isBlank()) continue
                s = if (value.isBlank()) key else "$key: $value"
            } else if (s.isEmpty()) continue
            dst += s
        }
        return dst
    }

    fun splitCamel(key: String): String {
        if (key.isEmpty()) return ""
        val sb = StringBuilder(key.length + 4).append(key[0])
        for (i in 1 until key.length) {
            if (key[i].isUpperCase() && key[i - 1].isLowerCase()) sb.append(' ')
            sb.append(key[i])
        }
        return sb.toString().trim()
    }

    private fun titleCase(lowercased: String): String {
        return lowercased.split(" ").joinToString(" ") { word ->
            word.replaceFirstChar { ch -> ch.uppercase() }
        }
    }

    fun displayPersonName(raw: String?): String {
        if (raw.isNullOrBlank()) return "—"
        return raw.trim().split(Regex("\\s+")).joinToString(" ") { word ->
            word.lowercase().replaceFirstChar { ch -> ch.uppercase() }
        }
    }

    fun selectCurrentVisit(visits: List<CallFieldVisitDto>): CallFieldVisitDto? =
        visits.firstOrNull()

    /**
     * Desktop CanSignOffQuick (minus login + temp-return checks, which need
     * other APIs): final status + a completed signed current visit + no
     * open Scheduled visit.
     */
    fun isSignOffEligible(status: String?, visits: List<CallFieldVisitDto>): Boolean {
        if (!finalStatuses.contains(status?.trim()?.lowercase())) return false
        val current = selectCurrentVisit(visits) ?: return false
        if (!current.status.equals("Completed", ignoreCase = true)) return false
        if (!current.hasSignature) return false
        if (visits.any { it.status.equals("Scheduled", ignoreCase = true) }) return false
        return true
    }
}
