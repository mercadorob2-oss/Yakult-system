package com.example.yakultscanner.utils

import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.Rect
import android.graphics.pdf.PdfRenderer
import android.os.ParcelFileDescriptor
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.example.yakultscanner.data.model.GatepassItemCategory
import com.example.yakultscanner.data.model.GatepassModel
import com.example.yakultscanner.data.model.GatepassReport
import com.example.yakultscanner.data.model.TransmittalItemDraft
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

@RunWith(AndroidJUnit4::class)
class GatepassPrintPdfInstrumentedTest {

    @Test
    fun freshPdfUsesLegalPageAndRendersBothGatepassCopies() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val report = GatepassReport(
            to = "IT Department",
            from = "Warehouse",
            date = "2026-08-24",
            itemCategory = GatepassItemCategory.Monitor,
            model = GatepassModel.Lx310,
            fixedAssetNumber = "FA-LEGAL-001",
            quantity = "1",
            items = listOf(
                TransmittalItemDraft(description = "Monitor", serialNumber = "UNIT-TEST-001"),
                TransmittalItemDraft(description = "Keyboard", serialNumber = "UNIT-TEST-002")
            ),
            issuedBy = "Issuer",
            notedBy = "Supervisor",
            receivedByDate = "Receiver / 2026-08-24",
            approvedByDate = "Manager / 2026-08-24"
        )

        val uri = createGatepassPrintPdf(context, report)
        val renderedFile = File(context.cacheDir, "gatepass-legal-verification.pdf")
        context.contentResolver.openInputStream(uri).use { input ->
            requireNotNull(input) { "The generated Gatepass PDF URI could not be opened." }
            renderedFile.outputStream().use { output -> input.copyTo(output) }
        }

        assertTrue(renderedFile.length() > 4_000L)
        ParcelFileDescriptor.open(renderedFile, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
            PdfRenderer(descriptor).use { renderer ->
                assertEquals(1, renderer.pageCount)
                val page = renderer.openPage(0)
                try {
                    assertEquals(GATEPASS_PAGE_WIDTH, page.width)
                    assertEquals(GATEPASS_PAGE_HEIGHT, page.height)
                    assertTrue("Legal page must be portrait", page.height > page.width)

                    val bitmap = Bitmap.createBitmap(page.width, page.height, Bitmap.Config.ARGB_8888)
                    bitmap.eraseColor(Color.WHITE)
                    page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                    assertTrue(
                        "The upper Gatepass copy must render dark content",
                        darkPixelCount(bitmap, 0, GATEPASS_COPY_HEIGHT) > 500
                    )
                    assertTrue(
                        "The reference-style top copy must not have an outer perimeter line",
                        isLightPixel(bitmap, 20, 12)
                    )
                    assertTrue(
                        "The reference-style bottom copy must not have an outer perimeter line",
                        isLightPixel(bitmap, 20, GATEPASS_COPY_HEIGHT + 12)
                    )
                } finally {
                    page.close()
                }
            }
        }
        renderedFile.delete()
    }

    @Test
    fun allThreeModeRendersTwoSheetsWithDataOnlyLowerHalfOnSecondSheet() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val report = GatepassReport(
            to = "IT Department",
            from = "Warehouse",
            date = "2026-08-24",
            itemCategory = GatepassItemCategory.Monitor,
            model = GatepassModel.Lx310,
            fixedAssetNumber = "FA-ALL-THREE-001",
            quantity = "3",
            items = listOf(
                TransmittalItemDraft(description = "Monitor", serialNumber = "ALL-THREE-001"),
                TransmittalItemDraft(description = "Keyboard", serialNumber = "ALL-THREE-002"),
                TransmittalItemDraft(description = "Mouse", serialNumber = "ALL-THREE-003")
            ),
            others = "Other data",
            remarks = "Data-only verification",
            issuedBy = "Issuer",
            notedBy = "Supervisor",
            receivedByDate = "Receiver / 2026-08-24",
            approvedByDate = "Manager / 2026-08-24",
            printAllFormTypes = true
        )

        val uri = createGatepassPrintPdf(context, report)
        val renderedFile = File(context.cacheDir, "gatepass-all-three-verification.pdf")
        context.contentResolver.openInputStream(uri).use { input ->
            requireNotNull(input) { "The generated all-three Gatepass PDF URI could not be opened." }
            renderedFile.outputStream().use { output -> input.copyTo(output) }
        }

        ParcelFileDescriptor.open(renderedFile, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
            PdfRenderer(descriptor).use { renderer ->
                assertEquals(2, renderer.pageCount)
                for (pageIndex in 0 until renderer.pageCount) {
                    val page = renderer.openPage(pageIndex)
                    try {
                        assertEquals(GATEPASS_PAGE_WIDTH, page.width)
                        assertEquals(GATEPASS_PAGE_HEIGHT, page.height)
                        val bitmap = Bitmap.createBitmap(page.width, page.height, Bitmap.Config.ARGB_8888)
                        bitmap.eraseColor(Color.WHITE)
                        page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                        assertTrue(
                            "Each all-three sheet must have dark content in its top half",
                            darkPixelCount(bitmap, 0, GATEPASS_COPY_HEIGHT) > 500
                        )
                        if (pageIndex == 0) {
                            assertTrue(
                                "The first sheet must have both Gatepass and Transmittal forms",
                                darkPixelCount(bitmap, GATEPASS_COPY_HEIGHT, GATEPASS_PAGE_HEIGHT) > 500
                            )
                        } else {
                            assertTrue(
                                "The second sheet lower half must contain the data-only copy",
                                darkPixelCount(bitmap, GATEPASS_COPY_HEIGHT, GATEPASS_PAGE_HEIGHT) > 500
                            )
                        }
                    } finally {
                        page.close()
                    }
                }
            }
        }
        renderedFile.delete()
    }

    private fun isLightPixel(bitmap: Bitmap, x: Int, y: Int): Boolean {
        val pixel = bitmap.getPixel(x, y)
        return Color.alpha(pixel) > 0 &&
            Color.red(pixel) > 245 &&
            Color.green(pixel) > 245 &&
            Color.blue(pixel) > 245
    }

    private fun darkPixelCount(bitmap: Bitmap, startY: Int, endY: Int): Int {
        var count = 0
        for (y in startY until endY step 2) {
            for (x in 0 until bitmap.width step 2) {
                val pixel = bitmap.getPixel(x, y)
                if (Color.alpha(pixel) > 0 &&
                    (Color.red(pixel) < 235 || Color.green(pixel) < 235 || Color.blue(pixel) < 235)
                ) {
                    count++
                }
            }
        }
        return count
    }
}
