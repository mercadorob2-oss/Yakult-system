package com.example.yakultscanner.utils

import com.example.yakultscanner.data.model.TransmittalItemDraft
import com.example.yakultscanner.data.model.TransmittalReport
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.File
import java.util.zip.ZipInputStream

class TransmittalExcelExporterTest {

    @Test
    fun populateTemplate_preservesTheOriginalTwoSheetPackage() {
        val templateBytes = templateFile().readBytes()
        val generatedEntries = zipEntries(
            populateTemplate(
                templateBytes = templateBytes,
                report = TransmittalReport(
                    to = "IT Department",
                    from = "Warehouse",
                    date = "2026-08-17",
                    items = listOf(
                        TransmittalItemDraft(
                            description = "Laptop",
                            serialNumber = "UNIT-TEST-SERIAL-001"
                        )
                    ),
                    preparedBy = "Tester",
                    transmitBy = "Tester",
                    receivedByDate = "",
                    notedBy = "",
                    approvedByDate = ""
                )
            )
        )
        val templateEntries = zipEntries(templateBytes)

        assertEquals(templateEntries.keys, generatedEntries.keys)
        assertFalse(generatedEntries.containsKey("xl/worksheets/sheet3.xml"))

        val workbookXml = generatedEntries.getValue("xl/workbook.xml").decodeToString()
        assertFalse(workbookXml.contains("Scanned Details"))
        assertEquals(2, "<sheet ".toRegex().findAll(workbookXml).count())

        val sheet1Xml = generatedEntries.getValue("xl/worksheets/sheet1.xml").decodeToString()
        val sheet2Xml = generatedEntries.getValue("xl/worksheets/sheet2.xml").decodeToString()
        val sharedStringItems = sharedStringItems(generatedEntries.getValue("xl/sharedStrings.xml").decodeToString())

        assertTrue(sheet1Xml.contains("t=\"s\""))
        assertTrue(sheet2Xml.contains("t=\"s\""))
        assertFalse(sheet1Xml.contains("inlineStr"))
        assertFalse(sheet2Xml.contains("inlineStr"))

        val toIndex = sharedStringIndex(sheet1Xml, "B6")
        val serialIndex = sharedStringIndex(sheet1Xml, "C10")
        assertEquals("IT Department", sharedStringItems[toIndex])
        assertTrue(sharedStringItems[serialIndex].contains("UNIT-TEST-SERIAL-001"))
        assertEquals(toIndex, sharedStringIndex(sheet2Xml, "B36"))
        assertEquals(serialIndex, sharedStringIndex(sheet2Xml, "C40"))
    }

    private fun sharedStringIndex(sheetXml: String, reference: String): Int {
        val cell = Regex(
            "<c\\b(?=[^>]*\\br=\"${Regex.escape(reference)}\"[^>]*)(?:[^>]*/>|[^>]*>.*?</c>)",
            setOf(RegexOption.DOT_MATCHES_ALL)
        ).find(sheetXml)?.value ?: error("Missing cell $reference")
        assertTrue("Cell $reference must use a shared-string reference.", cell.contains("t=\"s\""))
        return Regex("<v>(\\d+)</v>").find(cell)?.groupValues?.get(1)?.toInt()
            ?: error("Missing shared-string index in $reference")
    }

    private fun sharedStringItems(sharedStringsXml: String): List<String> =
        Regex("<si(?:\\s[^>]*)?>(.*?)</si>", setOf(RegexOption.DOT_MATCHES_ALL))
            .findAll(sharedStringsXml)
            .map { match ->
                match.groupValues[1]
                    .replace(Regex("<[^>]+>"), "")
                    .replace("&amp;", "&")
                    .replace("&lt;", "<")
                    .replace("&gt;", ">")
                    .replace("&quot;", "\"")
                    .replace("&apos;", "'")
            }
            .toList()

    private fun templateFile(): File = listOf(
        File("src/main/assets/transmittal_template.xlsx"),
        File("app/src/main/assets/transmittal_template.xlsx")
    ).firstOrNull(File::isFile)
        ?: error("Could not find transmittal_template.xlsx for the export compatibility test.")

    private fun zipEntries(bytes: ByteArray): Map<String, ByteArray> = buildMap {
        ZipInputStream(ByteArrayInputStream(bytes)).use { input ->
            while (true) {
                val entry = input.nextEntry ?: break
                put(entry.name, input.readBytes())
                input.closeEntry()
            }
        }
    }
}
