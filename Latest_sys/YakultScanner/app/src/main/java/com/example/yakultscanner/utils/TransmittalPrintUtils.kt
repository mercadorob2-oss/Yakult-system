package com.example.yakultscanner.utils

import android.content.ClipData
import android.content.Context
import android.content.Intent
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import android.net.Uri
import androidx.core.content.FileProvider
import com.example.yakultscanner.data.model.TransmittalReport
import java.io.File
import java.io.FileOutputStream
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private const val PRINT_PDF_MIME = "application/pdf"
private const val MOPRIA_PACKAGE = "org.mopria.printplugin"

/**
 * Creates a temporary PDF for Android's print framework. The exported XLSX is never
 * modified or replaced; this PDF exists only for the current print job.
 */
private const val TRANSIMITTAL_PAGE_WIDTH = 612
private const val TRANSIMITTAL_PAGE_HEIGHT = 792
private const val TRANSIMITTAL_SIDE_MARGIN = 30f
private const val TRANSIMITTAL_TOP_MARGIN = 24f
private const val TRANSIMITTAL_COPY_GAP = 16f

internal data class TransmittalPdfFormBounds(
    val left: Float,
    val top: Float,
    val right: Float,
    val bottom: Float
)

/** Returns the fixed portrait-page positions of the workbook's TRANSMITTAL and FILE copies. */
internal fun transmittalPdfFormBounds(
    pageWidth: Float = TRANSIMITTAL_PAGE_WIDTH.toFloat(),
    pageHeight: Float = TRANSIMITTAL_PAGE_HEIGHT.toFloat()
): List<TransmittalPdfFormBounds> {
    val usableHeight = pageHeight - TRANSIMITTAL_TOP_MARGIN * 2f - TRANSIMITTAL_COPY_GAP
    val formHeight = usableHeight / 2f
    val left = TRANSIMITTAL_SIDE_MARGIN
    val right = pageWidth - TRANSIMITTAL_SIDE_MARGIN
    val firstTop = TRANSIMITTAL_TOP_MARGIN
    return listOf(
        TransmittalPdfFormBounds(left, firstTop, right, firstTop + formHeight),
        TransmittalPdfFormBounds(left, firstTop + formHeight + TRANSIMITTAL_COPY_GAP, right, pageHeight - TRANSIMITTAL_TOP_MARGIN)
    )
}

internal fun transmittalPdfPageCount(itemCount: Int): Int =
    maxOf(1, (itemCount.coerceAtLeast(0) + TransmittalReport.TEMPLATE_ITEM_COUNT - 1) / TransmittalReport.TEMPLATE_ITEM_COUNT)

/**
 * Creates a portrait PDF that reproduces the printable layout of transmittal_template.xlsx.
 * Each page contains its two fixed copies: TRANSMITTAL and FILE, each with twelve item lines.
 */
fun createTransmittalPrintPdf(context: Context, report: TransmittalReport): Uri {
    val directory = File(context.cacheDir, "transmittal-print").apply { mkdirs() }
    val file = File(directory, "Transmittal_${fileStamp()}_excel_form_v3.pdf")
    val document = PdfDocument()
    try {
        val pageCount = transmittalPdfPageCount(report.items.size)
        repeat(pageCount) { pageIndex ->
            val page = document.startPage(
                PdfDocument.PageInfo.Builder(TRANSIMITTAL_PAGE_WIDTH, TRANSIMITTAL_PAGE_HEIGHT, pageIndex + 1).create()
            )
            val firstItemIndex = pageIndex * TransmittalReport.TEMPLATE_ITEM_COUNT
            val lines = List(TransmittalReport.TEMPLATE_ITEM_COUNT) { rowIndex ->
                report.lineText(firstItemIndex + rowIndex)
            }
            val bounds = transmittalPdfFormBounds()
            drawTransmittalForm(page.canvas, bounds[0], "TRANSMITTAL", report, lines, firstItemIndex)
            drawTransmittalForm(page.canvas, bounds[1], "FILE", report, lines, firstItemIndex)
            document.finishPage(page)
        }
        FileOutputStream(file).use { output -> document.writeTo(output) }
    } finally {
        document.close()
    }
    return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
}

private fun drawTransmittalForm(
    canvas: android.graphics.Canvas,
    bounds: TransmittalPdfFormBounds,
    copyLabel: String,
    report: TransmittalReport,
    lines: List<String>,
    firstItemIndex: Int
) {
    val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        style = Paint.Style.STROKE
        strokeWidth = 0.8f
    }
    val boldBorderPaint = Paint(borderPaint).apply { strokeWidth = 1.35f }
    val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textAlign = Paint.Align.CENTER
        textSize = 12f
    }
    val brandPaint = Paint(titlePaint).apply {
        textAlign = Paint.Align.LEFT
        textSize = 14f
    }
    val labelPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textSize = 6.8f
    }
    val valuePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        textSize = 7.2f
    }
    val itemPaint = Paint(valuePaint).apply { textSize = 6.6f }
    val itemNumberPaint = Paint(itemPaint).apply { textAlign = Paint.Align.CENTER }

    val x = bounds.left
    val width = bounds.right - bounds.left
    val y = bounds.top
    val headerBottom = y + 44f
    val fieldRowHeight = 15f
    val tableTop = headerBottom + fieldRowHeight * 2f
    val tableHeaderHeight = 14f
    val tableBottom = bounds.bottom - 65f
    val itemRowHeight = (tableBottom - tableTop - tableHeaderHeight) / TransmittalReport.TEMPLATE_ITEM_COUNT
    val numberColumn = x + width * 0.075f
    val dateColumn = x + width * 0.74f

    canvas.drawRect(x, y, bounds.right, bounds.bottom, boldBorderPaint)
    canvas.drawLine(x, headerBottom, bounds.right, headerBottom, borderPaint)
    canvas.drawLine(x, headerBottom + fieldRowHeight, bounds.right, headerBottom + fieldRowHeight, borderPaint)
    canvas.drawLine(dateColumn, headerBottom, dateColumn, tableTop, borderPaint)

    canvas.drawText("YAKULT", x + 9f, y + 17f, brandPaint)
    canvas.drawText("YAKULT PHILIPPINES, INC.", x + 9f, y + 29f, labelPaint)
    canvas.drawText(copyLabel, x + width * 0.72f, y + 24f, titlePaint)
    canvas.drawText("TO:", x + 5f, headerBottom + 10f, labelPaint)
    drawFittedText(canvas, report.to, x + 25f, headerBottom + 10f, dateColumn - x - 30f, valuePaint)
    canvas.drawText("DATE:", dateColumn + 5f, headerBottom + 10f, labelPaint)
    drawFittedText(canvas, report.date, dateColumn + 33f, headerBottom + 10f, bounds.right - dateColumn - 38f, valuePaint)
    canvas.drawText("FROM:", x + 5f, headerBottom + fieldRowHeight + 10f, labelPaint)
    drawFittedText(canvas, report.from, x + 31f, headerBottom + fieldRowHeight + 10f, bounds.right - x - 36f, valuePaint)

    canvas.drawRect(x, tableTop, bounds.right, tableBottom, borderPaint)
    canvas.drawLine(numberColumn, tableTop, numberColumn, tableBottom, borderPaint)
    canvas.drawLine(x, tableTop + tableHeaderHeight, bounds.right, tableTop + tableHeaderHeight, borderPaint)
    canvas.drawText("NO.", x + (numberColumn - x) / 2f, tableTop + 9.5f, itemNumberPaint)
    canvas.drawText("DESCRIPTION / PARTICULARS", numberColumn + 5f, tableTop + 9.5f, labelPaint)
    repeat(TransmittalReport.TEMPLATE_ITEM_COUNT) { rowIndex ->
        val rowTop = tableTop + tableHeaderHeight + rowIndex * itemRowHeight
        canvas.drawLine(x, rowTop + itemRowHeight, bounds.right, rowTop + itemRowHeight, borderPaint)
        canvas.drawText((firstItemIndex + rowIndex + 1).toString(), x + (numberColumn - x) / 2f, rowTop + itemRowHeight * 0.68f, itemNumberPaint)
        drawFittedText(canvas, lines.getOrElse(rowIndex) { "" }, numberColumn + 5f, rowTop + itemRowHeight * 0.68f, bounds.right - numberColumn - 10f, itemPaint)
    }

    val signTop = tableBottom
    canvas.drawLine(x, signTop + 16f, bounds.right, signTop + 16f, borderPaint)
    canvas.drawLine(x, signTop + 32f, bounds.right, signTop + 32f, borderPaint)
    canvas.drawLine(x + width * 0.5f, signTop, x + width * 0.5f, signTop + 48f, borderPaint)
    canvas.drawLine(x, signTop + 48f, bounds.right, signTop + 48f, borderPaint)
    canvas.drawText("PREPARED BY:", x + 5f, signTop + 10.5f, labelPaint)
    drawFittedText(canvas, report.preparedBy, x + 66f, signTop + 10.5f, width * 0.5f - 72f, valuePaint)
    canvas.drawText("RECEIVED BY / DATE:", x + width * 0.5f + 5f, signTop + 10.5f, labelPaint)
    drawFittedText(canvas, report.receivedByDate, x + width * 0.5f + 88f, signTop + 10.5f, width * 0.5f - 94f, valuePaint)
    canvas.drawText("TRANSMITTED BY:", x + 5f, signTop + 26.5f, labelPaint)
    drawFittedText(canvas, report.transmitBy, x + 78f, signTop + 26.5f, width * 0.5f - 84f, valuePaint)
    canvas.drawText("APPROVED BY / DATE:", x + width * 0.5f + 5f, signTop + 26.5f, labelPaint)
    drawFittedText(canvas, report.approvedByDate, x + width * 0.5f + 92f, signTop + 26.5f, width * 0.5f - 98f, valuePaint)
    canvas.drawText("NOTED BY:", x + 5f, signTop + 42.5f, labelPaint)
    drawFittedText(canvas, report.notedBy, x + 51f, signTop + 42.5f, width - 56f, valuePaint)
    canvas.drawText("${copyLabel.lowercase(Locale.US)} copy", bounds.right - 56f, bounds.bottom - 5f, labelPaint)
}

private fun drawFittedText(
    canvas: android.graphics.Canvas,
    value: String,
    x: Float,
    baseline: Float,
    availableWidth: Float,
    paint: Paint
) {
    if (value.isBlank() || availableWidth <= 0f) return
    val ellipsis = "…"
    val text = if (paint.measureText(value) <= availableWidth) {
        value
    } else {
        value.takeWhileIndexed { index -> paint.measureText(value.take(index + 1) + ellipsis) <= availableWidth } + ellipsis
    }
    canvas.drawText(text, x, baseline, paint)
}

private inline fun String.takeWhileIndexed(predicate: (Int) -> Boolean): String {
    var endIndex = 0
    while (endIndex < length && predicate(endIndex)) endIndex++
    return take(endIndex)
}

private fun fileStamp(): String = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(Date())

fun shareTransmittalPrintPdf(context: Context, pdfUri: Uri): Boolean {
    return runCatching {
        val intent = android.content.Intent(android.content.Intent.ACTION_SEND).apply {
            type = PRINT_PDF_MIME
            putExtra(android.content.Intent.EXTRA_STREAM, pdfUri)
            addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
        context.startActivity(android.content.Intent.createChooser(intent, "Send transmittal PDF to printer"))
        true
    }.getOrDefault(false)
}

/**
 * Sends a saved PDF directly to Mopria when its PDF activity is available. Devices without
 * Mopria continue through Android's standard PrintManager flow and any enabled print service.
 */
fun printTransmittalPdf(context: Context, pdfUri: Uri, jobName: String): Boolean {
    return runCatching {
        val mopriaIntent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(pdfUri, PRINT_PDF_MIME)
            clipData = ClipData.newRawUri(jobName, pdfUri)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            setPackage(MOPRIA_PACKAGE)
        }
        val mopriaAvailable = context.packageManager.resolveActivity(
            mopriaIntent,
            android.content.pm.PackageManager.MATCH_DEFAULT_ONLY
        ) != null

        if (mopriaAvailable) {
            context.startActivity(mopriaIntent)
        } else {
            val printManager = context.getSystemService(Context.PRINT_SERVICE) as android.print.PrintManager
            printManager.print(jobName, PdfPrintAdapter(context, pdfUri), null)
        }
        true
    }.getOrDefault(false)
}

private class PdfPrintAdapter(
    private val context: Context,
    private val uri: Uri
) : android.print.PrintDocumentAdapter() {
    override fun onLayout(
        oldAttributes: android.print.PrintAttributes?,
        newAttributes: android.print.PrintAttributes,
        cancellationSignal: android.os.CancellationSignal,
        callback: LayoutResultCallback,
        extras: android.os.Bundle?
    ) {
        if (cancellationSignal.isCanceled) {
            callback.onLayoutCancelled()
            return
        }
        callback.onLayoutFinished(
            android.print.PrintDocumentInfo.Builder("transmittal.pdf")
                .setContentType(android.print.PrintDocumentInfo.CONTENT_TYPE_DOCUMENT)
                .build(),
            true
        )
    }

    override fun onWrite(
        pages: Array<out android.print.PageRange>,
        destination: android.os.ParcelFileDescriptor,
        cancellationSignal: android.os.CancellationSignal,
        callback: WriteResultCallback
    ) {
        try {
            if (cancellationSignal.isCanceled) {
                callback.onWriteCancelled()
                return
            }
            context.contentResolver.openFileDescriptor(uri, "r")?.use { source ->
                android.os.FileUtils.copy(source.fileDescriptor, destination.fileDescriptor)
            } ?: throw IllegalStateException("Could not open the temporary print document.")
            callback.onWriteFinished(arrayOf(android.print.PageRange.ALL_PAGES))
        } catch (error: Throwable) {
            callback.onWriteFailed(error.message)
        } finally {
            destination.close()
        }
    }
}
