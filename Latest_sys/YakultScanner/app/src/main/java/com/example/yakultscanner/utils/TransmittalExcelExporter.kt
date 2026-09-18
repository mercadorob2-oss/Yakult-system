package com.example.yakultscanner.utils

import android.content.ContentValues
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import androidx.core.content.FileProvider
import com.example.yakultscanner.data.model.TransmittalReport
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileOutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipInputStream
import java.util.zip.ZipOutputStream

private const val TEMPLATE_ASSET = "transmittal_template.xlsx"
private const val EXCEL_MIME = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

data class SavedTransmittalWorkbook(
    val displayName: String,
    val uri: Uri
)

suspend fun exportTransmittalWorkbook(
    context: Context,
    report: TransmittalReport
): Result<SavedTransmittalWorkbook> = withContext(Dispatchers.IO) {
    runCatching {
        val templateBytes = context.assets.open(TEMPLATE_ASSET).use { it.readBytes() }
        val workbookBytes = populateTemplate(templateBytes, report)
        val fileName = "Transmittal_${fileStamp()}.xlsx"
        saveWorkbook(context, fileName, workbookBytes)
    }
}

fun openTransmittalWorkbook(context: Context, workbook: SavedTransmittalWorkbook): Boolean {
    return runCatching {
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(workbook.uri, EXCEL_MIME)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NO_HISTORY)
        }
        context.startActivity(Intent.createChooser(intent, "Open Excel report"))
        true
    }.getOrDefault(false)
}

private const val SHARED_STRINGS_PATH = "xl/sharedStrings.xml"

private data class SharedStringTable(
    val xml: String,
    val indexByValue: Map<String, Int>
)

internal fun populateTemplate(templateBytes: ByteArray, report: TransmittalReport): ByteArray {
    val values = linkedMapOf<String, String>(
        "B6" to report.to,
        "M6" to report.date,
        "B7" to report.from,
        "C23" to report.preparedBy,
        "C24" to report.transmitBy,
        "M24" to report.receivedByDate,
        "C26" to report.notedBy,
        "M26" to report.approvedByDate,
        "B36" to report.to,
        "M36" to report.date,
        "B37" to report.from,
        "C53" to report.preparedBy,
        "C54" to report.transmitBy,
        "M54" to report.receivedByDate,
        "C57" to report.notedBy,
        "M57" to report.approvedByDate
    )

    for (index in 0 until TransmittalReport.TEMPLATE_ITEM_COUNT) {
        val firstRow = 10 + index
        val secondRow = 40 + index
        val line = report.lineText(index)
        values["C$firstRow"] = line
        values["C$secondRow"] = line
    }

    val sharedStringTable = prepareSharedStringTable(
        templateXml = readZipEntry(templateBytes, SHARED_STRINGS_PATH).decodeToString(),
        cellValues = values.values
    )

    val output = ByteArrayOutputStream(templateBytes.size + 4096)
    ZipInputStream(ByteArrayInputStream(templateBytes)).use { input ->
        ZipOutputStream(output).use { zip ->
            while (true) {
                val entry = input.nextEntry ?: break
                val bytes = input.readBytes()
                val updated = when {
                    entry.name == SHARED_STRINGS_PATH ->
                        sharedStringTable.xml.toByteArray(Charsets.UTF_8)
                    entry.name.startsWith("xl/worksheets/sheet") && entry.name.endsWith(".xml") -> {
                        var xml = bytes.toString(Charsets.UTF_8)
                        values.forEach { (reference, value) ->
                            xml = replaceCell(
                                xml = xml,
                                reference = reference,
                                value = value,
                                sharedStringIndex = sharedStringTable.indexByValue[value]
                            )
                        }
                        xml = normalizeExportView(xml)
                        xml.toByteArray(Charsets.UTF_8)
                    }
                    entry.name == "xl/styles.xml" -> removeTemplateHighlightFill(bytes.toString(Charsets.UTF_8)).toByteArray(Charsets.UTF_8)
                    else -> bytes
                }

                val copied = ZipEntry(entry.name).apply {
                    time = entry.time
                    comment = entry.comment
                }
                zip.putNextEntry(copied)
                zip.write(updated)
                zip.closeEntry()
            }
        }
    }
    return output.toByteArray()
}

private fun readZipEntry(zipBytes: ByteArray, entryName: String): ByteArray {
    ZipInputStream(ByteArrayInputStream(zipBytes)).use { input ->
        while (true) {
            val entry = input.nextEntry ?: break
            if (entry.name == entryName) return input.readBytes()
        }
    }
    error("Workbook template is missing $entryName.")
}

private fun prepareSharedStringTable(
    templateXml: String,
    cellValues: Collection<String>
): SharedStringTable {
    val existingItemCount = Regex("<si(?:\\s[^>]*)?>").findAll(templateXml).count()
    val valuesToAppend = cellValues
        .asSequence()
        .filter(String::isNotBlank)
        .distinct()
        .toList()
    val indexByValue = valuesToAppend.mapIndexed { index, value ->
        value to existingItemCount + index
    }.toMap()

    if (valuesToAppend.isEmpty()) {
        return SharedStringTable(templateXml, indexByValue)
    }

    val existingReferenceCount = sharedStringAttribute(templateXml, "count") ?: existingItemCount
    val existingUniqueCount = sharedStringAttribute(templateXml, "uniqueCount") ?: existingItemCount
    val additionalReferences = cellValues.count(String::isNotBlank)
    var updatedXml = updateSharedStringAttribute(
        xml = templateXml,
        attribute = "count",
        value = existingReferenceCount + additionalReferences
    )
    updatedXml = updateSharedStringAttribute(
        xml = updatedXml,
        attribute = "uniqueCount",
        value = existingUniqueCount + valuesToAppend.size
    )
    val appendedItems = valuesToAppend.joinToString(separator = "") { value ->
        val preserveSpace = value.startsWith(' ') || value.endsWith(' ') || value.contains('\n')
        val spaceAttribute = if (preserveSpace) " xml:space=\"preserve\"" else ""
        "<si><t$spaceAttribute>${escapeXml(value)}</t></si>"
    }
    updatedXml = updatedXml.replace("</sst>", "$appendedItems</sst>")
    return SharedStringTable(updatedXml, indexByValue)
}

private fun sharedStringAttribute(xml: String, attribute: String): Int? =
    Regex("\\b${Regex.escape(attribute)}=\"(\\d+)\"")
        .find(xml)
        ?.groupValues
        ?.getOrNull(1)
        ?.toIntOrNull()

private fun updateSharedStringAttribute(xml: String, attribute: String, value: Int): String {
    val pattern = Regex("\\b${Regex.escape(attribute)}=\"[^\"]*\"")
    return if (pattern.containsMatchIn(xml)) {
        pattern.replaceFirst(xml, "$attribute=\"$value\"")
    } else {
        xml.replaceFirst("<sst", "<sst $attribute=\"$value\"")
    }
}

private const val DETAILS_SHEET_NAME = "Scanned Details"
private const val DETAILS_SHEET_PATH = "xl/worksheets/sheet3.xml"
private const val DETAILS_RELATIONSHIP_ID = "rId7"

private fun addDetailsWorksheetToWorkbook(xml: String): String {
    if (xml.contains("name=\"$DETAILS_SHEET_NAME\"")) return xml
    return xml.replace(
        "</sheets>",
        "<sheet name=\"$DETAILS_SHEET_NAME\" sheetId=\"4\" r:id=\"$DETAILS_RELATIONSHIP_ID\"/></sheets>"
    )
}

private fun addDetailsWorksheetRelationship(xml: String): String {
    if (xml.contains("Target=\"worksheets/sheet3.xml\"")) return xml
    return xml.replace(
        "</Relationships>",
        "<Relationship Id=\"$DETAILS_RELATIONSHIP_ID\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/></Relationships>"
    )
}

private fun addDetailsWorksheetContentType(xml: String): String {
    if (xml.contains("PartName=\"/$DETAILS_SHEET_PATH\"")) return xml
    return xml.replace(
        "</Types>",
        "<Override PartName=\"/$DETAILS_SHEET_PATH\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>"
    )
}

private fun buildDetailsSheet(report: TransmittalReport): String {
    val rows = StringBuilder()
    fun appendRow(rowNumber: Int, values: List<Pair<String, String>>, style: String? = null) {
        rows.append("<row r=\"").append(rowNumber).append("\">")
        values.forEach { (column, value) ->
            rows.append(inlineCell("$column$rowNumber", value, style))
        }
        rows.append("</row>")
    }

    appendRow(1, listOf("A" to "Yakult Transmittal - Scanned Details"), "1")
    appendRow(2, listOf(
        "A" to "Mode: ${report.itemMode.label}",
        "C" to "To: ${report.to}",
        "E" to "From: ${report.from}",
        "G" to "Date: ${report.date}"
    ))
    appendRow(4, listOf(
        "A" to "No.",
        "B" to "Description",
        "C" to "Serial",
        "D" to "Mobile number",
        "E" to "IMEI 1",
        "F" to "IMEI 2",
        "G" to "Status"
    ), "1")
    report.items.forEachIndexed { index, item ->
        appendRow(index + 5, listOf(
            "A" to (index + 1).toString(),
            "B" to item.description,
            "C" to item.serialNumber,
            "D" to item.mobileNumber,
            "E" to item.imei1,
            "F" to item.imei2,
            "G" to if (item.hasDetailedValues()) "Scanned" else "Pending"
        ))
    }

    val lastRow = maxOf(4, report.items.size + 4)
    return buildString {
        append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
        append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">")
        append("<sheetViews><sheetView view=\"normal\" workbookViewId=\"0\"/></sheetViews>")
        append("<sheetFormatPr defaultRowHeight=\"15\"/>")
        append("<cols>")
        append("<col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/>")
        append("<col min=\"2\" max=\"2\" width=\"34\" customWidth=\"1\"/>")
        append("<col min=\"3\" max=\"3\" width=\"28\" customWidth=\"1\"/>")
        append("<col min=\"4\" max=\"6\" width=\"22\" customWidth=\"1\"/>")
        append("<col min=\"7\" max=\"7\" width=\"14\" customWidth=\"1\"/>")
        append("</cols>")
        append("<sheetData>").append(rows).append("</sheetData>")
        append("<mergeCells count=1><mergeCell ref=\"A1:G1\"/></mergeCells>")
        append("<autoFilter ref=\"A4:G").append(lastRow).append("\"/>")
        append("<pageMargins left=\"0.25\" right=\"0.25\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/>")
        append("<pageSetup paperSize=\"5\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>")
        append("</worksheet>")
    }
}

private fun inlineCell(reference: String, value: String, style: String? = null): String {
    val styleAttribute = style?.let { " s=\"$it\"" }.orEmpty()
    if (value.isBlank()) return "<c r=\"$reference\"$styleAttribute/>"
    return "<c r=\"$reference\"$styleAttribute t=\"inlineStr\"><is><t>${escapeXml(value)}</t></is></c>"
}

private fun normalizeExportView(xml: String): String {
    val viewPattern = Regex("""(<sheetView\b[^>]*\bview=\")[^\"]*(\")""")
    if (viewPattern.containsMatchIn(xml)) {
        return viewPattern.replace(xml) { match ->
            "${match.groupValues[1]}normal${match.groupValues[2]}"
        }
    }

    val openingPattern = Regex("""<sheetView\b[^>]*>""")
    val openingMatch = openingPattern.find(xml) ?: return xml
    return xml.replaceRange(openingMatch.range, "<sheetView view=\"normal\">")
}

private fun removeTemplateHighlightFill(xml: String): String =
    xml.replace("fillId=\"2\"", "fillId=\"0\"")

private fun replaceCell(
    xml: String,
    reference: String,
    value: String,
    sharedStringIndex: Int?
): String {
    val pattern = Regex(
        "<c\\b(?=[^>]*\\br=\"${Regex.escape(reference)}\"[^>]*)(?:[^>]*/>|[^>]*>.*?</c>)",
        setOf(RegexOption.DOT_MATCHES_ALL)
    )
    val match = pattern.find(xml) ?: return xml
    val openingTag = match.value.substringBefore('>')
    val styleAttribute = Regex("\\bs=\"[^\"]+\"").find(openingTag)?.value
    val replacement = buildString {
        append("<c r=\"").append(reference).append('"')
        if (styleAttribute != null) append(' ').append(styleAttribute)
        if (value.isBlank()) {
            append("/>")
        } else {
            requireNotNull(sharedStringIndex) { "Missing shared string index for populated cell $reference." }
            append(" t=\"s\"><v>").append(sharedStringIndex).append("</v></c>")
        }
    }
    return xml.replaceRange(match.range, replacement)
}

private fun escapeXml(value: String): String = value
    .replace("&", "&amp;")
    .replace("<", "&lt;")
    .replace(">", "&gt;")
    .replace("\"", "&quot;")
    .replace("'", "&apos;")

private fun saveWorkbook(
    context: Context,
    fileName: String,
    bytes: ByteArray
): SavedTransmittalWorkbook {
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
        val resolver = context.contentResolver
        val values = ContentValues().apply {
            put(MediaStore.MediaColumns.DISPLAY_NAME, fileName)
            put(MediaStore.MediaColumns.MIME_TYPE, EXCEL_MIME)
            put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
            put(MediaStore.MediaColumns.IS_PENDING, 1)
        }
        val uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
            ?: throw IllegalStateException("Could not create the Excel report in Downloads.")
        try {
            resolver.openOutputStream(uri)?.use { it.write(bytes) }
                ?: throw IllegalStateException("Could not open the Excel report output stream.")
            values.clear()
            values.put(MediaStore.MediaColumns.IS_PENDING, 0)
            resolver.update(uri, values, null, null)
            return SavedTransmittalWorkbook(fileName, uri)
        } catch (error: Throwable) {
            resolver.delete(uri, null, null)
            throw error
        }
    }

    val downloads = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir
    if (!downloads.exists()) downloads.mkdirs()
    val file = File(downloads, fileName)
    FileOutputStream(file).use { it.write(bytes) }
    val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
    return SavedTransmittalWorkbook(fileName, uri)
}

private fun fileStamp(): String =
    java.text.SimpleDateFormat("yyyyMMdd_HHmmss", java.util.Locale.getDefault())
        .format(java.util.Date())
