package com.example.yakultscanner.utils

import com.example.yakultscanner.data.model.TransmittalReport
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class TransmittalPrintUtilsTest {

    @Test
    fun portraitPageContainsTwoEqualWidthNonOverlappingFormCopies() {
        val forms = transmittalPdfFormBounds()

        assertEquals(2, forms.size)
        assertEquals(30f, forms[0].left, 0.001f)
        assertEquals(582f, forms[0].right, 0.001f)
        assertEquals(24f, forms[0].top, 0.001f)
        assertEquals(768f, forms[1].bottom, 0.001f)
        assertEquals(forms[0].right - forms[0].left, forms[1].right - forms[1].left, 0.001f)
        assertEquals(forms[0].bottom - forms[0].top, forms[1].bottom - forms[1].top, 0.001f)
        assertTrue("The Transmittal and File copies must not overlap", forms[0].bottom < forms[1].top)
        assertEquals(16f, forms[1].top - forms[0].bottom, 0.001f)
    }

    @Test
    fun paginationKeepsTwelveTemplateLinesPerCopyOnEachPage() {
        assertEquals(1, transmittalPdfPageCount(0))
        assertEquals(1, transmittalPdfPageCount(1))
        assertEquals(1, transmittalPdfPageCount(TransmittalReport.TEMPLATE_ITEM_COUNT))
        assertEquals(2, transmittalPdfPageCount(TransmittalReport.TEMPLATE_ITEM_COUNT + 1))
        assertEquals(2, transmittalPdfPageCount(TransmittalReport.TEMPLATE_ITEM_COUNT * 2))
        assertEquals(3, transmittalPdfPageCount(TransmittalReport.TEMPLATE_ITEM_COUNT * 2 + 1))
    }
}
