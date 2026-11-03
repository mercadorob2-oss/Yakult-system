package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.ResolutionLineRequest
import com.example.yakultscanner.api.ResolutionRequest
import com.google.gson.Gson
import org.junit.Assert.*
import org.junit.Test

class ResolutionEntriesTest {
    private val stock = listOf(CallItemLookupDto(10, "Laptop", "Computer", 3), CallItemLookupDto(11, "Monitor", "Monitor", 2))
    private fun old(id: Int = 1) = ResolutionLineRequest(oldItemId = id, quantity = 1, oldItemConditionId = 2, oldItemRepairAction = "Unrepaired", remarks = "Collected faulty unit")
    private fun new(id: Int = 10, qty: Int = 1) = ResolutionLineRequest(newItemId = id, quantity = qty, remarks = "Issued replacement")

    @Test fun acceptsOldOnlyWithNoReplacementStock() {
        assertNull(validateResolutionEntries(listOf(old()), emptyList()))
    }
    @Test fun acceptsNewOnlyWithoutOldCondition() {
        assertNull(validateResolutionEntries(listOf(new()), stock))
    }
    @Test fun acceptsUnequalOldAndNewCountsAndQuantities() {
        assertNull(validateResolutionEntries(listOf(old(), old(2), new(qty = 3)), stock))
    }
    @Test fun rejectsCombinedStockOverAllocation() {
        assertNotNull(validateResolutionEntries(listOf(new(qty = 2), new(qty = 2)), stock))
    }
    @Test fun rejectsDuplicateOldItemAndCrossEntryReuse() {
        assertNotNull(validateResolutionEntries(listOf(old(), old()), stock))
        assertNotNull(validateResolutionEntries(listOf(old(10), new()), stock))
    }
    @Test fun rejectsRepeatedUnlistedSerialIgnoringCaseAndSpaces() {
        val entry = old().copy(oldItemId = null, useUnlistedOldItem = true, unlistedOldItemName = "PC", unlistedOldItemModelNumber = "M1", unlistedOldItemUnitOfMeasure = "Unit", unlistedOldItemCategoryId = 2, unlistedOldItemSerialNumber = " abc ")
        assertNotNull(validateResolutionEntries(listOf(entry, entry.copy(unlistedOldItemSerialNumber = "ABC")), stock))
    }
    @Test fun rejectsEmptyInvalidQuantityAndMissingNotes() {
        assertNotNull(validateResolutionEntries(emptyList(), stock))
        assertNotNull(validateResolutionEntries(listOf(new(qty = 0)), stock))
        assertNotNull(validateResolutionEntries(listOf(new().copy(remarks = " ")), stock))
        assertNotNull(validateResolutionEntries(listOf(new().copy(quantity = null)), stock))
    }
    @Test fun rejectsUnavailableItem() {
        assertNotNull(validateResolutionEntries(listOf(new(99)), stock))
    }
    @Test fun serializesIndependentBatchSidesWithoutInventingItems() {
        val json = Gson().toJson(ResolutionRequest(ticketId = 42, resolutionType = "Replacement", lines = listOf(old(), new())))
        val decoded = Gson().fromJson(json, ResolutionRequest::class.java)
        assertEquals(2, decoded.lines!!.size)
        assertNull(decoded.lines!![0].newItemId)
        assertNull(decoded.lines!![1].oldItemId)
        assertEquals("Collected faulty unit", decoded.lines!![0].remarks)
    }
}
