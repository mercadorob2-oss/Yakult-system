package com.example.yakultscanner.utils

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import com.example.yakultscanner.api.RepairReportPreviewResponse
import com.example.yakultscanner.api.RepairReportPreviewTicketDto
import com.example.yakultscanner.api.RepairReportSignatureDto
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

object RepairReportPdfGenerator {

    private const val PAGE_W = 595 // A4 72dpi
    private const val PAGE_H = 842
    private const val MARGIN = 36
    private const val CONTENT_W = PAGE_W - MARGIN * 2

    fun generate(
        context: Context,
        preview: RepairReportPreviewResponse,
        reviewedBy: RepairReportSignatureDto? = null,
        receivedBy: RepairReportSignatureDto? = null
    ): File {
        val document = PdfDocument()
        val paintTitle = Paint().apply { color = Color.parseColor("#1e293b"); textSize = 13f; isFakeBoldText = true; isAntiAlias = true }
        val paintSubtitle = Paint().apply { color = Color.parseColor("#64748b"); textSize = 9f; isAntiAlias = true }
        val paintHeader = Paint().apply { color = Color.parseColor("#1e293b"); textSize = 11f; isFakeBoldText = true; isAntiAlias = true }
        val paintBody = Paint().apply { color = Color.parseColor("#334155"); textSize = 9f; isAntiAlias = true }
        val paintBodyBold = Paint().apply { color = Color.parseColor("#0f172a"); textSize = 10f; isFakeBoldText = true; isAntiAlias = true }
        val paintMuted = Paint().apply { color = Color.parseColor("#64748b"); textSize = 8f; isAntiAlias = true }
        val paintLine = Paint().apply { color = Color.parseColor("#1e293b"); strokeWidth = 1.2f }
        val paintLineLight = Paint().apply { color = Color.parseColor("#E2E8F0"); strokeWidth = 0.8f }

        var pageInfo = PdfDocument.PageInfo.Builder(PAGE_W, PAGE_H, 1).create()
        var page = document.startPage(pageInfo)
        var canvas = page.canvas
        var y = MARGIN.toFloat()
        var pageNumber = 1

        fun ensureSpace(needed: Float) {
            if (y + needed > PAGE_H - MARGIN) {
                document.finishPage(page)
                pageNumber++
                pageInfo = PdfDocument.PageInfo.Builder(PAGE_W, PAGE_H, pageNumber).create()
                page = document.startPage(pageInfo)
                canvas = page.canvas
                y = MARGIN.toFloat()
                // page header repeat
                drawPageHeader(canvas, paintSubtitle, paintMuted, y)
                y += 22f
                canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight)
                y += 8f
            }
        }

        // First page header
        drawPageHeader(canvas, paintSubtitle, paintMuted, y)
        y += 22f
        canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight)
        y += 8f

        // Title
        canvas.drawText("Repair Report", MARGIN.toFloat(), y + 12, Paint().apply { color = Color.parseColor("#2563EB"); textSize = 16f; isFakeBoldText = true; isAntiAlias = true })
        y += 20f
        val generatedLabel = "Generated ${formatDate(preview.generatedAt)} • ${preview.generatedByName ?: "Mobile"} • ${preview.ticketCount} ticket(s) in ${preview.groupCount} group(s)"
        y = drawWrapped(canvas, generatedLabel, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintMuted, 12f)

        y += 6f
        canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLine)
        y += 10f

        // Batch groups (if >1) else single
        val groups = if (preview.groups.isNotEmpty()) preview.groups else listOf(
            com.example.yakultscanner.api.RepairReportGroupDto(groupLabel = "Report", ticketCount = preview.reports.size, ticketCodes = preview.reports.mapNotNull { it.ticketCode }, tickets = preview.reports)
        )

        for ((gi, group) in groups.withIndex()) {
            ensureSpace(60f)
            // Group banner
            val bannerPaint = Paint().apply { color = Color.parseColor("#EFF6FF") }
            val bannerH = 22f
            canvas.drawRect(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y + bannerH, bannerPaint)
            canvas.drawText(
                if (groups.size > 1) "Requester: ${group.groupLabel ?: "Unspecified"}  —  ${group.ticketCount} ticket(s): ${group.ticketCodes.joinToString(", ")}"
                else "Ticket: ${group.ticketCodes.firstOrNull() ?: ""}",
                MARGIN.toFloat() + 8, y + 14, paintHeader
            )
            y += bannerH + 8f

            for ((ti, ticket) in group.tickets.withIndex()) {
                y = drawTicketBlock(canvas, ticket, paintTitle, paintSubtitle, paintHeader, paintBody, paintBodyBold, paintMuted, paintLine, paintLineLight, y, ::ensureSpace, gi, ti, group.tickets.size)
                // signature block only once per group, after last ticket
                if (ti == group.tickets.lastIndex) {
                    ensureSpace(90f)
                    y = drawSignatures(canvas, preview.generatedByName, reviewedBy, receivedBy, paintHeader, paintBody, paintMuted, paintLine, y)
                }
                // page break between groups
                if (gi != groups.lastIndex || ti != group.tickets.lastIndex) {
                    ensureSpace(14f)
                    // light separator between tickets within same group
                    if (ti != group.tickets.lastIndex) {
                        canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight)
                        y += 10f
                    } else if (gi != groups.lastIndex) {
                        // force new page for next group if not enough space for next banner
                        ensureSpace(80f)
                    }
                }
            }
        }

        // Footer on last page
        val footerY = PAGE_H - MARGIN + 14f
        canvas.drawText("Yakult Philippines Inc. — Information Technology Dept. • Page $pageNumber", MARGIN.toFloat(), footerY, paintMuted)

        document.finishPage(page)

        val outFile = File(context.cacheDir, "RepairReport-${SimpleDateFormat("yyyyMMdd-HHmmss", Locale.US).format(Date())}.pdf")
        outFile.parentFile?.mkdirs()
        document.writeTo(outFile.outputStream())
        document.close()
        return outFile
    }

    private fun drawPageHeader(canvas: Canvas, paintSubtitle: Paint, paintMuted: Paint, y: Float) {
        canvas.drawText("Yakult Philippines Inc.", MARGIN.toFloat(), y + 10, Paint().apply { color = Color.parseColor("#DC2626"); textSize = 12f; isFakeBoldText = true; isAntiAlias = true })
        canvas.drawText("Information Technology Dept.", MARGIN.toFloat(), y + 20, paintSubtitle)
        val rightX = PAGE_W - MARGIN - 200
        canvas.drawText("Mobile Repair Portal", rightX.toFloat(), y + 10, paintMuted)
        canvas.drawText(SimpleDateFormat("MMM d, yyyy", Locale.US).format(Date()), rightX.toFloat(), y + 20, paintMuted)
    }

    private fun drawTicketBlock(
        canvas: Canvas,
        ticket: RepairReportPreviewTicketDto,
        paintTitle: Paint,
        paintSubtitle: Paint,
        paintHeader: Paint,
        paintBody: Paint,
        paintBodyBold: Paint,
        paintMuted: Paint,
        paintLine: Paint,
        paintLineLight: Paint,
        startY: Float,
        ensureSpace: (Float) -> Unit,
        gi: Int, ti: Int, ticketsInGroup: Int
    ): Float {
        var y = startY
        ensureSpace(30f)
        // Item title bar
        val titleText = if (ticketsInGroup > 1) "Item ${ti + 1} — ${ticket.itemName ?: "Unknown"} (${ticket.ticketCode ?: ticket.repairTicketId})"
        else "${ticket.itemName ?: "Unknown"} (${ticket.ticketCode ?: ticket.repairTicketId})"
        canvas.drawText(titleText, MARGIN.toFloat(), y + 12, paintTitle)
        y += 16f
        canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, Paint().apply { color = Color.parseColor("#2563EB"); strokeWidth = 1.5f })
        y += 8f

        // Asset Information — paired two per row like desktop AssetL1-R4 (but mobile just lists non-empty)
        ensureSpace(14f)
        canvas.drawText("ASSET INFORMATION", MARGIN.toFloat(), y + 10, paintHeader)
        y += 14f
        canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight)
        y += 4f
        val assetFields = ticket.assetFields.filter { it.isNotBlank() }
        if (assetFields.isEmpty()) {
            y = drawWrapped(canvas, "No asset details", MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintMuted, 12f)
        } else {
            // two columns
            val colW = CONTENT_W / 2f - 6f
            var col = 0
            var colYLeft = y
            var colYRight = y
            for (field in assetFields) {
                val isLeft = col % 2 == 0
                val x = if (isLeft) MARGIN.toFloat() else MARGIN + colW + 12
                val baseY = if (isLeft) colYLeft else colYRight
                ensureSpace(14f)
                val newY = drawWrapped(canvas, field, x, baseY, colW, paintBody, 12f)
                if (isLeft) colYLeft = newY else colYRight = newY
                col++
            }
            y = maxOf(colYLeft, colYRight) + 4f
        }

        // Reported Problem — only if has
        if (ticket.hasReportedProblem && !ticket.problemText.isNullOrBlank()) {
            ensureSpace(24f)
            canvas.drawText("REPORTED PROBLEM", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            y = drawWrapped(canvas, ticket.problemText!!, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBodyBold, 12f) + 4f
        }

        // Repair Details — compact always (status + technician + reported + completed + duration)
        ensureSpace(24f)
        canvas.drawText("REPAIR DETAILS", MARGIN.toFloat(), y + 10, paintHeader)
        y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
        val details = buildDetailsText(ticket)
        y = drawWrapped(canvas, details, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f

        if (ticket.hasWorkPerformed && !ticket.conclusion?.workPerformed.isNullOrBlank()) {
            ensureSpace(24f)
            canvas.drawText("WORK PERFORMED", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            y = drawWrapped(canvas, ticket.conclusion!!.workPerformed!!.trim(), MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f
        }

        if (ticket.hasParts && ticket.parts.isNotEmpty()) {
            ensureSpace(24f)
            canvas.drawText("PARTS", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            val partsText = ticket.parts.joinToString("\n") { "${it.displayName ?: "Part"}: ${mapPartStatus(it.status)}" }
            y = drawWrapped(canvas, partsText, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f
        }

        if (ticket.hasDiagnosis) {
            ensureSpace(24f)
            canvas.drawText("DIAGNOSIS & RESOLUTION", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            val diag = buildDiagnosisText(ticket)
            if (diag.isNotBlank()) y = drawWrapped(canvas, diag, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f
        }

        // Item disposition (if spare/replacement) — mapped from conclusion.disposition
        val dispositionText = buildDispositionText(ticket)
        if (dispositionText.isNotBlank()) {
            ensureSpace(24f)
            canvas.drawText("ITEM DISPOSITION", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            y = drawWrapped(canvas, dispositionText, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f
        }

        if (ticket.hasRecommendations && !ticket.conclusion?.recommendations.isNullOrBlank()) {
            ensureSpace(24f)
            canvas.drawText("RECOMMENDATIONS", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            y = drawWrapped(canvas, ticket.conclusion!!.recommendations!!.trim(), MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintBody, 12f) + 4f
        }

        if (ticket.hasAttachments && ticket.attachments.isNotEmpty()) {
            ensureSpace(24f)
            canvas.drawText("ATTACHMENTS  (${ticket.attachments.size} file(s))", MARGIN.toFloat(), y + 10, paintHeader)
            y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLineLight); y += 4f
            val attachText = ticket.attachments.take(6).joinToString("\n") { "• ${it.fileName ?: "Evidence"}  (${it.source ?: ""})  ${it.fileSizeBytes?.let { sz -> "${sz/1024} KB" } ?: ""}" }
            y = drawWrapped(canvas, attachText, MARGIN.toFloat(), y, CONTENT_W.toFloat(), paintMuted, 11f) + 4f
        }

        return y + 6f
    }

    private fun buildDetailsText(t: RepairReportPreviewTicketDto): String {
        val lines = mutableListOf<String>()
        lines.add("Status: ${t.status ?: ""}")
        if (!t.assignedTechnicianName.isNullOrBlank()) lines.add("Technician: ${t.assignedTechnicianName}")
        lines.add("Reported: ${formatDate(t.createdAt)}")
        if (!t.completedAt.isNullOrBlank()) {
            lines.add("Completed: ${formatDate(t.completedAt)}")
            lines.add("Duration: ${formatDuration(t.createdAt, t.completedAt)}")
        }
        if (!t.priority.isNullOrBlank()) lines.add("Priority: ${t.priority}")
        if (!t.dateReceived.isNullOrBlank()) lines.add("Date Received: ${t.dateReceived}")
        return lines.joinToString("\n")
    }

    private fun buildDiagnosisText(t: RepairReportPreviewTicketDto): String {
        val c = t.conclusion ?: return ""
        val lines = mutableListOf<String>()
        if (!c.rootCause.isNullOrBlank()) lines.add("Root Cause: ${c.rootCause!!.trim()}")
        if (!c.finalOutcome.isNullOrBlank()) lines.add("Resolution: ${c.finalOutcome!!.trim()}")
        return lines.joinToString("\n")
    }

    private fun buildDispositionText(t: RepairReportPreviewTicketDto): String {
        val c = t.conclusion ?: return ""
        val lines = mutableListOf<String>()
        if (c.disposition.equals("Replace", ignoreCase = true) && c.replacementItemId != null) lines.add("Item Replacement — Replacement Item #${c.replacementItemId}")
        // Spare is not in preview api yet (would need BorrowLog lookup) — show if disposition indicates
        if (!c.disposition.isNullOrBlank() && !c.disposition.equals("Replace", ignoreCase = true)) lines.add("Disposition: ${c.disposition}")
        return lines.joinToString("\n")
    }

    private fun drawSignatures(
        canvas: Canvas,
        generatedByName: String?,
        reviewedBy: RepairReportSignatureDto?,
        receivedBy: RepairReportSignatureDto?,
        paintHeader: Paint,
        paintBody: Paint,
        paintMuted: Paint,
        paintLine: Paint,
        startY: Float
    ): Float {
        var y = startY
        canvas.drawText("SIGNATURES", MARGIN.toFloat(), y + 10, paintHeader)
        y += 14f; canvas.drawLine(MARGIN.toFloat(), y, (PAGE_W - MARGIN).toFloat(), y, paintLine); y += 12f
        val colW = CONTENT_W / 3f - 8f
        val cols = listOf(
            Triple("Prepared By", generatedByName ?: "Mobile User", formatToday()),
            Triple("Reviewed By", reviewedBy?.employeeName ?: "__________________", reviewedBy?.title ?: ""),
            Triple("Received By", receivedBy?.employeeName ?: "__________________", receivedBy?.title ?: "")
        )
        val dates = listOf(formatToday(), reviewedBy?.signedDate ?: "Date: __________", receivedBy?.signedDate ?: "Date: __________")
        for (i in cols.indices) {
            val x = MARGIN + i * (colW + 12)
            val (role, name, _) = cols[i]
            // signature line
            canvas.drawLine(x, y + 18, x + colW, y + 18, paintLine)
            // name
            canvas.drawText(name.take(28), x + 4, y + 12, Paint(paintBody).apply { isFakeBoldText = true; textSize = 9f })
            // role
            canvas.drawText(role, x + 4, y + 30, paintMuted)
            // date
            canvas.drawText(dates[i].take(28), x + 4, y + 42, paintMuted)
        }
        return y + 54f
    }

    private fun drawWrapped(canvas: Canvas, text: String, x: Float, y: Float, maxWidth: Float, paint: Paint, lineH: Float): Float {
        if (text.isBlank()) return y
        val words = text.split(Regex("\\s+"))
        var line = StringBuilder()
        var curY = y
        for (w in words) {
            val test = if (line.isEmpty()) w else "$line $w"
            // handle explicit newlines in source
            if (w.contains("\n")) {
                val parts = w.split("\n")
                for ((idx, part) in parts.withIndex()) {
                    val trial = if (line.isEmpty()) part else "$line $part"
                    if (paint.measureText(trial) > maxWidth) {
                        if (line.isNotEmpty()) { canvas.drawText(line.toString(), x, curY + lineH, paint); curY += lineH }
                        line = StringBuilder(part)
                    } else line = StringBuilder(trial)
                    if (idx != parts.lastIndex) { canvas.drawText(line.toString(), x, curY + lineH, paint); curY += lineH; line = StringBuilder() }
                }
                continue
            }
            if (paint.measureText(test) > maxWidth) {
                if (line.isNotEmpty()) { canvas.drawText(line.toString(), x, curY + lineH, paint); curY += lineH }
                line = StringBuilder(w)
            } else {
                line = StringBuilder(test)
            }
        }
        if (line.isNotEmpty()) { canvas.drawText(line.toString(), x, curY + lineH, paint); curY += lineH }
        return curY
    }

    private fun formatDate(iso: String?): String {
        if (iso.isNullOrBlank()) return "—"
        return try {
            val inFmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }
            val out = SimpleDateFormat("MMM d, yyyy h:mm a", Locale.US)
            // try multiple patterns quickly
            val patterns = listOf("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd")
            var d: Date? = null
            for (p in patterns) { d = try { SimpleDateFormat(p, Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }.parse(iso) } catch (_: Exception) { null }; if (d != null) break }
            if (d != null) out.format(d) else iso
        } catch (_: Exception) { iso }
    }

    private fun formatDuration(startIso: String?, endIso: String?): String {
        if (startIso.isNullOrBlank() || endIso.isNullOrBlank()) return "—"
        return try {
            fun parse(s: String): Long? {
                val p = listOf("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss")
                for (fmt in p) try { return SimpleDateFormat(fmt, Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }.parse(s)?.time } catch (_: Exception) {}
                return null
            }
            val s = parse(startIso) ?: return "—"; val e = parse(endIso) ?: return "—"
            val diff = e - s; if (diff < 0) return "—"
            val days = diff / 86400000; val hrs = (diff % 86400000) / 3600000; val mins = (diff % 3600000) / 60000
            when { days > 0 -> "${days}d ${hrs}h" ; hrs > 0 -> "${hrs}h ${mins}m" ; mins > 0 -> "${mins}m" ; else -> "<1m" }
        } catch (_: Exception) { "—" }
    }

    private fun formatToday(): String = SimpleDateFormat("MMM d, yyyy", Locale.US).format(Date())
    private fun mapPartStatus(s: String?): String = when (s) {
        "Repaired" -> "Repaired"; "CannotRepair" -> "Not Repairable"; else -> "In Progress"
    }
}
