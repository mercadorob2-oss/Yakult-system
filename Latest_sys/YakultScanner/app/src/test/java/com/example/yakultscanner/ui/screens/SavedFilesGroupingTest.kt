package com.example.yakultscanner.ui.screens

import org.junit.Assert.assertEquals
import org.junit.Test

class SavedFilesGroupingTest {

    @Test
    fun knownSystemPrefixesUseTheirSystemSections() {
        val expectedSections = mapOf(
            "Gatepass_20260822_082422_local.pdf" to "Gatepass / File Transmittal",
            "Transmittal_20260822_082422.xlsx" to "Transmittal",
            "Dispatch_SET-100_123.pdf" to "Dispatch / Receipt Sets",
            "Report_Monthly_123.pdf" to "Reports",
            "BorrowLog_History_123.csv" to "Borrow Records",
            "CallMonitoring_Open_123.pdf" to "Call Monitoring / ITCM",
            "Ticket_ITCM-100_123.pdf" to "Call Monitoring / ITCM",
            "LocalSerialScan_20260822_082422.csv" to "Serial Scanning",
            "Pending_20260822.json" to "Updates / Sync",
            "Processed_20260822.json" to "Updates / Sync"
        )

        expectedSections.forEach { (fileName, expectedSection) ->
            assertEquals(expectedSection, savedFileSystemSection(fileName))
        }
    }

    @Test
    fun importedAndUnknownFilesHaveFallbackSections() {
        assertEquals("Imported Files", savedFileSystemSection("Imported_20260822.xlsx"))
        assertEquals("Other Saved Files", savedFileSystemSection("notes.pdf"))
    }
}
