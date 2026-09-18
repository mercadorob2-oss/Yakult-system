package com.example.yakultscanner.utils

import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.graphics.RectF
import android.graphics.pdf.PdfDocument
import android.net.Uri
import androidx.core.content.FileProvider
import com.example.yakultscanner.data.model.GatepassFormType
import com.example.yakultscanner.data.model.GatepassItemCategory
import com.example.yakultscanner.data.model.GatepassReport
import com.example.yakultscanner.data.model.TransmittalItemMode
import java.io.File
import java.io.FileOutputStream
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

internal const val GATEPASS_PAGE_WIDTH = 612
internal const val GATEPASS_PAGE_HEIGHT = 1008
internal const val GATEPASS_COPIES_PER_PAGE = 2
internal const val GATEPASS_COPY_HEIGHT = GATEPASS_PAGE_HEIGHT / GATEPASS_COPIES_PER_PAGE
private const val GATEPASS_MARGIN_X = 58f
private const val GATEPASS_MARGIN_Y = 12f

internal fun gatepassPdfPageCount(itemCount: Int, printAllFormTypes: Boolean = false): Int {
    val logicalPageCount = maxOf(
        1,
        (itemCount.coerceAtLeast(0) + GatepassReport.TEMPLATE_ITEM_COUNT - 1) /
            GatepassReport.TEMPLATE_ITEM_COUNT
    )
    return if (printAllFormTypes) logicalPageCount * 2 else logicalPageCount
}

internal fun gatepassPdfCopyTopOffsets(): List<Float> =
    List(GATEPASS_COPIES_PER_PAGE) { index -> index * GATEPASS_COPY_HEIGHT.toFloat() }

/**
 * Returns the two half-page slots for one physical page in all-three mode.
 * A null slot is the data-only copy and must not draw checkboxes, underlines, boxes, or other
 * form input controls.
 */
internal fun gatepassPdfAllThreeFormTypesForPage(pageIndex: Int): List<GatepassFormType?> =
    if (pageIndex % 2 == 0) {
        listOf(GatepassFormType.Gatepass, GatepassFormType.Transmittal)
    } else {
        listOf(GatepassFormType.File, null)
    }

/**
 * Creates a local Legal-size PDF with two compact Gatepass/File Transmittal copies on every
 * physical page by default. When [GatepassReport.printAllFormTypes] is enabled, each logical
 * twelve-line item page expands to two physical sheets:
 *   - sheet one: Gatepass on top and Transmittal below
 *   - sheet two: File on top and a data-only copy below with no form input controls
 *
 * The generated document is intentionally independent from the API and remote database.
 */
fun createGatepassPrintPdf(context: android.content.Context, report: GatepassReport): Uri {
    val directory = File(context.cacheDir, "gatepass-print").apply { mkdirs() }
    val file = File(directory, "Gatepass_${fileStamp()}.pdf")
    val document = PdfDocument()
    try {
        val printAllFormTypes = report.printAllFormTypes
        val pageCount = gatepassPdfPageCount(report.items.size, printAllFormTypes)
        repeat(pageCount) { pageIndex ->
            val page = document.startPage(
                PdfDocument.PageInfo.Builder(
                    GATEPASS_PAGE_WIDTH,
                    GATEPASS_PAGE_HEIGHT,
                    pageIndex + 1
                ).create()
            )
            page.canvas.drawColor(android.graphics.Color.WHITE)
            val logicalPageIndex = if (printAllFormTypes) pageIndex / 2 else pageIndex
            val firstItemIndex = logicalPageIndex * GatepassReport.TEMPLATE_ITEM_COUNT
            val lines = List(GatepassReport.TEMPLATE_ITEM_COUNT) { rowIndex ->
                report.items.getOrNull(firstItemIndex + rowIndex)
                    ?.reportLine(TransmittalItemMode.Standard)
                    .orEmpty()
            }

            if (printAllFormTypes) {
                gatepassPdfAllThreeFormTypesForPage(pageIndex).forEachIndexed { slotIndex, formType ->
                    if (formType != null) {
                        drawGatepassForm(
                            canvas = page.canvas,
                            report = report,
                            lines = lines,
                            copyTop = gatepassPdfCopyTopOffsets()[slotIndex],
                            formType = formType
                        )
                    } else {
                        drawGatepassDataOnly(
                            canvas = page.canvas,
                            report = report,
                            lines = lines,
                            copyTop = gatepassPdfCopyTopOffsets()[slotIndex]
                        )
                    }
                }
            } else {
                gatepassPdfCopyTopOffsets().forEach { copyTop ->
                    drawGatepassForm(page.canvas, report, lines, copyTop)
                }
            }
            document.finishPage(page)
        }
        FileOutputStream(file).use { output -> document.writeTo(output) }
    } finally {
        document.close()
    }
    return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
}

private fun drawGatepassForm(
    canvas: Canvas,
    report: GatepassReport,
    lines: List<String>,
    copyTop: Float,
    formType: GatepassFormType = report.formType
) {
    val line = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        style = Paint.Style.STROKE
        strokeWidth = 0.7f
    }
    val heading = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textAlign = Paint.Align.LEFT
        textSize = 10.2f
    }
    val label = Paint(heading).apply { textSize = 8.0f }
    val value = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        textSize = 8.6f
    }
    val plainValue = Paint(value).apply { typeface = android.graphics.Typeface.DEFAULT_BOLD }
    val small = Paint(value).apply { textSize = 7.4f }
    val categoryPaint = Paint(small).apply {
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textSize = 8.1f
    }
    val headerPaint = Paint(value).apply {
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textSize = 8.0f
    }
    val companyPaint = Paint(value).apply {
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textSize = 7.4f
    }
    val quantityHeading = Paint(heading).apply { textAlign = Paint.Align.CENTER }

    val left = GATEPASS_MARGIN_X
    val right = GATEPASS_PAGE_WIDTH - GATEPASS_MARGIN_X
    val top = copyTop + GATEPASS_MARGIN_Y
    val width = right - left

    val companyX = left + width * 0.34f
    drawCompanyOption(canvas, companyX, top + 10f, report.company.name == "YakultPhilippines", "YAKULT PHILIPPINES INC.", companyPaint)
    drawCompanyOption(canvas, companyX, top + 23f, report.company.name == "YakultMarketing", "YAKULT MARKETING CORP.", companyPaint)
    drawHeaderOption(canvas, left + 70f, top + 48f, formType == GatepassFormType.Gatepass, "GATEPASS", headerPaint)
    drawHeaderOption(canvas, left + 215f, top + 48f, formType == GatepassFormType.Transmittal, "TRANSMITTAL", headerPaint)
    drawHeaderOption(canvas, left + 385f, top + 48f, formType == GatepassFormType.File, "FILE", headerPaint)

    val detailsTop = top + 70f
    drawLabeledText(canvas, "TO:", report.to, left + 10f, detailsTop + 13f, width * 0.55f, label, plainValue)
    drawLabeledLine(canvas, "DATE:", report.date, left + width * 0.68f, detailsTop + 13f, width * 0.26f, label, value)
    drawLabeledText(canvas, "FROM:", report.from, left + 10f, detailsTop + 32f, width * 0.55f, label, plainValue)

    val contentTop = top + 116f
    val modelColumn = left + width * 0.37f
    val quantityColumn = left + width * 0.77f
    canvas.drawText("ITEMS:", left, contentTop + 12f, heading)

    GatepassItemCategory.entries
        .filter { it != GatepassItemCategory.Others }
        .forEachIndexed { index, category ->
            val rowY = contentTop + 24f + index * 20.5f
            val selected = report.itemCategory == category
            val categoryLabel = if (category == GatepassItemCategory.ComputerTableFixedAsset) {
                "COMPUTER TABLE FIXED ASSET NO."
            } else {
                category.label.uppercase(Locale.US)
            }
            drawCheckbox(canvas, left + 28f, rowY, selected, categoryLabel, categoryPaint)
            if (category == GatepassItemCategory.ComputerTableFixedAsset) {
                val lineStart = left + 6f + 15f + categoryPaint.measureText(categoryLabel) + 5f
                val lineEnd = quantityColumn - 115f
                canvas.drawLine(lineStart, rowY + 9f, lineEnd, rowY + 9f, line)
            }
        }

    drawCheckbox(canvas, modelColumn, contentTop + 24f, report.model?.name == "Lx300", "LX300", small)
    drawCheckbox(canvas, modelColumn + 110f, contentTop + 24f, report.model?.name == "Lx310", "LX310", small)
    drawLabeledLine(canvas, "FIXED ASSET NO.:", report.fixedAssetNumber, modelColumn, contentTop + 67f, quantityColumn - modelColumn - 55f, small, value)

    val quantityCenter = quantityColumn + (right - quantityColumn) / 2f
    canvas.drawText("QUANTITY", quantityCenter, contentTop + 12f, quantityHeading)
    val quantityLine = contentTop + 48f
    canvas.drawLine(quantityColumn, quantityLine, right, quantityLine, line)
    drawFittedText(canvas, report.quantity, quantityColumn + 2f, quantityLine - 2f, right - quantityColumn - 4f, value)
    repeat(GatepassReport.TEMPLATE_ITEM_COUNT) { index ->
        val itemLine = quantityLine + 14f + index * 14f
        canvas.drawLine(quantityColumn, itemLine, right, itemLine, line)
        drawFittedText(
            canvas,
            lines.getOrElse(index) { "" },
            quantityColumn + 2f,
            itemLine - 2f,
            right - quantityColumn - 4f,
            small
        )
    }

    val notesTop = contentTop + 226f
    drawLabeledLine(canvas, "OTHERS:", report.others, left + 10f, notesTop + 12f, width - 20f, label, value)
    drawLabeledLine(canvas, "REMARKS:", report.remarks, left + 10f, notesTop + 32f, width - 20f, label, value)

    val signatureTop = contentTop + 275f
    val signatureWidth = width * 0.42f
    drawLabeledLine(canvas, "ISSUED BY:", report.issuedBy, left + 10f, signatureTop + 15f, signatureWidth, label, value)
    drawLabeledLine(canvas, "NOTED BY:", report.notedBy, left + 10f, signatureTop + 38f, signatureWidth, label, value)
    drawLabeledLine(canvas, "RECEIVED BY / DATE:", report.receivedByDate, left + width / 2f + 10f, signatureTop + 15f, signatureWidth, label, value)
    drawLabeledLine(canvas, "APPROVED BY / DATE:", report.approvedByDate, left + width / 2f + 10f, signatureTop + 38f, signatureWidth, label, value)
}

internal fun gatepassPdfDataSummaryItemLines(lines: List<String>): List<String> =
    lines.mapIndexedNotNull { index, line ->
        line.trim().takeIf { it.isNotBlank() }?.let { "${index + 1}. $it" }
    }

private fun drawGatepassDataOnly(
    canvas: Canvas,
    report: GatepassReport,
    lines: List<String>,
    copyTop: Float
) {
    val label = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        typeface = android.graphics.Typeface.DEFAULT_BOLD
        textSize = 7.2f
    }
    val value = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = android.graphics.Color.BLACK
        textSize = 7.8f
    }
    val itemValue = Paint(value).apply { textSize = 7.3f }
    val section = Paint(label).apply { textSize = 8.0f }
    val title = Paint(label).apply {
        textAlign = Paint.Align.CENTER
        textSize = 10.6f
    }
    val subtitle = Paint(value).apply {
        textAlign = Paint.Align.CENTER
        textSize = 6.8f
    }
    val left = GATEPASS_MARGIN_X
    val right = GATEPASS_PAGE_WIDTH - GATEPASS_MARGIN_X
    val width = right - left
    val top = copyTop + GATEPASS_MARGIN_Y
    val center = (left + right) / 2f
    val columnGap = 14f
    val columnWidth = (width - columnGap) / 2f
    val rightColumn = left + columnWidth + columnGap

    canvas.drawText("ITEMS & DATA SUMMARY", center, top + 16f, title)
    canvas.drawText("Entered information and listed items", center, top + 28f, subtitle)
    drawSummaryField(
        canvas,
        "COMPANY:",
        report.company.label.uppercase(Locale.US),
        left,
        top + 43f,
        width,
        label,
        value
    )
    drawSummaryField(
        canvas,
        "FORM TYPES:",
        "Gatepass / Transmittal / File",
        left,
        top + 56f,
        width,
        label,
        value
    )

    canvas.drawText("DOCUMENT DATA", left, top + 73f, section)
    drawSummaryField(canvas, "TO:", report.to, left, top + 87f, columnWidth, label, value)
    drawSummaryField(canvas, "DATE:", report.date, rightColumn, top + 87f, columnWidth, label, value)
    drawSummaryField(canvas, "FROM:", report.from, left, top + 100f, width, label, value)

    val categoryText = buildString {
        append(report.categoryLabel())
        if (report.itemCategory == GatepassItemCategory.Others && report.itemCategoryOther.isNotBlank()) {
            append(" - ")
            append(report.itemCategoryOther)
        }
    }
    drawSummaryField(canvas, "ITEM:", categoryText, left, top + 113f, columnWidth, label, value)
    drawSummaryField(canvas, "MODEL:", report.model?.label.orEmpty(), rightColumn, top + 113f, columnWidth, label, value)
    drawSummaryField(canvas, "ASSET NO.:", report.fixedAssetNumber, left, top + 126f, columnWidth, label, value)
    drawSummaryField(canvas, "QTY:", report.quantity, rightColumn, top + 126f, columnWidth, label, value)

    val itemSummaryLines = gatepassPdfDataSummaryItemLines(lines)
    val displayedItems = itemSummaryLines.ifEmpty { listOf("No additional listed item lines") }
    canvas.drawText("LISTED ITEMS", left, top + 145f, section)
    val splitIndex = (displayedItems.size + 1) / 2
    val itemRows = maxOf(1, splitIndex)
    for (row in 0 until itemRows) {
        displayedItems.getOrNull(row)?.let { itemLine ->
            drawFittedText(canvas, itemLine, left, top + 158f + row * 12f, columnWidth, itemValue)
        }
        displayedItems.getOrNull(row + splitIndex)?.let { itemLine ->
            drawFittedText(canvas, itemLine, rightColumn, top + 158f + row * 12f, columnWidth, itemValue)
        }
    }

    var nextBaseline = top + 158f + itemRows * 12f + 7f
    val hasAdditionalData = report.others.isNotBlank() || report.remarks.isNotBlank()
    if (hasAdditionalData) {
        canvas.drawText("ADDITIONAL DATA", left, nextBaseline, section)
        nextBaseline += 13f
        drawSummaryField(canvas, "OTHERS:", report.others, left, nextBaseline, width, label, value)
        nextBaseline += 13f
        drawSummaryField(canvas, "REMARKS:", report.remarks, left, nextBaseline, width, label, value)
        nextBaseline += 5f
    }

    val hasSignoffData = listOf(
        report.issuedBy,
        report.notedBy,
        report.receivedByDate,
        report.approvedByDate
    ).any { it.isNotBlank() }
    if (hasSignoffData) {
        canvas.drawText("SIGN-OFF DATA", left, nextBaseline, section)
        nextBaseline += 13f
        drawSummaryField(canvas, "ISSUED BY:", report.issuedBy, left, nextBaseline, columnWidth, label, value)
        drawSummaryField(canvas, "RECEIVED BY / DATE:", report.receivedByDate, rightColumn, nextBaseline, columnWidth, label, value)
        nextBaseline += 13f
        drawSummaryField(canvas, "NOTED BY:", report.notedBy, left, nextBaseline, columnWidth, label, value)
        drawSummaryField(canvas, "APPROVED BY / DATE:", report.approvedByDate, rightColumn, nextBaseline, columnWidth, label, value)
    }
}

private fun drawSummaryField(
    canvas: Canvas,
    label: String,
    value: String,
    x: Float,
    baseline: Float,
    width: Float,
    labelPaint: Paint,
    valuePaint: Paint
) {
    if (value.isBlank() || width <= 0f) return
    canvas.drawText(label, x, baseline, labelPaint)
    val valueX = x + labelPaint.measureText(label) + 4f
    drawFittedText(canvas, value, valueX, baseline, width - (valueX - x), valuePaint)
}

private fun drawHeaderOption(
    canvas: Canvas,
    x: Float,
    baseline: Float,
    checked: Boolean,
    text: String,
    paint: Paint
) {
    val mark = if (checked) "(✓)" else "( )"
    canvas.drawText(text, x, baseline, paint)
    canvas.drawText(mark, x + paint.measureText(text) + 5f, baseline, paint)
}

private fun drawCompanyOption(
    canvas: Canvas,
    x: Float,
    baseline: Float,
    checked: Boolean,
    text: String,
    paint: Paint
) {
    val mark = if (checked) "(✓)" else "( )"
    canvas.drawText(mark, x, baseline, paint)
    canvas.drawText(text, x + paint.measureText(mark) + 5f, baseline, paint)
}

private fun drawCheckbox(
    canvas: Canvas,
    x: Float,
    y: Float,
    checked: Boolean,
    text: String,
    paint: Paint
) {
    val boxSize = 10f
    val box = RectF(x, y, x + boxSize, y + boxSize)
    canvas.drawRect(box, paintForBox(paint))
    if (checked) {
        val check = Path().apply {
            moveTo(x + 2f, y + 5f)
            lineTo(x + 4.5f, y + 8f)
            lineTo(x + 8.5f, y + 2f)
        }
        canvas.drawPath(check, paintForCheck(paint))
    }
    canvas.drawText(text, x + 15f, y + 9f, paint)
}

private fun paintForBox(source: Paint): Paint = Paint(source).apply {
    style = Paint.Style.STROKE
    strokeWidth = 0.8f
}

private fun paintForCheck(source: Paint): Paint = Paint(source).apply {
    style = Paint.Style.STROKE
    strokeWidth = 1.2f
}

private fun drawLabeledText(
    canvas: Canvas,
    label: String,
    value: String,
    x: Float,
    baseline: Float,
    width: Float,
    labelPaint: Paint,
    valuePaint: Paint
) {
    canvas.drawText(label, x, baseline, labelPaint)
    val valueX = x + labelPaint.measureText(label) + 4f
    drawFittedText(canvas, value, valueX, baseline, width - (valueX - x), valuePaint)
}

private fun drawLabeledLine(
    canvas: Canvas,
    label: String,
    value: String,
    x: Float,
    baseline: Float,
    width: Float,
    labelPaint: Paint,
    valuePaint: Paint
) {
    canvas.drawText(label, x, baseline, labelPaint)
    val valueX = x + labelPaint.measureText(label) + 4f
    val lineRight = x + width
    canvas.drawLine(valueX, baseline + 2f, lineRight, baseline + 2f, labelPaint)
    drawFittedText(canvas, value, valueX + 2f, baseline - 1f, lineRight - valueX - 4f, valuePaint)
}

private fun drawFittedText(
    canvas: Canvas,
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
        var end = 0
        while (end < value.length && paint.measureText(value.take(end + 1) + ellipsis) <= availableWidth) end++
        value.take(end) + ellipsis
    }
    canvas.drawText(text, x, baseline, paint)
}

private fun fileStamp(): String =
    SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(Date())
