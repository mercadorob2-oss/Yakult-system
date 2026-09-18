package com.example.yakultscanner.utils

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.graphics.pdf.PdfDocument
import com.example.yakultscanner.data.db.LocalScanItemEntity
import com.example.yakultscanner.data.db.LocalScanSessionEntity
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

fun generateLocalScanReportPdf(
    context: Context,
    session: LocalScanSessionEntity,
    items: List<LocalScanItemEntity>
) {
    val document = PdfDocument()
    val pageWidth = 595
    val pageHeight = 842
    val margin = 36f
    val bottom = pageHeight - 44f
    var pageNumber = 1

    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(183, 28, 28)
        textSize = 18f
        typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD)
    }
    val headerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(40, 40, 40)
        textSize = 10f
        typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD)
    }
    val bodyPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(55, 55, 55)
        textSize = 9f
    }
    val smallPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(100, 100, 100)
        textSize = 8f
    }
    val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.rgb(220, 220, 220)
        strokeWidth = 1f
    }

    fun newPage(): Pair<PdfDocument.Page, Canvas> {
        val info = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNumber++).create()
        val page = document.startPage(info)
        return page to page.canvas
    }

    fun finishPage(page: PdfDocument.Page, canvas: Canvas, number: Int) {
        canvas.drawLine(margin, bottom + 12f, pageWidth - margin, bottom + 12f, linePaint)
        canvas.drawText("Yakult Local Serial Scan Report", margin, bottom + 28f, smallPaint)
        canvas.drawText("Page $number", pageWidth - margin - 40f, bottom + 28f, smallPaint)
        document.finishPage(page)
    }

    var current = newPage()
    var page = current.first
    var canvas = current.second
    var y = margin
    var currentPageNumber = 1
    val stamp = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault()).format(Date(session.createdAt))

    canvas.drawText(session.title, margin, y, titlePaint)
    y += 18f
    canvas.drawText("Created: $stamp    By: ${session.createdBy.orEmpty().ifBlank { "Unknown" }}    Items: ${items.size}", margin, y, bodyPaint)
    y += 18f
    canvas.drawLine(margin, y, pageWidth - margin, y, linePaint)
    y += 16f

    fun drawHeader() {
        canvas.drawText("#", margin, y, headerPaint)
        canvas.drawText("Serial", margin + 26f, y, headerPaint)
        canvas.drawText("Cellphone", margin + 158f, y, headerPaint)
        canvas.drawText("IMEI 1", margin + 278f, y, headerPaint)
        canvas.drawText("IMEI 2", margin + 398f, y, headerPaint)
        y += 10f
        canvas.drawLine(margin, y, pageWidth - margin, y, linePaint)
        y += 12f
    }

    fun clip(value: String?, max: Int): String {
        val text = value.orEmpty()
        return if (text.length <= max) text else text.take(max - 1) + "..."
    }

    drawHeader()
    items.forEach { item ->
        if (y > bottom - 24f) {
            finishPage(page, canvas, currentPageNumber++)
            current = newPage()
            page = current.first
            canvas = current.second
            y = margin
            drawHeader()
        }
        canvas.drawText(item.rowNumber.toString(), margin, y, bodyPaint)
        canvas.drawText(clip(item.serialNumber, 22), margin + 26f, y, bodyPaint)
        canvas.drawText(clip(item.cellPhoneNumber, 18), margin + 158f, y, bodyPaint)
        canvas.drawText(clip(item.imei1, 18), margin + 278f, y, bodyPaint)
        canvas.drawText(clip(item.imei2, 18), margin + 398f, y, bodyPaint)
        y += 14f
    }

    finishPage(page, canvas, currentPageNumber)
    val fileStamp = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(Date())
    savePdfAndOpen(context, document, "LocalSerialScan_${fileStamp}.pdf")
}
