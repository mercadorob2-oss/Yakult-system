package com.example.yakultscanner.utils

import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.ResolutionLineRequest

fun validateResolutionEntries(lines: List<ResolutionLineRequest>, stock: List<CallItemLookupDto>, pullOut: Boolean = false): String? {
    if (lines.isEmpty() || lines.size > 100) return "Add between 1 and 100 item entries."
    val oldIds = mutableSetOf<Int>()
    val serials = mutableSetOf<String>()
    for ((index, line) in lines.withIndex()) {
        val prefix = "Entry ${index + 1}: "
        val hasOld = line.useUnlistedOldItem == true || (line.oldItemId ?: 0) > 0
        val hasNew = (line.newItemId ?: 0) > 0
        if (pullOut && (!hasOld || hasNew)) return prefix + "Pull Out requires an old item and cannot issue a new item."
        if (!hasOld && !hasNew) return prefix + "Select an old item, a new item, or both."
        if ((line.quantity ?: 0) <= 0) return prefix + "Enter a positive quantity."
        if (line.remarks.isNullOrBlank()) return prefix + "Add notes before reviewing."
        if (hasOld && (line.oldItemConditionId ?: 0) <= 0) return prefix + "Select the old item condition."
        if (hasOld && line.oldItemRepairAction !in listOf("Repaired", "Unrepaired", "Repaired - Spare inventory")) return prefix + "Select a valid old item action."
        if (line.useUnlistedOldItem == true) {
            if (line.unlistedOldItemName.isNullOrBlank() || line.unlistedOldItemModelNumber.isNullOrBlank() || line.unlistedOldItemUnitOfMeasure.isNullOrBlank() || (line.unlistedOldItemCategoryId ?: 0) <= 0)
                return prefix + "Unlisted items need a name, model, unit and category."
            val serial = line.unlistedOldItemSerialNumber.orEmpty().trim().lowercase(java.util.Locale.ROOT)
            if (serial.isNotEmpty() && !serials.add(serial)) return "Unlisted old items cannot share a serial number."
        } else if (hasOld && !oldIds.add(line.oldItemId!!)) return "Each old item can only be included once."
    }
    if (lines.any { (it.newItemId ?: 0) > 0 && it.newItemId in oldIds }) return "An old item cannot also be issued as new."
    for ((id, entries) in lines.filter { (it.newItemId ?: 0) > 0 }.groupBy { it.newItemId }) {
        val item = stock.find { it.itemId == id } ?: return "Selected replacement is no longer available."
        if (entries.sumOf { (it.quantity ?: 0).toLong() } > (item.stockOnHand ?: 0)) return "Combined quantities exceed stock for ${item.displayText}."
    }
    return null
}

fun resolutionIsTemporary(type: String, temporary: Boolean, lines: List<ResolutionLineRequest>): Boolean =
    type == "Pull Out" || (temporary && (type == "Service Only" || lines.any { (it.newItemId ?: 0) > 0 }))
