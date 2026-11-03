package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallItemLookupDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SerialItemMatchTest {

    private fun item(
        id: Int,
        serial: String?,
        fixedAsset: String? = "FA-$id",
        category: String = "Desktop"
    ) = CallItemLookupDto(
        itemId = id,
        displayText = "Item $id",
        category = category,
        stockOnHand = 1,
        serialNumber = serial,
        fixedAssetNumber = fixedAsset
    )

    @Test
    fun partialSerialSearchMatchesSubstringIgnoringCase() {
        val items = listOf(item(1, "ABC-123"), item(2, "XYZ-999"))

        assertEquals(listOf(1), serialMatches(items, "abc").map { it.itemId })
        assertEquals(listOf(1), serialMatches(items, "123").map { it.itemId })
    }

    @Test
    fun partialSerialSearchIgnoresBlankSerials() {
        val items = listOf(item(1, null), item(2, ""), item(3, "   "), item(4, "ABC-123"))

        assertEquals(listOf(4), serialMatches(items, "a").map { it.itemId })
    }

    @Test
    fun partialSerialSearchReturnsEmptyForBlankQuery() {
        val items = listOf(item(1, "ABC-123"), item(2, "XYZ-999"))

        assertTrue(serialMatches(items, "").isEmpty())
        assertTrue(serialMatches(items, "   ").isEmpty())
    }

    @Test
    fun partialSerialSearchOrdersBySerial() {
        val items = listOf(item(1, "ZZZ-1"), item(2, "AAA-2"), item(3, "MMM-3"))

        assertEquals(listOf(2, 3, 1), serialMatches(items, "-").map { it.itemId })
    }

    @Test
    fun exactSerialMatchIgnoresWhitespaceAndCase() {
        val items = listOf(item(1, "ABC-123"), item(2, "XYZ-999"))

        assertEquals(listOf(1), exactSerialMatches(items, " abc-123 ").map { it.itemId })
    }

    @Test
    fun exactSerialMatchIgnoresInternalWhitespace() {
        val items = listOf(item(1, "ABC 123"))

        assertEquals(listOf(1), exactSerialMatches(items, "abc123").map { it.itemId })
    }

    @Test
    fun partialSerialSearchIgnoresWhitespaceInsideStoredSerial() {
        val items = listOf(item(1, "ABC 123"))

        assertEquals(listOf(1), serialMatches(items, "abc12").map { it.itemId })
    }

    @Test
    fun exactSerialMatchRejectsPartialSerial() {
        val items = listOf(item(1, "ABC-123"))

        assertTrue(exactSerialMatches(items, "ABC").isEmpty())
    }

    @Test
    fun exactSerialMatchReturnsAllDuplicates() {
        val items = listOf(item(1, "DUP-1"), item(2, "DUP-1"), item(3, "OTHER"))

        assertEquals(listOf(1, 2), exactSerialMatches(items, "dup-1").map { it.itemId })
    }

    @Test
    fun fetchAssetResolvesUniqueSerial() {
        val items = listOf(item(1, "ABC-123", "FA-ABC"), item(2, "XYZ-999", "FA-XYZ"))

        assertEquals(FetchAssetResult.Found(items[0]), resolveFetchAsset(items, " abc-123 "))
    }

    @Test
    fun fetchAssetReportsAmbiguousSerial() {
        val items = listOf(item(1, "DUP-1"), item(2, "DUP-1"))

        val result = resolveFetchAsset(items, "DUP-1")

        assertTrue(result is FetchAssetResult.Ambiguous)
        assertEquals(listOf(1, 2), (result as FetchAssetResult.Ambiguous).items.map { it.itemId })
    }

    @Test
    fun fetchAssetReportsMissingFixedAsset() {
        val items = listOf(item(1, "ABC-123", fixedAsset = " "))

        assertEquals(FetchAssetResult.MissingFixedAsset(items[0]), resolveFetchAsset(items, "ABC-123"))
    }

    @Test
    fun fetchAssetReportsNoMatch() {
        val items = listOf(item(1, "ABC-123"))

        assertEquals(FetchAssetResult.NotFound, resolveFetchAsset(items, "NOPE"))
        assertEquals(FetchAssetResult.NotFound, resolveFetchAsset(items, " "))
    }

    @Test
    fun fetchAssetDeduplicatesSameItemId() {
        val items = listOf(item(1, "ABC-123", "FA-ABC"), item(1, "ABC-123", "FA-ABC"))

        assertEquals(FetchAssetResult.Found(items[0]), resolveFetchAsset(items, "ABC-123"))
    }
}
