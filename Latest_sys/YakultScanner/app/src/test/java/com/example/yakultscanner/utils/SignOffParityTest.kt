package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallTicketHistoryDto
import com.example.yakultscanner.api.CallTicketNoteDto
import com.example.yakultscanner.api.CallFieldVisitDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SignOffParityTest {

    private fun history(
        field: String?,
        old: String?,
        new: String?,
        by: String? = "Tech",
        at: String? = "2026-09-01 10:00:00"
    ) = CallTicketHistoryDto(
        historyId = 0,
        fieldName = field,
        oldValue = old,
        newValue = new,
        changedBy = by,
        changedAt = at
    )

    // ── Timeline filter (desktop BuildReportModelAsync rules) ──

    @Test
    fun timelineKeepsStatusField() {
        val out = SignOffParity.filterTimeline(
            listOf(history("Status", "Pending", "Solved"))
        )
        assertEquals(1, out.rows.size)
        assertEquals("Pending → Solved", out.rows[0].status)
        assertEquals("(Tech)", out.rows[0].details)
        assertEquals(1, out.totalCount)
    }

    @Test
    fun timelineKeepsKeyTransitionOnNonKeyField() {
        val out = SignOffParity.filterTimeline(
            listOf(history("AssignedTo", "In Progress", "Completed"))
        )
        assertEquals(1, out.rows.size)
    }

    @Test
    fun timelineDropsNonKeyChange() {
        val out = SignOffParity.filterTimeline(
            listOf(history("Priority", "High", "Critical"))
        )
        assertTrue(out.rows.isEmpty())
        assertEquals(1, out.totalCount)
    }

    @Test
    fun timelineDropsBlankDigitAndShortNoise() {
        val out = SignOffParity.filterTimeline(
            listOf(
                history("Status", "", ""),
                // Key-field transitions survive the noise checks (desktop keeps
                // "139 → 1412" on Status too); noise must be on other fields.
                history("Priority", "139", "1412"),
                history("Priority", "AB", "")
            )
        )
        assertTrue(out.rows.isEmpty())
    }

    @Test
    fun timelineDedupsCaseInsensitivelyAndCapsAt15() {
        val items = mutableListOf<CallTicketHistoryDto>()
        repeat(20) { i ->
            items += history("Status", "Pending", "Solved$i")
        }
        items += history("status", "pending", "solved0")
        val out = SignOffParity.filterTimeline(items)
        assertEquals(SignOffParity.TIMELINE_MAX_ROWS, out.rows.size)
        assertEquals(21, out.totalCount)
    }

    @Test
    fun timelineSingleSidedValues() {
        val onlyNew = SignOffParity.filterTimeline(listOf(history("Status", "", "Solved")))
        assertEquals("Solved", onlyNew.rows[0].status)
        val onlyOld = SignOffParity.filterTimeline(listOf(history("Status", "Solved", "")))
        assertEquals("Solved", onlyOld.rows[0].status)
    }

    // ── Resolution type label ──

    @Test
    fun resolutionTypeLabelMatrix() {
        val svc = listOf(history("ResolutionType", "", "Service Only"))
        val rep = listOf(history("ResolutionType", "", "Replacement"))
        assertEquals(
            "TEMPORARY SERVICE",
            SignOffParity.resolutionTypeLabel("Resolved (Temporary)", svc)
        )
        assertEquals(
            "TEMPORARY",
            SignOffParity.resolutionTypeLabel("Resolved (Temporary)", emptyList())
        )
        assertEquals(
            "TEMPORARY REPLACEMENT",
            SignOffParity.resolutionTypeLabel("Resolved (Temporary)", rep)
        )
        assertEquals("SERVICE ONLY", SignOffParity.resolutionTypeLabel("Solved", emptyList()))
        assertEquals(
            "SERVICE ONLY",
            SignOffParity.resolutionTypeLabel("Solved", svc)
        )
    }

    // ── Humanize + meta ordering ──

    @Test
    fun humanizeResolutionLines() {
        val out = SignOffParity.humanizeResolutionLines(
            listOf(
                "ResolutionResponsiblePerson: JUAN DELA CRUZ",
                "ResolutionDepartment: INFORMATION TECHNOLOGY",
                "ResolutionType: Service Only",
                "",
                "ResolutionResponsiblePerson:"
            )
        )
        assertEquals(
            listOf(
                "Responsible Person: Juan Dela Cruz",
                // Non-person values keep their case (desktop title-cases
                // person/responsible values only).
                "Department: INFORMATION TECHNOLOGY",
                "Type: Service Only",
                // Key-only survivor: desktop keeps the bare key when the
                // value is blank (val blank -> s = key).
                "Responsible Person"
            ),
            out
        )
    }

    @Test
    fun resolutionMetaOrdersPersonDeptType() {
        val history = listOf(
            history("ResolutionType", "", "Service Only"),
            history("ResolutionDepartment", "", "IT"),
            history("ResolutionResponsiblePerson", "", "JUAN")
        )
        assertEquals(
            listOf("Responsible Person: Juan", "Department: IT", "Type: Service Only"),
            SignOffParity.resolutionMeta(history, emptyList())
        )    }

    @Test
    fun buildResolutionBodyJoinsSolutionAndMeta() {
        val history = listOf(history("ResolutionDepartment", "", "IT"))
        assertEquals(
            "Replaced toner  •  Department: IT",
            SignOffParity.buildResolutionBody("Replaced toner", history, emptyList())
        )
        assertEquals("—", SignOffParity.buildResolutionBody(" ", emptyList(), emptyList()))
    }

    @Test
    fun splitCamel() {
        assertEquals("Responsible Person", SignOffParity.splitCamel("ResponsiblePerson"))
        assertEquals("Old Item", SignOffParity.splitCamel("OldItem"))
    }

    // ── Notes + dates ──

    @Test
    fun notesLinesFormat() {
        val out = SignOffParity.notesLines(
            listOf(
                CallTicketNoteDto(
                    noteId = 1,
                    noteType = "Resolution",
                    noteText = "Fixed",
                    createdBy = "Tech",
                    createdAt = "2026-09-02 15:04:05"
                )
            )
        )
        assertEquals(1, out.size)
        assertTrue(out[0].startsWith("[Resolution] Fixed (Tech, "))
    }

    @Test
    fun dateFormats() {
        assertEquals(
            "Sep 2, 2026 3:04 PM",
            SignOffParity.formatDateTime("2026-09-02 15:04:05")
        )
        assertEquals("Sep 2, 2026", SignOffParity.formatDate("2026-09-02"))
        assertEquals("Sep 2, 3:04 PM", SignOffParity.formatNoteDate("2026-09-02 15:04:05"))
        assertEquals("not-a-date", SignOffParity.formatDateTime("not-a-date"))
        assertEquals("-", SignOffParity.formatDateTime(" "))
    }

    @Test
    fun displayPersonName() {
        assertEquals("Juan Dela Cruz", SignOffParity.displayPersonName("JUAN DELA CRUZ"))
        assertEquals("—", SignOffParity.displayPersonName("  "))
        assertEquals("—", SignOffParity.displayPersonName(null))
    }

    // ── Eligibility (desktop CanSignOffQuick minus login/temp-return) ──

    private fun visit(status: String, signed: Boolean) = CallFieldVisitDto(
        fieldVisitId = 1,
        ticketId = 1,
        status = status,
        hasSignature = signed,
        technicianName = "Tech"
    )

    @Test
    fun eligibilityMatrix() {
        assertTrue(SignOffParity.isSignOffEligible("Solved", listOf(visit("Completed", true))))
        assertTrue(SignOffParity.isSignOffEligible("Closed", listOf(visit("Completed", true))))
        assertTrue(
            SignOffParity.isSignOffEligible("Resolved (Temporary)", listOf(visit("Completed", true)))
        )
        assertFalse(SignOffParity.isSignOffEligible("Pending", listOf(visit("Completed", true))))
        assertFalse(SignOffParity.isSignOffEligible("Solved", emptyList()))
        assertFalse(SignOffParity.isSignOffEligible("Solved", listOf(visit("Scheduled", true))))
        assertFalse(SignOffParity.isSignOffEligible("Solved", listOf(visit("Completed", false))))
        assertFalse(
            SignOffParity.isSignOffEligible(
                "Solved",
                listOf(visit("Completed", true), visit("Scheduled", false))
            )
        )
    }
}
