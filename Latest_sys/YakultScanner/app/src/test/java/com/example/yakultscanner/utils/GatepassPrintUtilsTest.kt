package com.example.yakultscanner.utils

import com.example.yakultscanner.data.model.GatepassFormType
import com.example.yakultscanner.data.model.GatepassItemCategory
import com.example.yakultscanner.data.model.GatepassModel
import com.example.yakultscanner.data.model.GatepassReport
import com.example.yakultscanner.data.model.TransmittalItemDraft
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class GatepassPrintUtilsTest {

    @Test
    fun pageCountUsesTwelveFormLinesPerPage() {
        assertTrue(gatepassPdfPageCount(0) == 1)
        assertTrue(gatepassPdfPageCount(1) == 1)
        assertTrue(gatepassPdfPageCount(GatepassReport.TEMPLATE_ITEM_COUNT) == 1)
        assertTrue(gatepassPdfPageCount(GatepassReport.TEMPLATE_ITEM_COUNT + 1) == 2)
    }

    @Test
    fun legalPageContainsTwoHalfHeightCopies() {
        assertEquals(612, GATEPASS_PAGE_WIDTH)
        assertEquals(1008, GATEPASS_PAGE_HEIGHT)
        assertEquals(2, GATEPASS_COPIES_PER_PAGE)
        assertEquals(504, GATEPASS_COPY_HEIGHT)
        assertEquals(listOf(0f, 504f), gatepassPdfCopyTopOffsets())
    }

    @Test
    fun allThreeModeUsesTwoSheetsAndTheRequestedFormOrder() {
        assertEquals(2, gatepassPdfPageCount(0, printAllFormTypes = true))
        assertEquals(2, gatepassPdfPageCount(1, printAllFormTypes = true))
        assertEquals(4, gatepassPdfPageCount(GatepassReport.TEMPLATE_ITEM_COUNT + 1, printAllFormTypes = true))
        assertEquals(
            listOf(GatepassFormType.Gatepass, GatepassFormType.Transmittal),
            gatepassPdfAllThreeFormTypesForPage(0)
        )
        assertEquals(
            listOf(GatepassFormType.File, null),
            gatepassPdfAllThreeFormTypesForPage(1)
        )
    }

    @Test
    fun dataSummaryNumbersListedItemsAndSkipsBlankLines() {
        assertEquals(
            listOf(
                "1. Monitor — Serial: MON-001",
                "3. Keyboard — Serial: KEY-003"
            ),
            gatepassPdfDataSummaryItemLines(
                listOf(
                    "Monitor — Serial: MON-001",
                    "",
                    "Keyboard — Serial: KEY-003"
                )
            )
        )
    }

    @Test
    fun selectedPaperFieldsAppearInFirstFormLineWhenNoSerialIsScanned() {
        val report = GatepassReport(
            formType = GatepassFormType.File,
            to = "Information Technology Department",
            from = "Warehouse",
            date = "2026-08-20",
            itemCategory = GatepassItemCategory.CartridgeRibbon,
            model = GatepassModel.Lx310,
            fixedAssetNumber = "FA-001",
            quantity = "1"
        )

        val line = report.lineText(0)

        assertTrue(line.contains("Cartridge Ribbon"))
        assertTrue(line.contains("LX310"))
        assertTrue(line.contains("Asset: FA-001"))
        assertTrue(line.contains("Qty: 1"))
    }

    @Test
    fun scannedItemMakesGatepassReportExportable() {
        val report = GatepassReport(
            to = "IT",
            from = "Warehouse",
            date = "2026-08-20",
            items = listOf(TransmittalItemDraft(description = "Printer", serialNumber = "PRN-001"))
        )

        assertTrue(report.hasContent())
        assertTrue(report.lineText(0).contains("PRN-001"))
    }

    @Test
    fun emptyGatepassReportIsNotExportable() {
        val report = GatepassReport(to = "", from = "", date = "")

        assertFalse(report.hasContent())
    }
}
