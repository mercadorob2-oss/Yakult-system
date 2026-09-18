package com.example.yakultscanner.utils

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class AppFileStoreTest {

    @Test
    fun xlsxUsesTheOpenXmlSpreadsheetMimeType() {
        val file = SavedAppFile(File("Transmittal.xlsx"))

        assertEquals(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.mimeType
        )
    }

    @Test
    fun xlsxIncludesTheLegacySpreadsheetMimeAsAControlledCompatibilityCandidate() {
        val file = SavedAppFile(File("Transmittal.xlsx"))

        assertEquals(
            listOf("application/vnd.ms-excel"),
            AppFileStore.compatibleOpenMimeTypes(file)
        )
    }

    @Test
    fun nonSpreadsheetFilesDoNotReceiveSpreadsheetCompatibilityCandidates() {
        val file = SavedAppFile(File("report.pdf"))

        assertTrue(AppFileStore.compatibleOpenMimeTypes(file).isEmpty())
    }
}
