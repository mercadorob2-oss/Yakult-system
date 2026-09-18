package com.example.yakultscanner.utils

import android.content.ContentValues
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Rect
import android.graphics.RectF
import android.graphics.Typeface
import android.graphics.pdf.PdfDocument
import android.util.Base64
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import android.widget.Toast
import androidx.core.content.FileProvider
import com.example.yakultscanner.api.BorrowLogDto
import com.example.yakultscanner.api.CallTicketDetail
import com.example.yakultscanner.api.CallTicketListItem
import com.example.yakultscanner.api.CallTicketHistoryDto
import com.example.yakultscanner.api.CallTicketNoteDto
import com.example.yakultscanner.api.ReportListRowDto
import com.example.yakultscanner.api.ReportsSummaryResponse
import com.example.yakultscanner.api.SetItemUpdateDto
import com.example.yakultscanner.data.db.PendingUpdateEntity
import com.example.yakultscanner.data.model.DispatchSet
import java.io.File
import java.io.FileOutputStream
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

fun generateAndOpenPdf(
    context: Context,
    set: DispatchSet,
    dispatcherBitmap: Bitmap?,
    requesterBitmap: Bitmap?
) {
    val document = PdfDocument()

    val pageWidth = 595
    val pageHeight = 842
    val margin = 32f
    val contentLeft = margin
    val contentRight = pageWidth - margin
    val contentWidth = contentRight - contentLeft
    val contentBottom = pageHeight - 42f

    val yakultRed = Color.rgb(184, 26, 31)
    val yakultDark = Color.rgb(97, 18, 20)
    val ink = Color.rgb(32, 37, 41)
    val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230)
    val softFill = Color.rgb(248, 249, 250)
    val cardFill = Color.WHITE
    val sealFill = Color.argb(44, 255, 255, 255)

    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textSize = 22f
        isFakeBoldText = true
    }
    val bannerLabelPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.argb(220, 255, 255, 255)
        textSize = 9f
        isFakeBoldText = true
    }
    val bannerValuePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textSize = 13f
        isFakeBoldText = true
    }
    val bannerPillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.argb(34, 255, 255, 255)
    }
    val bannerPillTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textSize = 8.2f
        isFakeBoldText = true
    }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = sealFill }
    val sealTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textSize = 25f
        isFakeBoldText = true
        textAlign = Paint.Align.CENTER
    }
    val sectionTitlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = yakultDark
        textSize = 11f
        isFakeBoldText = true
    }
    val cardLabelPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = muted
        textSize = 8.6f
        isFakeBoldText = true
    }
    val cardValuePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = ink
        textSize = 10.6f
    }
    val bodyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = ink
        textSize = 10.4f
    }
    val bodyBoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = ink
        textSize = 11f
        isFakeBoldText = true
    }
    val tableHeaderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = yakultDark
        textSize = 8.6f
        isFakeBoldText = true
    }
    val tableCellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = ink
        textSize = 9.2f
    }
    val tableCellBoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = ink
        textSize = 9.5f
        isFakeBoldText = true
    }
    val tableMetaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = muted
        textSize = 7.8f
    }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = muted
        textSize = 8.3f
    }
    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultRed }
    val cardFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = cardFill }
    val softFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softFill }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = softBorder
        style = Paint.Style.STROKE
        strokeWidth = 1f
    }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = softBorder
        strokeWidth = 1f
    }
    val tableHeaderFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(244, 236, 236)
    }
    val altRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(252, 252, 252)
    }
    val whiteRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }
    val dateFormatter = SimpleDateFormat("MMM dd, yyyy", Locale.US)
    val generatedAtFormatter = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US)
    val generatedAt = generatedAtFormatter.format(Date())

    fun safeText(value: String?, fallback: String = "N/A"): String {
        return value?.trim()?.takeIf { it.isNotEmpty() } ?: fallback
    }

    fun formatDisplayDate(value: String?, fallback: String = "N/A"): String {
        val raw = value?.trim().orEmpty()
        if (raw.isBlank()) return fallback

        val parsers = listOf(
            SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
                timeZone = TimeZone.getTimeZone("UTC")
            },
            SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply {
                timeZone = TimeZone.getTimeZone("UTC")
            },
            SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US),
            SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US),
            SimpleDateFormat("yyyy-MM-dd", Locale.US),
            SimpleDateFormat("M/d/yyyy", Locale.US)
        )

        for (parser in parsers) {
            try {
                parser.isLenient = false
                val parsed = parser.parse(raw) ?: continue
                return dateFormatter.format(parsed)
            } catch (_: Exception) {
            }
        }

        return raw
    }

    fun ellipsizeToWidth(text: String, paint: Paint, maxWidth: Float): String {
        val normalized = text.trim()
        if (normalized.isEmpty()) return ""
        if (paint.measureText(normalized) <= maxWidth) return normalized

        val ellipsis = "..."
        var lo = 0
        var hi = normalized.length
        while (lo < hi) {
            val mid = (lo + hi) / 2
            val candidate = normalized.substring(0, mid) + ellipsis
            if (paint.measureText(candidate) <= maxWidth) {
                lo = mid + 1
            } else {
                hi = mid
            }
        }

        val end = (lo - 1).coerceAtLeast(0)
        return if (end == 0) ellipsis else normalized.substring(0, end) + ellipsis
    }

    fun wrapToWidth(text: String, paint: Paint, maxWidth: Float, maxLines: Int): List<String> {
        val normalized = text.trim().replace("\n", " ")
        if (normalized.isEmpty()) return listOf("")

        val words = normalized.split(Regex("\\s+"))
        val lines = mutableListOf<String>()
        var current = ""

        for (word in words) {
            val candidate = if (current.isBlank()) word else "$current $word"
            if (paint.measureText(candidate) <= maxWidth) {
                current = candidate
            } else {
                if (current.isNotBlank()) {
                    lines += current
                    if (lines.size == maxLines) {
                        lines[lines.lastIndex] = ellipsizeToWidth(lines.last(), paint, maxWidth)
                        return lines
                    }
                }
                current = if (paint.measureText(word) <= maxWidth) {
                    word
                } else {
                    ellipsizeToWidth(word, paint, maxWidth)
                }
            }
        }

        if (current.isNotBlank() && lines.size < maxLines) {
            lines += current
        }

        if (lines.isEmpty()) {
            lines += ellipsizeToWidth(normalized, paint, maxWidth)
        }

        return lines.take(maxLines)
    }

    fun drawLabeledValue(
        canvas: Canvas,
        label: String,
        value: String,
        left: Float,
        top: Float,
        width: Float
    ): Float {
        canvas.drawText(label.uppercase(), left, top, cardLabelPaint)
        val lines = wrapToWidth(value, cardValuePaint, width, 2)
        var y = top + 14f
        for (line in lines) {
            canvas.drawText(line, left, y, cardValuePaint)
            y += 12f
        }
        return y + 6f
    }

    fun drawInfoCard(
        canvas: Canvas,
        title: String,
        left: Float,
        top: Float,
        width: Float,
        rows: List<Pair<String, String>>
    ): Float {
        val titleBandHeight = 22f
        val rowHeight = 34f
        val rowGap = 6f
        val cardHeight = titleBandHeight + 12f + rows.size * rowHeight + (rows.size - 1) * rowGap
        val rect = RectF(left, top, left + width, top + cardHeight)
        canvas.drawRoundRect(rect, 14f, 14f, cardFillPaint)
        canvas.drawRoundRect(rect, 14f, 14f, borderPaint)

        val titleRect = RectF(left, top, left + width, top + titleBandHeight)
        canvas.drawRoundRect(titleRect, 14f, 14f, softFillPaint)
        canvas.drawText(title.uppercase(), left + 14f, top + 15f, sectionTitlePaint)

        val innerLeft = left + 14f
        val innerWidth = width - 28f
        var y = top + titleBandHeight + 16f

        rows.forEachIndexed { index, (label, value) ->
            y = drawLabeledValue(canvas, label, value, innerLeft, y, innerWidth)
            if (index < rows.lastIndex) {
                canvas.drawLine(innerLeft, y - 2f, left + width - 14f, y - 2f, dividerPaint)
                y += rowGap
            }
        }

        return rect.bottom
    }

    fun drawSummaryChip(canvas: Canvas, left: Float, top: Float, width: Float, label: String, value: String) {
        val rect = RectF(left, top, left + width, top + 44f)
        canvas.drawRoundRect(rect, 12f, 12f, softFillPaint)
        canvas.drawRoundRect(rect, 12f, 12f, borderPaint)
        canvas.drawText(label.uppercase(), left + 12f, top + 14f, cardLabelPaint)
        canvas.drawText(value, left + 12f, top + 31f, bodyBoldPaint)
    }

    fun statusColors(status: String): Pair<Int, Int> {
        return when (status.trim().uppercase()) {
            "DEPLOYED", "ACTIVE", "COMPLETED", "OK" -> Pair(Color.rgb(39, 174, 96), Color.WHITE)
            "PENDING", "FOR RELEASE" -> Pair(Color.rgb(241, 196, 15), yakultDark)
            "PULLED OUT", "DAMAGED", "REPORTED" -> Pair(Color.rgb(231, 76, 60), Color.WHITE)
            else -> Pair(Color.rgb(108, 117, 125), Color.WHITE)
        }
    }

    data class PageCtx(
        val number: Int,
        val page: PdfDocument.Page,
        val canvas: Canvas,
        var y: Float
    )

    fun drawPageBanner(canvas: Canvas, pageNumber: Int): Float {
        val bannerRect = RectF(contentLeft, 28f, contentRight, 112f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)

        val sealRect = RectF(contentLeft + 18f, 42f, contentLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTextPaint)

        val titleLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", titleLeft, 55f, bannerLabelPaint)
        canvas.drawText("Asset Dispatch Form", titleLeft, 79f, titlePaint)

        val pillRect = RectF(titleLeft, 86f, titleLeft + 88f, 102f)
        canvas.drawRoundRect(pillRect, 8f, 8f, bannerPillPaint)
        canvas.drawText("DISPATCH COPY", titleLeft + 10f, 97f, bannerPillTextPaint)

        val rightLabelX = contentRight - 160f
        canvas.drawText("SET CODE", rightLabelX, 56f, bannerLabelPaint)
        canvas.drawText(ellipsizeToWidth(safeText(set.setCode), bannerValuePaint, 140f), rightLabelX, 75f, bannerValuePaint)
        canvas.drawText("PAGE / GENERATED", rightLabelX, 93f, bannerLabelPaint)
        canvas.drawText(ellipsizeToWidth("$pageNumber  |  $generatedAt", bannerValuePaint, 140f), rightLabelX, 109f, bannerValuePaint)

        return bannerRect.bottom + 18f
    }

    fun drawFirstPageOverview(canvas: Canvas, top: Float): Float {
        val gap = 12f
        val cardWidth = (contentWidth - gap) / 2f

        val leftBottom = drawInfoCard(
            canvas = canvas,
            title = "Dispatch Overview",
            left = contentLeft,
            top = top,
            width = cardWidth,
            rows = listOf(
                "Employee" to safeText(set.employee),
                "Department" to safeText(set.department),
                "Branch" to safeText(set.branch),
                "Status" to safeText(set.status, "Pending")
            )
        )

        val rightBottom = drawInfoCard(
            canvas = canvas,
            title = "Record Details",
            left = contentLeft + cardWidth + gap,
            top = top,
            width = cardWidth,
            rows = listOf(
                "Company" to safeText(set.company),
                "Dispatch Date" to formatDisplayDate(set.dispatchDate),
                "Created Date" to formatDisplayDate(set.createdDate),
                "Prepared By" to safeText(set.createdBy),
                "Logistics" to safeText(set.logistics)
            )
        )

        val cardsBottom = maxOf(leftBottom, rightBottom)
        val itemCount = set.items.orEmpty().size
        val withSerial = set.items.orEmpty().count { !it.serialNumber.isNullOrBlank() }
        val summaryTop = cardsBottom + 12f
        val chipGap = 10f
        val chipWidth = (contentWidth - (chipGap * 2f)) / 3f

        drawSummaryChip(canvas, contentLeft, summaryTop, chipWidth, "Items", itemCount.toString())
        drawSummaryChip(canvas, contentLeft + chipWidth + chipGap, summaryTop, chipWidth, "With Serial", withSerial.toString())
        drawSummaryChip(canvas, contentLeft + (chipWidth + chipGap) * 2f, summaryTop, chipWidth, "Current Status", safeText(set.status, "Pending"))

        return summaryTop + 58f
    }

    fun drawSectionCaption(canvas: Canvas, top: Float, title: String, subtitle: String): Float {
        canvas.drawText(title.uppercase(), contentLeft, top, sectionTitlePaint)
        canvas.drawText(subtitle, contentLeft, top + 14f, footerPaint)
        return top + 26f
    }

    fun drawItemsTableHeader(canvas: Canvas, top: Float): Float {
        val rect = RectF(contentLeft, top, contentRight, top + 24f)
        canvas.drawRoundRect(rect, 10f, 10f, tableHeaderFillPaint)
        canvas.drawRoundRect(rect, 10f, 10f, borderPaint)

        val col1 = contentLeft + 12f
        val col2 = contentLeft + 247f
        val col3 = contentLeft + 366f
        val col4 = contentLeft + 464f

        canvas.drawText("ITEM / DETAILS", col1, top + 16f, tableHeaderPaint)
        canvas.drawText("SERIAL", col2, top + 16f, tableHeaderPaint)
        canvas.drawText("MODEL", col3, top + 16f, tableHeaderPaint)
        canvas.drawText("STATUS", col4, top + 16f, tableHeaderPaint)

        return rect.bottom + 8f
    }

    fun beginPage(number: Int, includeOverview: Boolean, includeTable: Boolean): PageCtx {
        val pageInfo = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, number).create()
        val page = document.startPage(pageInfo)
        val canvas = page.canvas

        var y = drawPageBanner(canvas, number)
        if (includeOverview) {
            y = drawFirstPageOverview(canvas, y)
        }
        if (includeTable) {
            y = drawSectionCaption(canvas, y, "Dispatched Items", "Issued assets captured from the scanned set payload.")
            y = drawItemsTableHeader(canvas, y)
        }

        return PageCtx(number, page, canvas, y)
    }

    fun finishPage(ctx: PageCtx) {
        val footerY = pageHeight - 24f
        ctx.canvas.drawLine(contentLeft, footerY - 10f, contentRight, footerY - 10f, dividerPaint)
        ctx.canvas.drawText("Set ${safeText(set.setCode)}", contentLeft, footerY, footerPaint)
        ctx.canvas.drawText(ellipsizeToWidth("Generated $generatedAt", footerPaint, 170f), contentLeft + 165f, footerY, footerPaint)
        ctx.canvas.drawText("Page ${ctx.number}", contentRight - 42f, footerY, footerPaint)
        document.finishPage(ctx.page)
    }

    val items = set.items.orEmpty()
    val col1 = contentLeft + 12f
    val col2 = contentLeft + 247f
    val col3 = contentLeft + 366f
    val col4 = contentLeft + 464f
    val col1Width = (col2 - col1) - 10f
    val col2Width = (col3 - col2) - 12f
    val col3Width = (col4 - col3) - 12f
    val statusChipWidth = 72f

    var pageNumber = 1
    var ctx = beginPage(number = pageNumber, includeOverview = true, includeTable = true)

    if (items.isEmpty()) {
        val emptyRect = RectF(contentLeft, ctx.y, contentRight, ctx.y + 52f)
        ctx.canvas.drawRoundRect(emptyRect, 12f, 12f, softFillPaint)
        ctx.canvas.drawRoundRect(emptyRect, 12f, 12f, borderPaint)
        ctx.canvas.drawText("No dispatched items were found in this set.", contentLeft + 14f, ctx.y + 31f, bodyPaint)
        ctx.y = emptyRect.bottom + 16f
    } else {
        items.forEachIndexed { index, item ->
            val primary = safeText(item.type ?: item.description, "Unknown Item")
            val detailParts = buildList {
                item.category?.trim()?.takeIf { it.isNotEmpty() }?.let { add(it) }
                if (item.quantity > 0) add("Qty ${item.quantity}")
                item.computerName?.trim()?.takeIf { it.isNotEmpty() }?.let { add("PC $it") }
                item.ipAddress?.trim()?.takeIf { it.isNotEmpty() }?.let { add(it) }
            }
            val secondary = detailParts.joinToString(" | ").ifBlank {
                safeText(item.description, "-")
            }

            val primaryLine = wrapToWidth(primary, tableCellBoldPaint, col1Width, 1).firstOrNull().orEmpty()
            val secondaryLines = wrapToWidth(secondary, tableMetaPaint, col1Width, 2)
            val rowHeight = if (secondaryLines.size > 1) 52f else 42f

            if (ctx.y + rowHeight > contentBottom - 150f) {
                finishPage(ctx)
                pageNumber += 1
                ctx = beginPage(number = pageNumber, includeOverview = false, includeTable = true)
            }

            val rowRect = RectF(contentLeft, ctx.y, contentRight, ctx.y + rowHeight)
            ctx.canvas.drawRoundRect(rowRect, 8f, 8f, if (index % 2 == 0) whiteRowPaint else altRowPaint)
            ctx.canvas.drawRoundRect(rowRect, 8f, 8f, borderPaint)

            ctx.canvas.drawText(primaryLine, col1, ctx.y + 15f, tableCellBoldPaint)
            secondaryLines.forEachIndexed { lineIndex, line ->
                ctx.canvas.drawText(line, col1, ctx.y + 29f + (lineIndex * 10f), tableMetaPaint)
            }

            val centeredTextY = ctx.y + (rowHeight / 2f) + 3.5f
            ctx.canvas.drawText(ellipsizeToWidth(safeText(item.serialNumber, "-"), tableCellPaint, col2Width), col2, centeredTextY, tableCellPaint)
            ctx.canvas.drawText(ellipsizeToWidth(safeText(item.modelNumber, "-"), tableCellPaint, col3Width), col3, centeredTextY, tableCellPaint)

            val statusText = ellipsizeToWidth(safeText(item.status, "Pending"), tableCellPaint, statusChipWidth - 14f)
            val (chipFill, chipTextColor) = statusColors(statusText)
            val chipPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = chipFill }
            val chipTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = chipTextColor
                textSize = 8.5f
                isFakeBoldText = true
                textAlign = Paint.Align.CENTER
            }
            val chipTop = ctx.y + ((rowHeight - 20f) / 2f)
            val chipRect = RectF(col4, chipTop, col4 + statusChipWidth, chipTop + 20f)
            ctx.canvas.drawRoundRect(chipRect, 10f, 10f, chipPaint)
            ctx.canvas.drawText(statusText, chipRect.centerX(), chipTop + 13.5f, chipTextPaint)

            ctx.y = rowRect.bottom + 6f
        }
    }

    val signatureSectionHeight = 188f
    if (ctx.y + signatureSectionHeight > contentBottom) {
        finishPage(ctx)
        pageNumber += 1
        ctx = beginPage(number = pageNumber, includeOverview = false, includeTable = false)
    }

    ctx.y += 8f
    ctx.y = drawSectionCaption(
        ctx.canvas,
        ctx.y,
        "Confirmation Signatures",
        "Capture at least one signature before sharing this dispatch copy."
    )

    val sigGap = 16f
    val sigWidth = (contentWidth - sigGap) / 2f
    val sigHeight = 132f

    fun drawSignaturePanel(left: Float, top: Float, title: String, bitmap: Bitmap?, caption: String) {
        val rect = RectF(left, top, left + sigWidth, top + sigHeight)
        ctx.canvas.drawRoundRect(rect, 14f, 14f, cardFillPaint)
        ctx.canvas.drawRoundRect(rect, 14f, 14f, borderPaint)

        val titleRect = RectF(left, top, left + sigWidth, top + 24f)
        ctx.canvas.drawRoundRect(titleRect, 14f, 14f, softFillPaint)
        ctx.canvas.drawText(title.uppercase(), left + 12f, top + 16f, sectionTitlePaint)

        val imageRect = RectF(left + 12f, top + 32f, left + sigWidth - 12f, top + 94f)
        ctx.canvas.drawRoundRect(imageRect, 10f, 10f, softFillPaint)

        if (bitmap != null) {
            val safeWidth = bitmap.width.takeIf { it > 0 } ?: 1
            val safeHeight = bitmap.height.takeIf { it > 0 } ?: 1
            val scale = minOf(
                imageRect.width() / safeWidth.toFloat(),
                imageRect.height() / safeHeight.toFloat()
            )
            val drawWidth = safeWidth * scale
            val drawHeight = safeHeight * scale
            val leftInset = imageRect.left + ((imageRect.width() - drawWidth) / 2f)
            val topInset = imageRect.top + ((imageRect.height() - drawHeight) / 2f)
            val destination = RectF(leftInset, topInset, leftInset + drawWidth, topInset + drawHeight)
            ctx.canvas.drawBitmap(bitmap, null, destination, null)
        } else {
            ctx.canvas.drawText("No signature captured", imageRect.left + 18f, imageRect.centerY() + 4f, footerPaint)
        }

        val lineY = top + 108f
        ctx.canvas.drawLine(left + 14f, lineY, left + sigWidth - 14f, lineY, dividerPaint)
        ctx.canvas.drawText(caption, left + 12f, top + 124f, footerPaint)
    }

    drawSignaturePanel(contentLeft, ctx.y, "Requester", requesterBitmap, "Employee / Receiver")
    drawSignaturePanel(contentLeft + sigWidth + sigGap, ctx.y, "Handler / Dispatcher", dispatcherBitmap, "Issued by Yakult IT / Logistics")

    finishPage(ctx)

    val fileName = "Dispatch_${set.setCode ?: "Unknown"}_${System.currentTimeMillis()}.pdf"

    try {
        val uri: Uri = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val resolver = context.contentResolver
            val values = ContentValues().apply {
                put(MediaStore.MediaColumns.DISPLAY_NAME, fileName)
                put(MediaStore.MediaColumns.MIME_TYPE, "application/pdf")
                put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
                put(MediaStore.MediaColumns.IS_PENDING, 1)
            }

            val outUri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                ?: throw IllegalStateException("Failed to create PDF in Downloads.")

            resolver.openOutputStream(outUri)?.use { out ->
                document.writeTo(out)
            } ?: throw IllegalStateException("Failed to open output stream for PDF.")

            values.clear()
            values.put(MediaStore.MediaColumns.IS_PENDING, 0)
            resolver.update(outUri, values, null, null)
            Toast.makeText(context, "PDF saved to Downloads: $fileName", Toast.LENGTH_LONG).show()
            outUri
        } else {
            val appDownloadsDir = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir
            if (!appDownloadsDir.exists()) appDownloadsDir.mkdirs()
            val file = File(appDownloadsDir, fileName)
            document.writeTo(FileOutputStream(file))
            Toast.makeText(context, "PDF saved: $fileName", Toast.LENGTH_LONG).show()
            FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
        }

        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/pdf")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NO_HISTORY)
        }
        context.startActivity(Intent.createChooser(intent, "Open PDF"))
    } catch (e: Exception) {
        Toast.makeText(context, "We couldn't finish the PDF export right now. Please try again.", Toast.LENGTH_LONG).show()
        e.printStackTrace()
    } finally {
        document.close()
    }
}

// ── Shared helper ──────────────────────────────────────────────────────────────

internal fun savePdfAndOpen(context: Context, document: PdfDocument, fileName: String) {    try {
        val uri: Uri = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val resolver = context.contentResolver
            val values = ContentValues().apply {
                put(MediaStore.MediaColumns.DISPLAY_NAME, fileName)
                put(MediaStore.MediaColumns.MIME_TYPE, "application/pdf")
                put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
                put(MediaStore.MediaColumns.IS_PENDING, 1)
            }
            val outUri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                ?: throw IllegalStateException("Failed to create PDF in Downloads.")
            resolver.openOutputStream(outUri)?.use { document.writeTo(it) }
                ?: throw IllegalStateException("Failed to open output stream.")
            values.clear()
            values.put(MediaStore.MediaColumns.IS_PENDING, 0)
            resolver.update(outUri, values, null, null)
            Toast.makeText(context, "PDF saved to Downloads: $fileName", Toast.LENGTH_LONG).show()
            outUri
        } else {
            val dir = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir
            if (!dir.exists()) dir.mkdirs()
            val file = File(dir, fileName)
            document.writeTo(FileOutputStream(file))
            Toast.makeText(context, "PDF saved: $fileName", Toast.LENGTH_LONG).show()
            FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
        }
        val intent = android.content.Intent(android.content.Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/pdf")
            addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(android.content.Intent.FLAG_ACTIVITY_NO_HISTORY)
        }
        context.startActivity(android.content.Intent.createChooser(intent, "Open PDF"))
    } catch (e: Exception) {
        Toast.makeText(context, "PDF export failed. Please try again.", Toast.LENGTH_LONG).show()
        e.printStackTrace()
    } finally {
        document.close()
    }
}

// ── Operations Report PDF ──────────────────────────────────────────────────────

fun generateReportPdf(context: Context, summary: ReportsSummaryResponse, rangeLabel: String) {
    val document = PdfDocument()
    val pageWidth = 595
    val pageHeight = 842
    val margin = 32f
    val cLeft = margin
    val cRight = pageWidth - margin
    val cWidth = cRight - cLeft
    val cBottom = pageHeight - 42f
    val generatedAtFmt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US)
    val generatedAt = generatedAtFmt.format(Date())

    val yakultRed = Color.rgb(184, 26, 31)
    val yakultDark = Color.rgb(97, 18, 20)
    val ink = Color.rgb(32, 37, 41)
    val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230)
    val softFill = Color.rgb(248, 249, 250)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultRed }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val sectionPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultDark; textSize = 11f; isFakeBoldText = true }
    val bodyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10f }
    val bodyBoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10f; isFakeBoldText = true }
    val mutedPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8.8f }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val softFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softFill }
    val tblHdrFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(244, 236, 236) }
    val tblHdrPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultDark; textSize = 8.4f; isFakeBoldText = true }
    val tblCellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f }
    val tblMetaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val altRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(252, 252, 252) }
    val whiteRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }
    val kpiRowFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softFill }

    fun safeText(v: String?, fb: String = "N/A") = v?.trim()?.takeIf { it.isNotEmpty() } ?: fb

    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim()
        if (paint.measureText(t) <= maxW) return t
        val ellipsis = "..."
        var lo = 0; var hi = t.length
        while (lo < hi) {
            val mid = (lo + hi) / 2
            if (paint.measureText(t.substring(0, mid) + ellipsis) <= maxW) lo = mid + 1 else hi = mid
        }
        val end = (lo - 1).coerceAtLeast(0)
        return if (end == 0) ellipsis else t.substring(0, end) + ellipsis
    }

    fun wrapLines(text: String, paint: Paint, maxW: Float, maxL: Int): List<String> {
        val words = text.trim().replace("\n", " ").split(Regex("\\s+"))
        val lines = mutableListOf<String>(); var cur = ""
        for (w in words) {
            val candidate = if (cur.isBlank()) w else "$cur $w"
            if (paint.measureText(candidate) <= maxW) cur = candidate
            else {
                if (cur.isNotBlank()) { lines += cur; if (lines.size == maxL) { lines[lines.lastIndex] = ellipsize(lines.last(), paint, maxW); return lines } }
                cur = if (paint.measureText(w) <= maxW) w else ellipsize(w, paint, maxW)
            }
        }
        if (cur.isNotBlank() && lines.size < maxL) lines += cur
        if (lines.isEmpty()) lines += ellipsize(text.trim(), paint, maxW)
        return lines.take(maxL)
    }

    var pageNum = 1
    fun beginPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum++).create()
        val page = document.startPage(info)
        return page to page.canvas
    }

    fun drawBanner(canvas: Canvas, isFirst: Boolean): Float {
        val bannerRect = RectF(cLeft, 28f, cRight, 110f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)
        val sealRect = RectF(cLeft + 18f, 42f, cLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTxt)
        val tLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", tLeft, 53f, bannerLblPaint)
        canvas.drawText("Operations Report", tLeft, 76f, titlePaint)
        val pillRect = RectF(tLeft, 84f, tLeft + 80f, 99f)
        canvas.drawRoundRect(pillRect, 8f, 8f, sealFillPaint)
        val pillTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 8f; isFakeBoldText = true }
        canvas.drawText(rangeLabel.uppercase(), tLeft + 8f, 95f, pillTxt)
        val rx = cRight - 155f
        canvas.drawText("GENERATED", rx, 53f, bannerLblPaint)
        canvas.drawText(ellipsize(generatedAt, bannerValPaint, 145f), rx, 72f, bannerValPaint)
        if (!isFirst) { canvas.drawText("(continued)", rx, 90f, bannerLblPaint) }
        return bannerRect.bottom + 18f
    }

    fun finishPage(page: PdfDocument.Page, canvas: Canvas, pgNum: Int) {
        val fy = pageHeight - 24f
        canvas.drawLine(cLeft, fy - 10f, cRight, fy - 10f, dividerPaint)
        canvas.drawText("Yakult Philippines, Inc. — Operations Report", cLeft, fy, footerPaint)
        canvas.drawText("Page $pgNum", cRight - 42f, fy, footerPaint)
        document.finishPage(page)
    }

    data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

    fun newPage(isFirst: Boolean = false): PageState {
        val n = pageNum - 1
        val (p, c) = beginPage()
        val y = drawBanner(c, isFirst)
        return PageState(p, c, y, n + 1)
    }

    fun ensureSpace(ps: PageState, needed: Float, action: (PageState) -> PageState): PageState {
        return if (ps.y + needed > cBottom) {
            finishPage(ps.page, ps.canvas, ps.num)
            newPage(false)
        } else ps
    }

    fun drawSectionHeader(canvas: Canvas, title: String, y: Float): Float {
        canvas.drawText(title.uppercase(), cLeft, y, sectionPaint)
        return y + 16f
    }

    fun drawTableHeader(canvas: Canvas, y: Float, col1W: Float): Float {
        val rect = RectF(cLeft, y, cRight, y + 22f)
        canvas.drawRoundRect(rect, 8f, 8f, tblHdrFill)
        canvas.drawRoundRect(rect, 8f, 8f, borderPaint)
        canvas.drawText("DESCRIPTION", cLeft + 10f, y + 15f, tblHdrPaint)
        canvas.drawText("DETAIL", cLeft + col1W + 10f, y + 15f, tblHdrPaint)
        canvas.drawText("VALUE", cRight - 65f, y + 15f, tblHdrPaint)
        return rect.bottom + 4f
    }

    fun drawTableRow(canvas: Canvas, row: ReportListRowDto, idx: Int, y: Float, col1W: Float): Float {
        val titleLines = wrapLines(safeText(row.title), tblCellPaint, col1W - 12f, 2)
        val rowH = if (titleLines.size > 1) 42f else 32f
        val rowRect = RectF(cLeft, y, cRight, y + rowH)
        canvas.drawRoundRect(rowRect, 6f, 6f, if (idx % 2 == 0) whiteRowPaint else altRowPaint)
        canvas.drawRoundRect(rowRect, 6f, 6f, borderPaint)
        val midY = y + rowH / 2f + 3.5f
        titleLines.forEachIndexed { li, line ->
            canvas.drawText(line, cLeft + 10f, y + 14f + li * 12f, if (li == 0) tblCellPaint else tblMetaPaint)
        }
        val subText = ellipsize(safeText(row.subtitle, ""), tblMetaPaint, (cRight - cLeft - col1W - 80f))
        if (subText.isNotEmpty()) canvas.drawText(subText, cLeft + col1W + 10f, midY, tblMetaPaint)
        val valText = ellipsize(safeText(row.value, "--"), tblCellPaint, 62f)
        canvas.drawText(valText, cRight - 65f, midY, tblCellPaint)
        return rowRect.bottom + 4f
    }

    fun drawKpiRow(canvas: Canvas, kpis: List<com.example.yakultscanner.api.ReportKpiDto>, y: Float): Float {
        val gap = 10f; val chipW = (cWidth - gap * (kpis.size - 1)) / kpis.size
        kpis.forEachIndexed { i, kpi ->
            val x = cLeft + i * (chipW + gap)
            val rect = RectF(x, y, x + chipW, y + 52f)
            canvas.drawRoundRect(rect, 12f, 12f, kpiRowFill)
            canvas.drawRoundRect(rect, 12f, 12f, borderPaint)
            canvas.drawText(safeText(kpi.title, "Metric").uppercase(), x + 10f, y + 14f, mutedPaint)
            canvas.drawText(safeText(kpi.value, "0"), x + 10f, y + 34f, bodyBoldPaint.apply { textSize = 16f })
            bodyBoldPaint.textSize = 10f
            canvas.drawText(ellipsize(safeText(kpi.delta, ""), mutedPaint, chipW - 14f), x + 10f, y + 48f, mutedPaint)
        }
        return y + 62f
    }

    var ps = newPage(true)

    ps.y = drawSectionHeader(ps.canvas, "Key Performance Indicators", ps.y)
    summary.kpis.chunked(2).forEach { chunk ->
        ps = ensureSpace(ps, 70f) { it }
        ps.y = drawKpiRow(ps.canvas, chunk, ps.y)
        ps.y += 8f
    }

    ps.y += 6f
    ps = ensureSpace(ps, 80f) { it }
    ps.y = drawSectionHeader(ps.canvas, "Executive Summary", ps.y)
    val summaryLines = wrapLines(safeText(summary.executiveSummary, "No summary available."), bodyPaint, cWidth, 6)
    summaryLines.forEach { line ->
        ps.canvas.drawText(line, cLeft, ps.y, bodyPaint)
        ps.y += 14f
    }

    if (summary.highlights.isNotEmpty()) {
        ps.y += 8f
        ps = ensureSpace(ps, 40f) { it }
        ps.y = drawSectionHeader(ps.canvas, "Highlights", ps.y)
        summary.highlights.take(6).forEach { hl ->
            val hlLines = wrapLines("• $hl", bodyPaint, cWidth, 2)
            hlLines.forEach { line ->
                ps = ensureSpace(ps, 18f) { it }
                ps.canvas.drawText(line, cLeft, ps.y, bodyPaint)
                ps.y += 14f
            }
        }
    }

    val sections = listOf(
        "Health Signals" to summary.healthSignals,
        "Issue Queue" to summary.issueRows,
        "Borrow Watchlist" to summary.borrowRows,
        "Encoder Activity" to summary.activityRows,
        "Recent Timeline" to summary.timelineRows
    )

    val col1W = cWidth * 0.42f

    sections.forEach { (title, rows) ->
        if (rows.isEmpty()) return@forEach
        ps.y += 12f
        ps = ensureSpace(ps, 60f) { it }
        ps.y = drawSectionHeader(ps.canvas, title, ps.y)
        ps.y = drawTableHeader(ps.canvas, ps.y, col1W)
        rows.forEachIndexed { idx, row ->
            val rowH = if (wrapLines(row.title.orEmpty(), tblCellPaint, col1W - 12f, 2).size > 1) 42f else 32f
            ps = ensureSpace(ps, rowH + 4f) { it }
            ps.y = drawTableRow(ps.canvas, row, idx, ps.y, col1W)
        }
    }

    finishPage(ps.page, ps.canvas, ps.num)
    val fileName = "Report_${rangeLabel.replace(" ", "_")}_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context, document, fileName)
}

// ── IT Service Ticket PDF ──────────────────────────────────────────────────────

fun generateTicketPdf(
    context: Context,
    ticket: CallTicketDetail,
    notes: List<CallTicketNoteDto>,
    history: List<CallTicketHistoryDto>,
    signOff: TicketSignOffBundle? = null
) {
    if (signOff != null && SignOffParity.isSignOffEligible(ticket.status, signOff.visits)) {
        generateSignOffTicketPdf(context, ticket, notes, history, signOff)
        return
    }
    val document = PdfDocument()
    val pageWidth = 595; val pageHeight = 842
    val margin = 32f; val cLeft = margin; val cRight = pageWidth - margin
    val cWidth = cRight - cLeft; val cBottom = pageHeight - 42f
    val generatedAt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US).format(Date())

    val yakultRed = Color.rgb(184, 26, 31); val yakultDark = Color.rgb(97, 18, 20)
    val ink = Color.rgb(32, 37, 41); val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230); val softFill = Color.rgb(248, 249, 250)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultRed }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val sectionPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultDark; textSize = 11f; isFakeBoldText = true }
    val lblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8.6f; isFakeBoldText = true }
    val valPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10.4f }
    val bodyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10f }
    val bodyBoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10f; isFakeBoldText = true }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val softFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softFill }
    val cardFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }

    fun safeText(v: String?, fb: String = "N/A") = v?.trim()?.takeIf { it.isNotEmpty() } ?: fb

    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim(); if (paint.measureText(t) <= maxW) return t
        val e = "..."; var lo = 0; var hi = t.length
        while (lo < hi) { val mid = (lo + hi) / 2; if (paint.measureText(t.substring(0, mid) + e) <= maxW) lo = mid + 1 else hi = mid }
        val end = (lo - 1).coerceAtLeast(0); return if (end == 0) e else t.substring(0, end) + e
    }

    fun wrapLines(text: String, paint: Paint, maxW: Float, maxL: Int): List<String> {
        val words = text.trim().replace("\n", " ").split(Regex("\\s+"))
        val lines = mutableListOf<String>(); var cur = ""
        for (w in words) {
            val c2 = if (cur.isBlank()) w else "$cur $w"
            if (paint.measureText(c2) <= maxW) cur = c2
            else { if (cur.isNotBlank()) { lines += cur; if (lines.size == maxL) { lines[lines.lastIndex] = ellipsize(lines.last(), paint, maxW); return lines } }; cur = if (paint.measureText(w) <= maxW) w else ellipsize(w, paint, maxW) }
        }
        if (cur.isNotBlank() && lines.size < maxL) lines += cur
        if (lines.isEmpty()) lines += ellipsize(text.trim(), paint, maxW)
        return lines.take(maxL)
    }

    var pageNum = 1
    fun beginPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum++).create()
        val page = document.startPage(info); return page to page.canvas
    }

    data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

    fun drawBanner(canvas: Canvas): Float {
        val bannerRect = RectF(cLeft, 28f, cRight, 110f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)
        val sealRect = RectF(cLeft + 18f, 42f, cLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTxt)
        val tLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", tLeft, 53f, bannerLblPaint)
        canvas.drawText("IT Service Report", tLeft, 76f, titlePaint)
        val pillRect = RectF(tLeft, 84f, tLeft + 92f, 99f)
        canvas.drawRoundRect(pillRect, 8f, 8f, sealFillPaint)
        val pillTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 8f; isFakeBoldText = true }
        canvas.drawText("TICKET COPY", tLeft + 8f, 95f, pillTxt)
        val rx = cRight - 155f
        canvas.drawText("TICKET CODE", rx, 53f, bannerLblPaint)
        canvas.drawText(ellipsize(safeText(ticket.ticketCode), bannerValPaint, 145f), rx, 72f, bannerValPaint)
        canvas.drawText("GENERATED", rx, 86f, bannerLblPaint)
        canvas.drawText(ellipsize(generatedAt, bannerValPaint, 145f), rx, 100f, bannerValPaint)
        return bannerRect.bottom + 18f
    }

    fun finishPage(page: PdfDocument.Page, canvas: Canvas, n: Int) {
        val fy = pageHeight - 24f
        canvas.drawLine(cLeft, fy - 10f, cRight, fy - 10f, dividerPaint)
        canvas.drawText("Ticket ${safeText(ticket.ticketCode)}", cLeft, fy, footerPaint)
        canvas.drawText("Generated $generatedAt", cLeft + 160f, fy, footerPaint)
        canvas.drawText("Page $n", cRight - 42f, fy, footerPaint)
        document.finishPage(page)
    }

    fun newPage(): PageState {
        val n = pageNum - 1; val (p, c) = beginPage(); val y = drawBanner(c); return PageState(p, c, y, n + 1)
    }

    fun ensureSpace(ps: PageState, needed: Float): PageState {
        return if (ps.y + needed > cBottom) { finishPage(ps.page, ps.canvas, ps.num); newPage() } else ps
    }

    fun drawInfoCard(canvas: Canvas, title: String, y: Float, rows: List<Pair<String, String>>): Float {
        val titleH = 22f; val rowH = 34f; val gap = 6f
        val cardH = titleH + 12f + rows.size * rowH + (rows.size - 1) * gap
        val rect = RectF(cLeft, y, cRight, y + cardH)
        canvas.drawRoundRect(rect, 14f, 14f, cardFillPaint)
        canvas.drawRoundRect(rect, 14f, 14f, borderPaint)
        val titleRect = RectF(cLeft, y, cRight, y + titleH)
        canvas.drawRoundRect(titleRect, 14f, 14f, softFillPaint)
        canvas.drawText(title.uppercase(), cLeft + 14f, y + 15f, sectionPaint)
        val iLeft = cLeft + 14f; val iWidth = cWidth - 28f; var ry = y + titleH + 16f
        rows.forEachIndexed { i, (label, value) ->
            canvas.drawText(label.uppercase(), iLeft, ry, lblPaint)
            val lines = wrapLines(value, valPaint, iWidth, 2)
            var vy = ry + 14f
            lines.forEach { line -> canvas.drawText(line, iLeft, vy, valPaint); vy += 12f }
            ry = vy + 6f
            if (i < rows.lastIndex) { canvas.drawLine(iLeft, ry - 2f, cRight - 14f, ry - 2f, dividerPaint); ry += gap }
        }
        return rect.bottom
    }

    fun drawTextBlock(canvas: Canvas, sectionTitle: String, text: String, y: Float): Float {
        canvas.drawText(sectionTitle.uppercase(), cLeft, y, sectionPaint)
        var ty = y + 16f
        val lines = wrapLines(text, bodyPaint, cWidth, 8)
        lines.forEach { line -> canvas.drawText(line, cLeft, ty, bodyPaint); ty += 14f }
        return ty + 6f
    }

    var ps = newPage()

    val halfW = (cWidth - 12f) / 2f
    val leftRows = listOf("Company" to safeText(ticket.company), "Department" to safeText(ticket.department), "Branch" to safeText(ticket.branch), "Caller" to safeText(ticket.callerName))
    val rightRows = listOf("Status" to safeText(ticket.status), "Priority" to safeText(ticket.priority), "Issue Type" to safeText(ticket.issueType), "Assigned To" to safeText(ticket.assignedTo), "Created" to safeText(ticket.createdAt), "Solved At" to safeText(ticket.solvedAt))

    val leftCardH = 22f + 12f + leftRows.size * 34f + (leftRows.size - 1) * 6f
    val rightCardH = 22f + 12f + rightRows.size * 34f + (rightRows.size - 1) * 6f

    fun drawHalfCard(canvas: Canvas, title: String, rows: List<Pair<String, String>>, x: Float, y: Float, w: Float): Float {
        val titleH = 22f; val rowH = 34f; val gap = 6f
        val cardH = titleH + 12f + rows.size * rowH + (rows.size - 1) * gap
        val rect = RectF(x, y, x + w, y + cardH)
        canvas.drawRoundRect(rect, 14f, 14f, cardFillPaint)
        canvas.drawRoundRect(rect, 14f, 14f, borderPaint)
        val tRect = RectF(x, y, x + w, y + titleH)
        canvas.drawRoundRect(tRect, 14f, 14f, softFillPaint)
        canvas.drawText(title.uppercase(), x + 14f, y + 15f, sectionPaint)
        val iLeft2 = x + 14f; val iWidth2 = w - 28f; var ry2 = y + titleH + 16f
        rows.forEachIndexed { i, (label, value) ->
            canvas.drawText(label.uppercase(), iLeft2, ry2, lblPaint)
            val lines = wrapLines(value, valPaint, iWidth2, 2); var vy = ry2 + 14f
            lines.forEach { line -> canvas.drawText(line, iLeft2, vy, valPaint); vy += 12f }; ry2 = vy + 6f
            if (i < rows.lastIndex) { canvas.drawLine(iLeft2, ry2 - 2f, x + w - 14f, ry2 - 2f, dividerPaint); ry2 += gap }
        }
        return rect.bottom
    }

    val maxCardH = maxOf(leftCardH, rightCardH)
    ps = ensureSpace(ps, maxCardH + 20f)
    drawHalfCard(ps.canvas, "Caller Info", leftRows, cLeft, ps.y, halfW)
    drawHalfCard(ps.canvas, "Ticket Info", rightRows, cLeft + halfW + 12f, ps.y, halfW)
    ps.y += maxCardH + 16f

    if (ticket.issue?.isNotBlank() == true) {
        ps = ensureSpace(ps, 60f)
        ps.y = drawTextBlock(ps.canvas, "Issue Description", ticket.issue, ps.y)
    }
    if (ticket.providedSolution?.isNotBlank() == true) {
        ps = ensureSpace(ps, 60f)
        ps.y = drawTextBlock(ps.canvas, "Provided Solution", ticket.providedSolution, ps.y)
    }

    if (notes.isNotEmpty()) {
        ps.y += 8f; ps = ensureSpace(ps, 40f)
        ps.canvas.drawText("NOTES & ACTIVITY", cLeft, ps.y, sectionPaint); ps.y += 16f
        notes.forEach { note ->
            val noteLines = wrapLines(safeText(note.noteText, "—"), bodyPaint, cWidth - 24f, 4)
            val noteH = 28f + noteLines.size * 13f
            ps = ensureSpace(ps, noteH + 10f)
            val noteRect = RectF(cLeft, ps.y, cRight, ps.y + noteH)
            ps.canvas.drawRoundRect(noteRect, 10f, 10f, softFillPaint)
            ps.canvas.drawRoundRect(noteRect, 10f, 10f, borderPaint)
            ps.canvas.drawText("${safeText(note.noteType, "Note")}  •  ${safeText(note.createdBy)}  •  ${safeText(note.createdAt)}", cLeft + 10f, ps.y + 14f, lblPaint)
            noteLines.forEachIndexed { i, line -> ps.canvas.drawText(line, cLeft + 10f, ps.y + 26f + i * 13f, bodyPaint) }
            ps.y = noteRect.bottom + 6f
        }
    }

    if (history.isNotEmpty()) {
        ps.y += 8f; ps = ensureSpace(ps, 40f)
        ps.canvas.drawText("STATUS HISTORY", cLeft, ps.y, sectionPaint); ps.y += 16f
        history.forEach { entry ->
            ps = ensureSpace(ps, 36f)
            val hRect = RectF(cLeft, ps.y, cRight, ps.y + 30f)
            ps.canvas.drawRoundRect(hRect, 8f, 8f, softFillPaint)
            ps.canvas.drawRoundRect(hRect, 8f, 8f, borderPaint)
            ps.canvas.drawText("${safeText(entry.fieldName)}: ${entry.oldValue?.takeIf { it.isNotBlank() } ?: "—"} → ${safeText(entry.newValue)}", cLeft + 10f, ps.y + 14f, bodyBoldPaint)
            ps.canvas.drawText("by ${safeText(entry.changedBy)}   ${safeText(entry.changedAt)}", cLeft + 10f, ps.y + 26f, lblPaint)
            ps.y = hRect.bottom + 4f
        }
    }

    finishPage(ps.page, ps.canvas, ps.num)
    val fileName = "Ticket_${ticket.ticketCode ?: "Unknown"}_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context, document, fileName)
}

// ── Field Work Sign-Off ticket PDF (1:1 with the desktop Sign-Off report) ──
// Sections, order, labels, caps and fallbacks mirror
// FieldVisitCompletionPdfGenerator + WpfFieldWorkReportDetailDialog.
// Basic (non-eligible) tickets keep the legacy TICKET COPY above.

private fun decodeSignOffBitmap(b64: String?): Bitmap? {
    if (b64.isNullOrBlank()) return null
    return try {
        val bytes = Base64.decode(b64.trim(), Base64.DEFAULT)
        BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
    } catch (_: Exception) {
        null
    }
}

fun generateSignOffTicketPdf(
    context: Context,
    ticket: CallTicketDetail,
    notes: List<CallTicketNoteDto>,
    history: List<CallTicketHistoryDto>,
    signOff: TicketSignOffBundle
) {
    val pageWidth = 595; val pageHeight = 842
    val margin = 44f; val cLeft = margin; val cRight = pageWidth - margin
    val cWidth = cRight - cLeft; val cBottom = pageHeight - margin - 34f
    val generatedAt = SimpleDateFormat("MMM d, yyyy h:mm a", Locale.US).format(Date())
    val generatedDay = SimpleDateFormat("MMM d, yyyy", Locale.US).format(Date())
    val fileStamp = SimpleDateFormat("yyyyMMdd", Locale.US).format(Date())
    val code = ticket.ticketCode?.trim()?.takeIf { it.isNotEmpty() } ?: "#${ticket.ticketId}"
    val codeNoSpaces = code.replace(" ", "")

    val yakultRed = Color.rgb(227, 6, 19)
    val ink = Color.rgb(30, 41, 59)
    val muted = Color.rgb(100, 116, 139)
    val softBorder = Color.rgb(226, 232, 240)
    val headerFill = Color.rgb(241, 245, 249)
    val issueBg = Color.rgb(254, 242, 242)
    val resolutionBg = Color.rgb(240, 253, 244)
    val green = Color.rgb(22, 163, 74)
    val amber = Color.rgb(217, 119, 6)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultRed }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val statusPillFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }
    fun statusPillTextPaint(c: Int) = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = c; textSize = 10f; isFakeBoldText = true }
    val sectionPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = yakultRed; textSize = 11f; isFakeBoldText = true; textAlign = Paint.Align.LEFT }
    val lblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8.6f; isFakeBoldText = true; textAlign = Paint.Align.LEFT }
    val valPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10.4f; textAlign = Paint.Align.LEFT }
    val bodyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 10f }
    val notePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 8.5f }
    val quotePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.ITALIC) }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val cardFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }
    val headFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = headerFill }
    val cellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f }
    val cellBoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f; isFakeBoldText = true }
    fun statusPaintFor(status: String) = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = when {
            status.contains("Solved", ignoreCase = true) || status.contains("Closed", ignoreCase = true) -> green
            status.contains("Temporary", ignoreCase = true) -> amber
            else -> ink
        }
        textSize = 9f; isFakeBoldText = true
    }

    fun dash(v: String?) = v?.trim()?.takeIf { it.isNotEmpty() } ?: "-"

    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim(); if (paint.measureText(t) <= maxW) return t
        val e = "..."; var lo = 0; var hi = t.length
        while (lo < hi) { val mid = (lo + hi) / 2; if (paint.measureText(t.substring(0, mid) + e) <= maxW) lo = mid + 1 else hi = mid }
        val end = (lo - 1).coerceAtLeast(0); return if (end == 0) e else t.substring(0, end) + e
    }

    fun wrapLines(text: String, paint: Paint, maxW: Float, maxL: Int): List<String> {
        val words = text.trim().replace("\n", " ").split(Regex("\\s+"))
        val lines = mutableListOf<String>(); var cur = ""
        for (w in words) {
            val c2 = if (cur.isBlank()) w else "$cur $w"
            if (paint.measureText(c2) <= maxW) cur = c2
            else { if (cur.isNotBlank()) { lines += cur; if (lines.size == maxL) { lines[lines.lastIndex] = ellipsize(lines.last(), paint, maxW); return lines } }; cur = if (paint.measureText(w) <= maxW) w else ellipsize(w, paint, maxW) }
        }
        if (cur.isNotBlank() && lines.size < maxL) lines += cur
        if (lines.isEmpty()) lines += ellipsize(text.trim(), paint, maxW)
        return lines.take(maxL)
    }

    fun centerCropSrc(bmp: Bitmap, dstW: Float, dstH: Float): Rect {
        val scale = maxOf(dstW / bmp.width, dstH / bmp.height)
        val sw = (dstW / scale).toInt().coerceAtMost(bmp.width).coerceAtLeast(1)
        val sh = (dstH / scale).toInt().coerceAtMost(bmp.height).coerceAtLeast(1)
        val left = (bmp.width - sw) / 2; val top = (bmp.height - sh) / 2
        return Rect(left, top, left + sw, top + sh)
    }

    fun containDst(bmp: Bitmap, dst: RectF): RectF {
        val scale = minOf(dst.width() / bmp.width, dst.height() / bmp.height)
        val w = bmp.width * scale; val h = bmp.height * scale
        val left = dst.centerX() - w / 2; val top = dst.centerY() - h / 2
        return RectF(left, top, left + w, top + h)
    }

    // ── Shared model (decoded once, reused across the footer passes) ──
    val timeline = SignOffParity.filterTimeline(history)
    val resLabel = SignOffParity.resolutionTypeLabel(ticket.status, history)
    val resBody = SignOffParity.buildResolutionBody(ticket.providedSolution, history, notes)
    val noteLines = SignOffParity.notesLines(notes)
    val firstVisit = signOff.currentVisit
    val statusUpper = (ticket.status?.trim()?.takeIf { it.isNotEmpty() } ?: "-").uppercase()
    val statusColor = if (statusUpper.contains("TEMPORARY")) amber else yakultRed
    val photoBitmaps: List<Bitmap?> = signOff.thumbs.map { decodeSignOffBitmap(it.bytes) }
    val signatureBitmap = signOff.signatureBytes?.let { decodeSignOffBitmap(it) }

    fun buildDocument(totalPages: Int?): PdfDocument {
        val document = PdfDocument()
        var pageNum = 1
        data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

        fun drawBanner(canvas: Canvas): Float {
            // Desktop: sharp full-bleed red band, no rounded card, no seal.
            val bannerH = 118f
            canvas.drawRect(0f, 0f, pageWidth.toFloat(), bannerH, bannerPaint)
            val titlePaint19 = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 19f; isFakeBoldText = true }
            canvas.drawText("FIELD WORK COMPLETION REPORT", cLeft, 38f, titlePaint19)
            val pillPaint = statusPillTextPaint(statusColor).apply { textAlign = Paint.Align.CENTER }
            val pillW = statusPillTextPaint(statusColor).measureText(statusUpper) + 30f
            val pillH = 24f
            val pillRect = RectF(cLeft + cWidth - pillW, 20f, cLeft + cWidth, 20f + pillH)
            canvas.drawRoundRect(pillRect, 12f, 12f, statusPillFill)
            canvas.drawText(statusUpper, pillRect.centerX(), 36f, pillPaint)
            val hairPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.argb(120, 255, 255, 255); strokeWidth = 0.8f
            }
            canvas.drawLine(cLeft, 66f, cLeft + cWidth, 66f, hairPaint)
            val metaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 9f }
            canvas.drawText("Report No.: $code", cLeft, 84f, metaPaint)
            val genText = "Generated: $generatedAt"
            canvas.drawText(genText, cLeft + cWidth - metaPaint.measureText(genText), 84f, metaPaint)
            return bannerH + 18f
        }

        fun finishPage(page: PdfDocument.Page, canvas: Canvas, n: Int) {
            val fy = pageHeight - 20f
            canvas.drawText("Report No: $code  |  Confidential", cLeft, fy, footerPaint)
            val pr = if (totalPages != null) "Page $n of $totalPages" else "Page $n"
            canvas.drawText(pr, cRight - footerPaint.measureText(pr), fy, footerPaint)
            document.finishPage(page)
        }

        fun newPage(): PageState {
            val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum).create()
            val page = document.startPage(info)
            val y = drawBanner(page.canvas)
            val st = PageState(page, page.canvas, y, pageNum)
            pageNum++
            return st
        }

        fun ensureSpace(ps: PageState, needed: Float): PageState {
            return if (ps.y + needed > cBottom) { finishPage(ps.page, ps.canvas, ps.num); newPage() } else ps
        }

        fun sectionHead(canvas: Canvas, y: Float, left: String, right: String?): Float {
            canvas.drawText(left.uppercase(), cLeft, y + 11f, sectionPaint)
            if (!right.isNullOrBlank()) {
                val w = sectionPaint.measureText(right.uppercase())
                canvas.drawText(right.uppercase(), cRight - w, y + 11f, sectionPaint)
            }
            var ny = y + 18f
            canvas.drawLine(cLeft, ny, cRight, ny, dividerPaint)
            return ny + 10f
        }

        val gridValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9.5f; textAlign = Paint.Align.LEFT }

        fun drawInfoGrid(
            canvas: Canvas,
            y: Float,
            leftRows: List<Pair<String, String>>,
            rightRows: List<Pair<String, String>>
        ): Float {
            // Desktop DrawInfoCell: label at (x, y+6, w=100), value at
            // (x+100, y+5) single-line trimmed, divider at row bottom.
            val colW = cWidth / 2f
            val rowH = 27f
            var ry = y
            for (i in 0 until maxOf(leftRows.size, rightRows.size)) {
                val (ll, lv) = leftRows.getOrElse(i) { "" to "" }
                val (rl, rv) = rightRows.getOrElse(i) { "" to "" }
                if (ll.isNotEmpty()) {
                    canvas.drawText(ll.uppercase(), cLeft, ry + 14f, lblPaint)
                    canvas.drawText(
                        ellipsize(lv.ifBlank { "—" }, gridValPaint, colW - 14f - 104f),
                        cLeft + 100f, ry + 14f, gridValPaint
                    )
                    canvas.drawLine(cLeft, ry + rowH - 1f, cLeft + colW - 14f, ry + rowH - 1f, dividerPaint)
                }
                if (rl.isNotEmpty()) {
                    val rx = cLeft + colW + 14f
                    canvas.drawText(rl.uppercase(), rx, ry + 14f, lblPaint)
                    canvas.drawText(
                        ellipsize(rv.ifBlank { "—" }, gridValPaint, colW - 14f - 104f),
                        rx + 100f, ry + 14f, gridValPaint
                    )
                    canvas.drawLine(rx, ry + rowH - 1f, rx + colW - 14f, ry + rowH - 1f, dividerPaint)
                }
                ry += rowH
            }
            return ry
        }

        fun drawAccentCard(canvas: Canvas, title: String, titleColor: Int, bg: Int, text: String, y: Float, maxLines: Int): Float {
            val tPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = titleColor; textSize = 11f; isFakeBoldText = true }
            val bgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = bg }
            val barPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = titleColor }
            val lines = wrapLines(text, bodyPaint, cWidth - 36f, maxLines)
            val h = 34f + lines.size * 13f + 14f
            val rect = RectF(cLeft, y, cRight, y + h)
            canvas.drawRoundRect(rect, 8f, 8f, bgPaint)
            canvas.drawRoundRect(rect, 8f, 8f, borderPaint)
            canvas.drawRect(cLeft + 1f, y + 8f, cLeft + 5f, y + h - 8f, barPaint)
            canvas.drawText(title.uppercase(), cLeft + 18f, y + 20f, tPaint)
            lines.forEachIndexed { i, line -> canvas.drawText(line, cLeft + 18f, y + 40f + i * 13f, bodyPaint) }
            return rect.bottom
        }

        val tsX = cLeft + 8f; val stX = cLeft + 150f; val dtX = cLeft + 282f
        val dtW = cWidth - 150f - 132f
        fun drawTimelineHead(canvas: Canvas, y: Float): Float {
            canvas.drawRect(cLeft, y, cRight, y + 24f, headFillPaint)
            canvas.drawText("TIMESTAMP", tsX, y + 14f, lblPaint)
            canvas.drawText("STATUS", stX, y + 14f, lblPaint)
            canvas.drawText("DETAILS", dtX, y + 14f, lblPaint)
            return y + 24f
        }

        var ps = newPage()

        // ── Info grid (desktop row sets, mobile two-card layout) ──
        val leftRows = listOf(
            "Department" to dash(ticket.department),
            "Branch" to dash(ticket.branch),
            "Priority" to dash(ticket.priority),
            "Created" to SignOffParity.formatDateTime(ticket.createdAt),
            "Solved" to (ticket.solvedAt?.trim()?.takeIf { it.isNotEmpty() }?.let { SignOffParity.formatDateTime(it) } ?: "—"),
            "Age" to SignOffParity.ageLabel(ticket.createdAt)
        )
        val rightRows = listOf(
            "Caller" to dash(ticket.callerName),
            "Technician" to dash(ticket.assignedTo),
            "Field Tech" to dash(firstVisit?.technicianName),
            "Visit #" to (firstVisit?.let { "Field Visit #${it.fieldVisitId}" } ?: "—"),
            "Schedule" to (firstVisit?.scheduledAt?.trim()?.takeIf { it.isNotEmpty() }?.let { SignOffParity.formatDateTime(it) } ?: "—")
        )
        // ── Info grid (desktop: one head, two aligned columns, 6 rows) ──
        ps = ensureSpace(ps, 18f + 6 * 27f + 20f)
        ps.y = sectionHead(ps.canvas, ps.y, "Ticket Information", "Personnel & Assignment")
        ps.y = drawInfoGrid(ps.canvas, ps.y, leftRows, rightRows) + 14f

        // ── Issue + resolution accent cards (always drawn, desktop fallbacks) ──
        ps = ensureSpace(ps, 80f)
        ps.y = drawAccentCard(ps.canvas, "Reported Issue", yakultRed, issueBg, ticket.issue?.trim().takeIf { !it.isNullOrEmpty() } ?: "-", ps.y, 12) + 8f
        ps = ensureSpace(ps, 80f)
        ps.y = drawAccentCard(ps.canvas, "Resolution Summary ($resLabel)", green, resolutionBg, resBody, ps.y, 12) + 12f

        // ── Status timeline table ──
        ps = ensureSpace(ps, 60f)
        ps.y = sectionHead(ps.canvas, ps.y, "Status Timeline", null)
        ps = ensureSpace(ps, 30f)
        ps.y = drawTimelineHead(ps.canvas, ps.y)
        if (timeline.rows.isEmpty()) {
            ps = ensureSpace(ps, 30f)
            ps.canvas.drawText("No history recorded.", tsX, ps.y + 14f, lblPaint)
            ps.y += 28f
        }
        for (row in timeline.rows) {
            val tsText = SignOffParity.formatDateTime(row.changedAtRaw, "—")
            val stText = row.status.trim().takeIf { it.isNotEmpty() } ?: "—"
            val dtText = row.details.trim().takeIf { it.isNotEmpty() } ?: "—"
            val dtLines = wrapLines(dtText, cellPaint, dtW - 16f, 6)
            val rowH = maxOf(26f, dtLines.size * 12f + 12f)
            val before = ps
            ps = ensureSpace(ps, rowH)
            if (ps !== before) ps.y = drawTimelineHead(ps.canvas, ps.y)
            ps.canvas.drawText(tsText, tsX, ps.y + 14f, cellPaint)
            ps.canvas.drawText(
                ellipsize(stText, statusPaintFor(stText), 124f),
                stX, ps.y + 14f, statusPaintFor(stText)
            )
            var dy = ps.y + 14f
            dtLines.forEach { line -> ps.canvas.drawText(line, dtX, dy, cellPaint); dy += 12f }
            ps.canvas.drawLine(cLeft, ps.y + rowH, cRight, ps.y + rowH, dividerPaint)
            ps.y += rowH
        }
        if (timeline.totalCount > timeline.rows.size) {
            ps = ensureSpace(ps, 22f)
            ps.canvas.drawText("+${timeline.totalCount - timeline.rows.size} earlier events on file.", tsX, ps.y + 12f, lblPaint)
            ps.y += 20f
        }
        ps.y += 8f

        // ── Evidence photo grid (desktop: fixed 150x110 cells, no boxes) ──
        if (signOff.totalPhotoCount > 0) {
            val title = "Evidence (${signOff.totalPhotoCount} photo" + (if (signOff.totalPhotoCount == 1) ")" else "s)")
            ps = ensureSpace(ps, 60f)
            ps.y = sectionHead(ps.canvas, ps.y, title, null)
            val cellW = 150f; val cellH = 110f; val cellPad = 6f
            val shownThumbs = signOff.thumbs.take(SignOffParity.EVIDENCE_MAX_PHOTOS)
            val grayPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.GRAY; textSize = 8.5f }
            shownThumbs.chunked(3).forEachIndexed { ri, chunk ->
                ps = ensureSpace(ps, cellH + cellPad)
                chunk.forEachIndexed { ci, thumb ->
                    val bmp = photoBitmaps.getOrNull(ri * 3 + ci)
                    val dx = cLeft + ci * (cellW + cellPad)
                    val dst = RectF(dx, ps.y, dx + cellW, ps.y + cellH)
                    if (bmp != null) {
                        ps.canvas.drawBitmap(bmp, centerCropSrc(bmp, cellW, cellH), dst, null)
                    } else {
                        val msg = if (thumb.bytes.isBlank()) "No image" else "Invalid image"
                        ps.canvas.drawText(msg, dx + cellW / 2f - grayPaint.measureText(msg) / 2f, ps.y + 58f, grayPaint)
                    }
                }
                ps.y += cellH + cellPad
            }
            ps.y += 4f
            if (signOff.totalPhotoCount > shownThumbs.size) {
                ps = ensureSpace(ps, 22f)
                ps.canvas.drawText("+${signOff.totalPhotoCount - shownThumbs.size} more photos on file.", cLeft, ps.y + 9f, lblPaint)
                ps.y += 18f
            }
            ps.y += 6f
        }

        // ── Notes bullets (desktop: bullet at +8, text at +22) ──
        if (noteLines.isNotEmpty()) {
            ps = ensureSpace(ps, 60f)
            ps.y = sectionHead(ps.canvas, ps.y, "Notes", null)
            for (line in noteLines) {
                val wrapped = wrapLines(line, notePaint, cWidth - 30f, 12)
                if (wrapped.isEmpty()) continue
                ps = ensureSpace(ps, wrapped.size * 11f + 8f)
                var ny = ps.y + 12f
                ps.canvas.drawText("•", cLeft + 8f, ny, notePaint)
                wrapped.forEach { w -> ps.canvas.drawText(w, cLeft + 22f, ny, notePaint); ny += 11f }
                ps.y = ny + 2f
            }
            ps.y += 6f
        }

        // ── Customer sign-off & attestation ──
        if (ps.y + 300f > cBottom) {
            finishPage(ps.page, ps.canvas, ps.num); ps = newPage()
        }
        ps.y = sectionHead(ps.canvas, ps.y, "Customer Sign-Off & Attestation", null) + 4f
        val cardW = (cWidth - 14f) / 2f; val signCardH = 210f
        ps = ensureSpace(ps, signCardH + 10f)
        val cardY = ps.y
        val cardTitlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8.5f; textAlign = Paint.Align.CENTER }
        val cardNamePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 11f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
        val cardSubPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8.5f; textAlign = Paint.Align.CENTER }
        val cardEmptyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.GRAY; textSize = 8.5f; textAlign = Paint.Align.CENTER }
        val centerQuotePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f; textAlign = Paint.Align.CENTER; typeface = Typeface.create(Typeface.DEFAULT, Typeface.ITALIC) }
        fun drawSignCardFrame(x: Float, heading: String) {
            val rect = RectF(x, cardY, x + cardW, cardY + signCardH)
            ps.canvas.drawRoundRect(rect, 10f, 10f, cardFillPaint)
            ps.canvas.drawRoundRect(rect, 10f, 10f, borderPaint)
            ps.canvas.drawText(heading.uppercase(), x + cardW / 2f, cardY + 22f, cardTitlePaint)
        }
        drawSignCardFrame(cLeft, "Customer Signature")
        val imgTop = cardY + 34f; val imgH = 84f
        if (signatureBitmap != null) {
            val dst = containDst(signatureBitmap, RectF(cLeft + 14f, imgTop, cLeft + cardW - 14f, imgTop + imgH))
            ps.canvas.drawBitmap(signatureBitmap, null, dst, null)
        } else {
            ps.canvas.drawText("No signature captured", cLeft + cardW / 2f, imgTop + 45f, cardEmptyPaint)
        }
        ps.canvas.drawLine(cLeft + 14f, cardY + 124f, cLeft + cardW - 14f, cardY + 124f, dividerPaint)
        val custName = SignOffParity.displayPersonName(ticket.callerName)
        ps.canvas.drawText(ellipsize(custName, cardNamePaint, cardW - 24f), cLeft + cardW / 2f, cardY + 141f, cardNamePaint)
        ps.canvas.drawText("CUSTOMER  |  $generatedDay", cLeft + cardW / 2f, cardY + 160f, cardSubPaint)
        val ax = cLeft + cardW + 14f
        drawSignCardFrame(ax, "Technician Attestation")
        val quoteLines = wrapLines("“Attested no image required. Verified and signed on file.”", quotePaint, cardW - 40f, 6)
        var qy = cardY + 48f
        quoteLines.forEach { line -> ps.canvas.drawText(line, ax + cardW / 2f, qy, centerQuotePaint); qy += 15f }
        ps.canvas.drawLine(ax + 14f, cardY + 124f, ax + cardW - 14f, cardY + 124f, dividerPaint)
        val techName = SignOffParity.displayPersonName(firstVisit?.technicianName ?: ticket.assignedTo)
        ps.canvas.drawText(ellipsize(techName, cardNamePaint, cardW - 24f), ax + cardW / 2f, cardY + 141f, cardNamePaint)
        val techDate = firstVisit?.completedAt?.trim()?.takeIf { it.isNotEmpty() }?.let { SignOffParity.formatDate(it, "") }.orEmpty()
        ps.canvas.drawText(if (techDate.isEmpty()) "TECHNICIAN-ATTESTED" else "TECHNICIAN-ATTESTED  |  $techDate", ax + cardW / 2f, cardY + 160f, cardSubPaint)
        ps.y = cardY + signCardH + 10f

        finishPage(ps.page, ps.canvas, ps.num)
        return document
    }

    // Two passes: totals ("Page n of N") are known only after pagination.
    val first = buildDocument(null)
    val totalPages = first.pages.size
    first.close()
    val document = buildDocument(totalPages)
    val fileName = "${codeNoSpaces}_SignOff_${fileStamp}.pdf"
    savePdfAndOpen(context, document, fileName)
}

// ── Processed Updates PDF ─────────────────────────────────────────────────────

fun generateProcessedUpdatesPdf(context: Context, updates: List<SetItemUpdateDto>) {
    val document = PdfDocument()
    val pageWidth = 595; val pageHeight = 842
    val margin = 32f; val cLeft = margin; val cRight = pageWidth - margin
    val cWidth = cRight - cLeft; val cBottom = pageHeight - 42f
    val generatedAt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US).format(Date())

    val green = Color.rgb(56, 142, 60); val greenDark = Color.rgb(27, 94, 32)
    val ink = Color.rgb(32, 37, 41); val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230); val softFill = Color.rgb(248, 249, 250)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = green }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val tblHdrFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(232, 245, 233) }
    val tblHdrPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = greenDark; textSize = 8.4f; isFakeBoldText = true }
    val tblCellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 9f }
    val tblMetaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val altRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(252, 252, 252) }
    val whiteRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }

    fun safeText(v: String?, fb: String = "—") = v?.trim()?.takeIf { it.isNotEmpty() } ?: fb

    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim(); if (paint.measureText(t) <= maxW) return t
        val e = "..."; var lo = 0; var hi = t.length
        while (lo < hi) { val mid = (lo + hi) / 2; if (paint.measureText(t.substring(0, mid) + e) <= maxW) lo = mid + 1 else hi = mid }
        val end = (lo - 1).coerceAtLeast(0); return if (end == 0) e else t.substring(0, end) + e
    }

    var pageNum = 1
    fun beginPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum++).create()
        val page = document.startPage(info); return page to page.canvas
    }

    data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

    fun drawBanner(canvas: Canvas): Float {
        val bannerRect = RectF(cLeft, 28f, cRight, 110f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)
        val sealRect = RectF(cLeft + 18f, 42f, cLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTxt)
        val tLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", tLeft, 53f, bannerLblPaint)
        canvas.drawText("Processed Updates", tLeft, 76f, titlePaint)
        val pillRect = RectF(tLeft, 84f, tLeft + 110f, 99f)
        canvas.drawRoundRect(pillRect, 8f, 8f, sealFillPaint)
        val pillTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 8f; isFakeBoldText = true }
        canvas.drawText("EXPORT COPY", tLeft + 8f, 95f, pillTxt)
        val rx = cRight - 155f
        canvas.drawText("TOTAL RECORDS", rx, 53f, bannerLblPaint)
        canvas.drawText("${updates.size} updates", rx, 72f, bannerValPaint)
        canvas.drawText("GENERATED", rx, 88f, bannerLblPaint)
        canvas.drawText(ellipsize(generatedAt, bannerValPaint, 145f), rx, 104f, bannerValPaint)
        return bannerRect.bottom + 18f
    }

    fun finishPage(page: PdfDocument.Page, canvas: Canvas, n: Int) {
        val fy = pageHeight - 24f
        canvas.drawLine(cLeft, fy - 10f, cRight, fy - 10f, dividerPaint)
        canvas.drawText("Yakult Philippines, Inc. — Processed Updates", cLeft, fy, footerPaint)
        canvas.drawText("Page $n", cRight - 42f, fy, footerPaint)
        document.finishPage(page)
    }

    fun newPage(): PageState {
        val n = pageNum - 1; val (p, c) = beginPage(); val y = drawBanner(c); return PageState(p, c, y, n + 1)
    }

    fun drawTableHeader(canvas: Canvas, y: Float): Float {
        val colS = cLeft + 10f; val colMd = cLeft + 170f; val colSt = cLeft + 325f; val colTs = cLeft + 420f
        val rect = RectF(cLeft, y, cRight, y + 22f)
        canvas.drawRoundRect(rect, 8f, 8f, tblHdrFill)
        canvas.drawRoundRect(rect, 8f, 8f, borderPaint)
        canvas.drawText("SERIAL NUMBER", colS, y + 15f, tblHdrPaint)
        canvas.drawText("MODEL", colMd, y + 15f, tblHdrPaint)
        canvas.drawText("STATUS", colSt, y + 15f, tblHdrPaint)
        canvas.drawText("PROCESSED AT", colTs, y + 15f, tblHdrPaint)
        return rect.bottom + 4f
    }

    var ps = newPage()
    ps.y = drawTableHeader(ps.canvas, ps.y)

    val colS = cLeft + 10f; val colMd = cLeft + 170f; val colSt = cLeft + 325f; val colTs = cLeft + 420f
    val colSW = 152f; val colMdW = 148f; val colStW = 88f; val colTsW = 150f

    updates.forEachIndexed { idx, update ->
        if (ps.y + 32f > cBottom) {
            finishPage(ps.page, ps.canvas, ps.num)
            ps = newPage()
            ps.y = drawTableHeader(ps.canvas, ps.y)
        }
        val rowRect = RectF(cLeft, ps.y, cRight, ps.y + 30f)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, if (idx % 2 == 0) whiteRowPaint else altRowPaint)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, borderPaint)
        val midY = ps.y + 19f
        ps.canvas.drawText(ellipsize(safeText(update.serialNumber), tblCellPaint, colSW), colS, midY, tblCellPaint)
        ps.canvas.drawText(ellipsize(safeText(update.modelNumber), tblMetaPaint, colMdW), colMd, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(safeText(update.newStatus), tblMetaPaint, colStW), colSt, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(safeText(update.processedAt?.replace("T", " ")), tblMetaPaint, colTsW), colTs, midY, tblMetaPaint)
        ps.y = rowRect.bottom + 3f
    }

    finishPage(ps.page, ps.canvas, ps.num)
    val fileName = "ProcessedUpdates_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context, document, fileName)
}

// ── Borrow Records PDF ────────────────────────────────────────────────────────

fun generateBorrowRecordsPdf(context: Context, records: List<BorrowLogDto>, tabTitle: String) {
    val document = PdfDocument()
    val pageWidth = 595; val pageHeight = 842
    val margin = 32f; val cLeft = margin; val cRight = pageWidth - margin
    val cWidth = cRight - cLeft; val cBottom = pageHeight - 42f
    val generatedAt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US).format(Date())
    val displayFmt = SimpleDateFormat("MMM dd, yyyy", Locale.US)
    val parsers = listOf(
        SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") },
        SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") },
        SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US)
    )
    fun fmtDate(raw: String?): String {
        val r = raw?.trim() ?: return "—"
        for (p in parsers) { try { p.isLenient = false; return displayFmt.format(p.parse(r)!!) } catch (_: Exception) {} }
        return r
    }

    val blue = Color.rgb(21, 101, 192); val blueDark = Color.rgb(13, 71, 161)
    val ink = Color.rgb(32, 37, 41); val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230); val softFill = Color.rgb(248, 249, 250)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = blue }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val tblHdrFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(227, 242, 253) }
    val tblHdrPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = blueDark; textSize = 7.8f; isFakeBoldText = true }
    val tblCellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 8.6f }
    val tblMetaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 7.8f }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val altRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(252, 252, 252) }
    val whiteRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }
    val openPillFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(255, 243, 224) }
    val openPillText = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(230, 81, 0); textSize = 7f; isFakeBoldText = true }
    val closedPillFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(232, 245, 233) }
    val closedPillText = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(27, 94, 32); textSize = 7f; isFakeBoldText = true }

    fun safeText(v: String?, fb: String = "—") = v?.trim()?.takeIf { it.isNotEmpty() } ?: fb
    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim(); if (paint.measureText(t) <= maxW) return t
        val e = "..."; var lo = 0; var hi = t.length
        while (lo < hi) { val mid = (lo + hi) / 2; if (paint.measureText(t.substring(0, mid) + e) <= maxW) lo = mid + 1 else hi = mid }
        val end = (lo - 1).coerceAtLeast(0); return if (end == 0) e else t.substring(0, end) + e
    }

    var pageNum = 1
    fun beginPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum++).create()
        val page = document.startPage(info); return page to page.canvas
    }
    data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

    fun drawBanner(canvas: Canvas): Float {
        val bannerRect = RectF(cLeft, 28f, cRight, 110f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)
        val sealRect = RectF(cLeft + 18f, 42f, cLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTxt)
        val tLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", tLeft, 53f, bannerLblPaint)
        canvas.drawText("Borrow Records — $tabTitle", tLeft, 76f, titlePaint)
        val pillRect = RectF(tLeft, 84f, tLeft + 100f, 99f)
        canvas.drawRoundRect(pillRect, 8f, 8f, sealFillPaint)
        val pillTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 8f; isFakeBoldText = true }
        canvas.drawText("EXPORT COPY", tLeft + 8f, 95f, pillTxt)
        val rx = cRight - 155f
        canvas.drawText("TOTAL RECORDS", rx, 53f, bannerLblPaint)
        canvas.drawText("${records.size} records", rx, 72f, bannerValPaint)
        canvas.drawText("GENERATED", rx, 88f, bannerLblPaint)
        canvas.drawText(ellipsize(generatedAt, bannerValPaint, 145f), rx, 104f, bannerValPaint)
        return bannerRect.bottom + 18f
    }
    fun finishPage(page: PdfDocument.Page, canvas: Canvas, n: Int) {
        val fy = pageHeight - 24f
        canvas.drawLine(cLeft, fy - 10f, cRight, fy - 10f, dividerPaint)
        canvas.drawText("Yakult Philippines, Inc. — Borrow Records", cLeft, fy, footerPaint)
        canvas.drawText("Page $n", cRight - 42f, fy, footerPaint)
        document.finishPage(page)
    }
    fun newPage(): PageState {
        val n = pageNum - 1; val (p, c) = beginPage(); val y = drawBanner(c); return PageState(p, c, y, n + 1)
    }

    // 6 columns: SERIAL | ITEM | BORROWED BY | DEPT | BORROWED AT | RETURNED AT
    val c1 = cLeft + 8f; val c2 = cLeft + 100f; val c3 = cLeft + 222f; val c4 = cLeft + 322f; val c5 = cLeft + 388f; val c6 = cLeft + 458f
    val w1 = 84f; val w2 = 114f; val w3 = 92f; val w4 = 58f; val w5 = 62f; val w6 = cRight - c6 - 8f

    fun drawTableHeader(canvas: Canvas, y: Float): Float {
        val rect = RectF(cLeft, y, cRight, y + 22f)
        canvas.drawRoundRect(rect, 8f, 8f, tblHdrFill)
        canvas.drawRoundRect(rect, 8f, 8f, borderPaint)
        canvas.drawText("SERIAL", c1, y + 15f, tblHdrPaint)
        canvas.drawText("ITEM", c2, y + 15f, tblHdrPaint)
        canvas.drawText("BORROWED BY", c3, y + 15f, tblHdrPaint)
        canvas.drawText("DEPT", c4, y + 15f, tblHdrPaint)
        canvas.drawText("BORROWED", c5, y + 15f, tblHdrPaint)
        canvas.drawText("RETURNED", c6, y + 15f, tblHdrPaint)
        return rect.bottom + 4f
    }

    var ps = newPage()
    ps.y = drawTableHeader(ps.canvas, ps.y)

    records.forEachIndexed { idx, rec ->
        if (ps.y + 38f > cBottom) {
            finishPage(ps.page, ps.canvas, ps.num)
            ps = newPage()
            ps.y = drawTableHeader(ps.canvas, ps.y)
        }
        val rowRect = RectF(cLeft, ps.y, cRight, ps.y + 36f)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, if (idx % 2 == 0) whiteRowPaint else altRowPaint)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, borderPaint)
        val midY = ps.y + 15f
        val midY2 = ps.y + 27f
        ps.canvas.drawText(ellipsize(safeText(rec.serialNumber), tblCellPaint, w1), c1, midY, tblCellPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.itemName), tblCellPaint, w2), c2, midY, tblCellPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.modelNumber), tblMetaPaint, w2), c2, midY2, tblMetaPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.borrowedByEmpName), tblCellPaint, w3), c3, midY, tblCellPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.borrowedByDeptName), tblMetaPaint, w4), c4, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(fmtDate(rec.borrowedAtUtc), tblMetaPaint, w5), c5, midY, tblMetaPaint)
        if (rec.isOpen) {
            val pr = RectF(c6, ps.y + 8f, c6 + 42f, ps.y + 22f)
            ps.canvas.drawRoundRect(pr, 6f, 6f, openPillFill)
            ps.canvas.drawText("OPEN", c6 + 5f, ps.y + 18f, openPillText)
        } else {
            val pr = RectF(c6, ps.y + 8f, c6 + 52f, ps.y + 22f)
            ps.canvas.drawRoundRect(pr, 6f, 6f, closedPillFill)
            ps.canvas.drawText(ellipsize(fmtDate(rec.returnedAtUtc), tblMetaPaint, w6), c6, midY, tblMetaPaint)
            ps.canvas.drawRoundRect(RectF(c6, ps.y + 24f, c6 + 52f, ps.y + 34f), 4f, 4f, closedPillFill)
            ps.canvas.drawText("RETURNED", c6 + 3f, ps.y + 33f, closedPillText)
        }
        ps.y = rowRect.bottom + 3f
    }

    finishPage(ps.page, ps.canvas, ps.num)
    val fileName = "BorrowRecords_${tabTitle.replace(" ", "_")}_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context, document, fileName)
}

// ── Pending Updates PDF ───────────────────────────────────────────────────────

fun generatePendingUpdatesPdf(context: Context, records: List<PendingUpdateEntity>) {
    val document = PdfDocument()
    val pageWidth = 595; val pageHeight = 842
    val margin = 32f; val cLeft = margin; val cRight = pageWidth - margin
    val cWidth = cRight - cLeft; val cBottom = pageHeight - 42f
    val generatedAt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US).format(Date())
    val queuedFmt = SimpleDateFormat("MMM dd, yyyy HH:mm", Locale.US)

    val orange = Color.rgb(230, 81, 0); val orangeDark = Color.rgb(191, 54, 12)
    val ink = Color.rgb(32, 37, 41); val muted = Color.rgb(98, 108, 119)
    val softBorder = Color.rgb(221, 226, 230)

    val bannerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = orange }
    val sealFillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(44, 255, 255, 255) }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 20f; isFakeBoldText = true }
    val bannerLblPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(200, 255, 255, 255); textSize = 9f; isFakeBoldText = true }
    val bannerValPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 12f; isFakeBoldText = true }
    val sealTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 24f; isFakeBoldText = true; textAlign = Paint.Align.CENTER }
    val tblHdrFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(255, 243, 224) }
    val tblHdrPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = orangeDark; textSize = 7.8f; isFakeBoldText = true }
    val tblCellPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = ink; textSize = 8.6f }
    val tblMetaPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 7.8f }
    val footerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = muted; textSize = 8f }
    val dividerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; strokeWidth = 1f }
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = softBorder; style = Paint.Style.STROKE; strokeWidth = 1f }
    val altRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.rgb(252, 252, 252) }
    val whiteRowPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE }

    fun safeText(v: String?, fb: String = "—") = v?.trim()?.takeIf { it.isNotEmpty() } ?: fb
    fun ellipsize(text: String, paint: Paint, maxW: Float): String {
        val t = text.trim(); if (paint.measureText(t) <= maxW) return t
        val e = "..."; var lo = 0; var hi = t.length
        while (lo < hi) { val mid = (lo + hi) / 2; if (paint.measureText(t.substring(0, mid) + e) <= maxW) lo = mid + 1 else hi = mid }
        val end = (lo - 1).coerceAtLeast(0); return if (end == 0) e else t.substring(0, end) + e
    }
    fun statusColor(status: String): Int = when (status.uppercase()) {
        "FAILED" -> Color.rgb(183, 28, 28)
        "SYNCED" -> Color.rgb(27, 94, 32)
        "SYNCING" -> Color.rgb(13, 71, 161)
        else -> Color.rgb(230, 81, 0)
    }

    var pageNum = 1
    fun beginPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNum++).create()
        val page = document.startPage(info); return page to page.canvas
    }
    data class PageState(val page: PdfDocument.Page, val canvas: Canvas, var y: Float, val num: Int)

    fun drawBanner(canvas: Canvas): Float {
        val bannerRect = RectF(cLeft, 28f, cRight, 110f)
        canvas.drawRoundRect(bannerRect, 20f, 20f, bannerPaint)
        val sealRect = RectF(cLeft + 18f, 42f, cLeft + 68f, 92f)
        canvas.drawOval(sealRect, sealFillPaint)
        canvas.drawText("Y", sealRect.centerX(), 77f, sealTxt)
        val tLeft = sealRect.right + 14f
        canvas.drawText("YAKULT PHILIPPINES, INC.", tLeft, 53f, bannerLblPaint)
        canvas.drawText("Pending Sync Queue", tLeft, 76f, titlePaint)
        val pillRect = RectF(tLeft, 84f, tLeft + 110f, 99f)
        canvas.drawRoundRect(pillRect, 8f, 8f, sealFillPaint)
        val pillTxt = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = 8f; isFakeBoldText = true }
        canvas.drawText("SNAPSHOT COPY", tLeft + 8f, 95f, pillTxt)
        val rx = cRight - 155f
        canvas.drawText("TOTAL QUEUED", rx, 53f, bannerLblPaint)
        canvas.drawText("${records.size} items", rx, 72f, bannerValPaint)
        canvas.drawText("GENERATED", rx, 88f, bannerLblPaint)
        canvas.drawText(ellipsize(generatedAt, bannerValPaint, 145f), rx, 104f, bannerValPaint)
        return bannerRect.bottom + 18f
    }
    fun finishPage(page: PdfDocument.Page, canvas: Canvas, n: Int) {
        val fy = pageHeight - 24f
        canvas.drawLine(cLeft, fy - 10f, cRight, fy - 10f, dividerPaint)
        canvas.drawText("Yakult Philippines, Inc. — Pending Sync Queue", cLeft, fy, footerPaint)
        canvas.drawText("Page $n", cRight - 42f, fy, footerPaint)
        document.finishPage(page)
    }
    fun newPage(): PageState {
        val n = pageNum - 1; val (p, c) = beginPage(); val y = drawBanner(c); return PageState(p, c, y, n + 1)
    }

    // Columns: SERIAL | SET | MODEL | PREV STATUS | NEW STATUS | SYNC | QUEUED AT
    val c1 = cLeft + 8f; val c2 = cLeft + 108f; val c3 = cLeft + 188f; val c4 = cLeft + 276f; val c5 = cLeft + 334f; val c6 = cLeft + 396f; val c7 = cLeft + 446f
    val w1 = 92f; val w2 = 72f; val w3 = 80f; val w4 = 50f; val w5 = 54f; val w6 = 42f; val w7 = cRight - c7 - 8f

    fun drawTableHeader(canvas: Canvas, y: Float): Float {
        val rect = RectF(cLeft, y, cRight, y + 22f)
        canvas.drawRoundRect(rect, 8f, 8f, tblHdrFill)
        canvas.drawRoundRect(rect, 8f, 8f, borderPaint)
        canvas.drawText("SERIAL", c1, y + 15f, tblHdrPaint)
        canvas.drawText("SET", c2, y + 15f, tblHdrPaint)
        canvas.drawText("MODEL", c3, y + 15f, tblHdrPaint)
        canvas.drawText("PREV", c4, y + 15f, tblHdrPaint)
        canvas.drawText("NEW", c5, y + 15f, tblHdrPaint)
        canvas.drawText("SYNC", c6, y + 15f, tblHdrPaint)
        canvas.drawText("QUEUED AT", c7, y + 15f, tblHdrPaint)
        return rect.bottom + 4f
    }

    var ps = newPage()
    ps.y = drawTableHeader(ps.canvas, ps.y)

    records.forEachIndexed { idx, rec ->
        if (ps.y + 32f > cBottom) {
            finishPage(ps.page, ps.canvas, ps.num)
            ps = newPage()
            ps.y = drawTableHeader(ps.canvas, ps.y)
        }
        val rowRect = RectF(cLeft, ps.y, cRight, ps.y + 30f)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, if (idx % 2 == 0) whiteRowPaint else altRowPaint)
        ps.canvas.drawRoundRect(rowRect, 6f, 6f, borderPaint)
        val midY = ps.y + 19f
        val syncColor = statusColor(rec.syncStatus.name)
        val syncPillFill = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.argb(30, Color.red(syncColor), Color.green(syncColor), Color.blue(syncColor)) }
        val syncPillText = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = syncColor; textSize = 7f; isFakeBoldText = true }
        ps.canvas.drawText(ellipsize(rec.serialNumber, tblCellPaint, w1), c1, midY, tblCellPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.setCode), tblMetaPaint, w2), c2, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.modelNumber), tblMetaPaint, w3), c3, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(safeText(rec.previousStatus), tblMetaPaint, w4), c4, midY, tblMetaPaint)
        ps.canvas.drawText(ellipsize(rec.newStatus, tblCellPaint, w5), c5, midY, tblCellPaint)
        val pillW = tblHdrPaint.measureText(rec.syncStatus.name) + 10f
        val pillRect2 = RectF(c6, ps.y + 8f, c6 + pillW, ps.y + 22f)
        ps.canvas.drawRoundRect(pillRect2, 5f, 5f, syncPillFill)
        ps.canvas.drawText(rec.syncStatus.name, c6 + 5f, ps.y + 19f, syncPillText)
        val queuedText = queuedFmt.format(Date(rec.createdAt))
        ps.canvas.drawText(ellipsize(queuedText, tblMetaPaint, w7), c7, midY, tblMetaPaint)
        ps.y = rowRect.bottom + 3f
    }

    finishPage(ps.page, ps.canvas, ps.num)
    val fileName = "PendingSync_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context, document, fileName)
}
