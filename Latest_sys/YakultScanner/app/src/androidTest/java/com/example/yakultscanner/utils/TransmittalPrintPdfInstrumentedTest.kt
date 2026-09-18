package com.example.yakultscanner.utils

import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.Rect
import android.graphics.pdf.PdfRenderer
import android.os.ParcelFileDescriptor
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.example.yakultscanner.data.model.TransmittalItemDraft
import com.example.yakultscanner.data.model.TransmittalReport
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

@RunWith(AndroidJUnit4::class)
class TransmittalPrintPdfInstrumentedTest {

    @Test
    fun freshPdfRendersPortraitTransmittalAndFileFormsAcrossThePage() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val report = TransmittalReport(
            to = "IT Department",
            from = "Warehouse",
            date = "2026-08-19",
            items = listOf(
                TransmittalItemDraft(description = "Laptop", serialNumber = "UNIT-TEST-SERIAL-001"),
                TransmittalItemDraft(description = "Keyboard", serialNumber = "UNIT-TEST-SERIAL-002")
            ),
            preparedBy = "Tester",
            transmitBy = "Dispatcher",
            receivedByDate = "Receiver / 2026-08-19",
            notedBy = "Supervisor",
            approvedByDate = "Manager / 2026-08-19"
        )

        val uri = createTransmittalPrintPdf(context, report)
        val renderedFile = File(context.cacheDir, "transmittal-excel-form-verification.pdf")
        context.contentResolver.openInputStream(uri).use { input ->
            requireNotNull(input) { "The generated PDF URI could not be opened." }
            renderedFile.outputStream().use { output -> input.copyTo(output) }
        }

        assertTrue(renderedFile.length() > 4_000L)
        ParcelFileDescriptor.open(renderedFile, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
            PdfRenderer(descriptor).use { renderer ->
                assertEquals(1, renderer.pageCount)
                val page = renderer.openPage(0)
                try {
                    assertTrue("The Excel template is portrait", page.height > page.width)
                    val bitmap = Bitmap.createBitmap(page.width, page.height, Bitmap.Config.ARGB_8888)
                    bitmap.eraseColor(Color.WHITE)
                    page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                    val bounds = nonWhiteBounds(bitmap)

                    assertTrue("Both forms should use nearly the full printable width: $bounds", bounds.width() > page.width * 0.85f)
                    assertTrue("Both forms should use nearly the full printable height: $bounds", bounds.height() > page.height * 0.90f)
                    assertTrue("The upper TRANSMITTAL form must render", darkPixelCount(bitmap, 0, page.height / 2) > 500)
                    assertTrue("The lower FILE form must render", darkPixelCount(bitmap, page.height / 2, page.height) > 500)
                } finally {
                    page.close()
                }
            }
        }
    }

    private fun nonWhiteBounds(bitmap: Bitmap): Rect {
        val bounds = Rect(bitmap.width, bitmap.height, -1, -1)
        for (y in 0 until bitmap.height step 2) {
            for (x in 0 until bitmap.width step 2) {
                if (isDark(bitmap.getPixel(x, y))) {
                    bounds.left = minOf(bounds.left, x)
                    bounds.top = minOf(bounds.top, y)
                    bounds.right = maxOf(bounds.right, x)
                    bounds.bottom = maxOf(bounds.bottom, y)
                }
            }
        }
        return if (bounds.right >= bounds.left && bounds.bottom >= bounds.top) bounds else Rect()
    }

    private fun darkPixelCount(bitmap: Bitmap, startY: Int, endY: Int): Int {
        var count = 0
        for (y in startY until endY step 2) {
            for (x in 0 until bitmap.width step 2) {
                if (isDark(bitmap.getPixel(x, y))) count++
            }
        }
        return count
    }

    private fun isDark(pixel: Int): Boolean =
        Color.alpha(pixel) > 0 && (Color.red(pixel) < 235 || Color.green(pixel) < 235 || Color.blue(pixel) < 235)
}
