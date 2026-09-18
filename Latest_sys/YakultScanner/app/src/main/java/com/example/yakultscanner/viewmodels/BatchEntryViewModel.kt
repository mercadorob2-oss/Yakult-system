package com.example.yakultscanner.viewmodels

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.BatchItemRequest
import com.example.yakultscanner.api.BatchItemsRequest
import com.example.yakultscanner.api.ConditionDto
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import com.example.yakultscanner.api.VendorDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.data.repository.InventoryRepository
import com.example.yakultscanner.utils.normalizeSerial
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.*
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

data class PhoneItemEntry(
    val cellPhoneNumber: String = "",
    val serialNumber: String = "",
    val imei1: String = "",
    val imei2: String = ""
)

/**
 * ViewModel for BatchSerialEntryScreen.
 * Handles all state and business logic for batch item entry.
 */
@HiltViewModel
class BatchEntryViewModel @Inject constructor(
    private val repository: InventoryRepository
) : ViewModel() {

    // Item Details
    var itemName by mutableStateOf("")
        private set
    var description by mutableStateOf("")
        private set
    var selectedType by mutableStateOf("Hardware")
        private set
    var modelNumber by mutableStateOf("")
        private set
    var currentSerial by mutableStateOf("")
        private set
    var serialNumbers by mutableStateOf(listOf<String>())
        private set
    var phoneItems by mutableStateOf(listOf(PhoneItemEntry()))
        private set

    // Dropdowns state
    var categories by mutableStateOf<List<ItemCategoryDto>>(emptyList())
        private set
    var selectedCategory by mutableStateOf<ItemCategoryDto?>(null)
        private set
    var unitOptions by mutableStateOf(listOf("Unit", "Piece", "Box", "Pack", "Set"))
        private set
    var selectedUnit by mutableStateOf(unitOptions.first())
        private set
    var amount by mutableStateOf("")
        private set
    var startDate by mutableStateOf("")
        private set
    var endDate by mutableStateOf("")
        private set

    // Conditions & Vendors
    var conditions by mutableStateOf<List<ConditionDto>>(emptyList())
        private set
    var selectedCondition by mutableStateOf<ConditionDto?>(null)
        private set
    var vendors by mutableStateOf<List<VendorDto>>(emptyList())
        private set
    var selectedVendor by mutableStateOf<VendorDto?>(null)
        private set

    // Purchase Details
    var warrantyYears by mutableStateOf("")
        private set
    var datePurchased by mutableStateOf("")
        private set
    var licenseNumber by mutableStateOf("")
        private set
    var remarks by mutableStateOf("")
        private set

    // UI State
    var typeExpanded by mutableStateOf(false)
    var categoryExpanded by mutableStateOf(false)
    var unitExpanded by mutableStateOf(false)
    var conditionExpanded by mutableStateOf(false)
    var vendorExpanded by mutableStateOf(false)
    
    var isSaving by mutableStateOf(false)
        private set
    var errorMessage by mutableStateOf<String?>(null)
        private set
    var successMessage by mutableStateOf<String?>(null)
        private set

    val typeOptions = listOf("Hardware", "Software/License", "Services")

    init {
        loadInitialData()
    }

    fun loadInitialData() {
        viewModelScope.launch {
            loadCategories()
            loadConditions()
            loadVendors()
        }
    }

    private suspend fun loadCategories() {
        when (val result = safeApiCall { repository.getItemCategories() }) {
            is ApiResult.Success -> {
                categories = result.data
            }
            is ApiResult.HttpError -> {
                errorMessage = result.userMessageOr("We couldn't load the categories right now.")
            }
            is ApiResult.NetworkError -> {
                errorMessage = result.userMessageOr(SCANNER_NETWORK_MESSAGE)
            }
            is ApiResult.UnknownError -> {
                errorMessage = result.userMessageOr(SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    private suspend fun loadConditions() {
        when (val result = safeApiCall { repository.getConditions() }) {
            is ApiResult.Success -> {
                conditions = result.data
                // Default to "Good" if available
                selectedCondition = conditions.find {
                    it.conditionName.equals("Good", ignoreCase = true)
                } ?: conditions.firstOrNull()
            }
            is ApiResult.HttpError -> {
                errorMessage = result.userMessageOr("We couldn't load the conditions right now.")
            }
            is ApiResult.NetworkError -> {
                errorMessage = result.userMessageOr(SCANNER_NETWORK_MESSAGE)
            }
            is ApiResult.UnknownError -> {
                errorMessage = result.userMessageOr(SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    private suspend fun loadVendors() {
        when (val result = safeApiCall { repository.getVendors() }) {
            is ApiResult.Success -> {
                vendors = result.data
            }
            is ApiResult.HttpError -> {
                errorMessage = result.userMessageOr("We couldn't load the vendors right now.")
            }
            is ApiResult.NetworkError -> {
                errorMessage = result.userMessageOr(SCANNER_NETWORK_MESSAGE)
            }
            is ApiResult.UnknownError -> {
                errorMessage = result.userMessageOr(SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    // Update functions
    fun updateItemName(value: String) {
        itemName = value
    }

    fun updateDescription(value: String) {
        description = value
    }

    fun updateSelectedType(value: String) {
        selectedType = value
        // Update unit options based on type
        when (value) {
            "Hardware" -> {
                unitOptions = listOf("Unit", "Piece", "Box", "Pack", "Set")
                selectedUnit = unitOptions.first()
                startDate = ""
                endDate = ""
            }
            "Software/License" -> {
                unitOptions = listOf("Monthly", "Annually", "One Time")
                selectedUnit = unitOptions.first()
            }
            "Services" -> {
                unitOptions = listOf("Contract")
                selectedUnit = unitOptions.first()
                startDate = ""
                endDate = ""
            }
        }
    }

    fun updateModelNumber(value: String) {
        modelNumber = value
    }

    fun updateCurrentSerial(value: String) {
        currentSerial = value
    }

    fun addSerial() {
        val normalized = normalizeSerial(currentSerial)
        if (normalized.isNotEmpty()
            && !serialNumbers.any { normalizeSerial(it) == normalized }
        ) {
            serialNumbers = serialNumbers + normalized
            currentSerial = ""
        }
    }

    fun removeSerial(serial: String) {
        serialNumbers = serialNumbers.filter { it != serial }
    }

    fun isCellphoneCategory(): Boolean {
        return selectedCategory?.name?.replace(" ", "")?.equals("Cellphone", ignoreCase = true) == true
    }

    fun addPhoneRow() {
        phoneItems = phoneItems + PhoneItemEntry()
    }

    fun generatePhoneRows(countText: String) {
        val count = countText.toIntOrNull()?.coerceIn(1, 200) ?: return
        phoneItems = List(count) { index -> phoneItems.getOrNull(index) ?: PhoneItemEntry() }
    }

    fun removePhoneRow(index: Int) {
        phoneItems = phoneItems.filterIndexed { rowIndex, _ -> rowIndex != index }.ifEmpty { listOf(PhoneItemEntry()) }
    }

    fun updatePhoneRow(index: Int, value: PhoneItemEntry) {
        val current = phoneItems.getOrNull(index) ?: PhoneItemEntry()
        val used = phoneCodeSet(excludeIndex = index).toMutableSet()

        fun keepIfUnique(raw: String, previous: String): String {
            val normalized = normalizeSerial(raw)
            if (normalized.isBlank()) return raw
            return if (used.add(normalized)) raw else previous
        }

        val cleaned = value.copy(
            serialNumber = keepIfUnique(value.serialNumber, current.serialNumber),
            imei1 = keepIfUnique(value.imei1, current.imei1),
            imei2 = keepIfUnique(value.imei2, current.imei2)
        )
        phoneItems = phoneItems.mapIndexed { rowIndex, row -> if (rowIndex == index) cleaned else row }
    }

    fun addPhoneSerialFromScan(rawSerial: String): Boolean {
        val normalized = normalizeSerial(rawSerial)
        if (normalized.isBlank() || phoneCodeSet().contains(normalized)) return false

        val emptyIndex = phoneItems.indexOfFirst { normalizeSerial(it.serialNumber).isBlank() }
        phoneItems = if (emptyIndex >= 0) {
            phoneItems.mapIndexed { index, row -> if (index == emptyIndex) row.copy(serialNumber = normalized) else row }
        } else {
            phoneItems + PhoneItemEntry(serialNumber = normalized)
        }
        currentSerial = ""
        return true
    }

    private fun phoneCodeSet(excludeIndex: Int? = null): Set<String> {
        return phoneItems
            .asSequence()
            .filterIndexed { index, _ -> excludeIndex == null || index != excludeIndex }
            .flatMap { row -> sequenceOf(row.serialNumber, row.imei1, row.imei2) }
            .map { normalizeSerial(it) }
            .filter { it.isNotBlank() }
            .toSet()
    }

    private fun validPhoneRows(): List<PhoneItemEntry> {
        return phoneItems
            .map {
                it.copy(
                    cellPhoneNumber = it.cellPhoneNumber.trim(),
                    serialNumber = normalizeSerial(it.serialNumber),
                    imei1 = it.imei1.trim(),
                    imei2 = it.imei2.trim()
                )
            }
            .filter { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() }
    }

    private fun duplicatePhoneEntryMessage(rows: List<PhoneItemEntry>): String? {
        data class SeenCode(val label: String, val rowNumber: Int)

        val seen = mutableMapOf<String, SeenCode>()
        rows.forEachIndexed { index, row ->
            val rowNumber = index + 1
            val values = listOf(
                "Serial Number" to normalizeSerial(row.serialNumber),
                "IMEI 1" to normalizeSerial(row.imei1),
                "IMEI 2" to normalizeSerial(row.imei2)
            )

            for ((label, value) in values) {
                if (value.isBlank()) continue
                val existing = seen[value]
                if (existing != null) {
                    return "Duplicate $label: Phone ${existing.rowNumber} ${existing.label} and Phone $rowNumber $label have the same value."
                }
                seen[value] = SeenCode(label, rowNumber)
            }
        }

        return null
    }

    fun updateSelectedCategory(category: ItemCategoryDto?) {
        selectedCategory = category
    }

    fun updateSelectedCondition(condition: ConditionDto?) {
        selectedCondition = condition
    }

    fun updateSelectedUnit(unit: String) {
        selectedUnit = unit
    }

    fun updateAmount(value: String) {
        amount = value
    }

    fun updateStartDate(value: String) {
        startDate = value
    }

    fun updateEndDate(value: String) {
        endDate = value
    }

    fun updateSelectedVendor(vendor: VendorDto?) {
        selectedVendor = vendor
    }

    fun updateWarrantyYears(value: String) {
        // Only allow numeric input
        if (value.isEmpty() || value.all { it.isDigit() }) {
            warrantyYears = value
        }
    }

    fun updateDatePurchased(value: String) {
        datePurchased = value
    }

    fun updateLicenseNumber(value: String) {
        licenseNumber = value
    }

    fun updateRemarks(value: String) {
        remarks = value
    }

    private fun formatDate(millis: Long): String {
        val sdf = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault())
        return sdf.format(Date(millis))
    }

    fun onStartDateSelected(millis: Long) {
        startDate = formatDate(millis)
    }

    fun onEndDateSelected(millis: Long) {
        endDate = formatDate(millis)
    }

    fun onDatePurchasedSelected(millis: Long) {
        datePurchased = formatDate(millis)
    }

    fun isFormValid(): Boolean {
        return itemName.isNotBlank() &&
                modelNumber.isNotBlank() &&
                selectedCategory != null &&
                selectedCondition != null &&
                if (isCellphoneCategory()) validPhoneRows().isNotEmpty() && validPhoneRows().all { it.serialNumber.isNotBlank() } else serialNumbers.isNotEmpty()
    }

    fun clearError() {
        errorMessage = null
    }

    fun clearSuccess() {
        successMessage = null
    }

    /**
     * Validates and saves batch items
     * Returns error message if validation fails, null on success
     */
    fun saveBatchItems(onSuccess: () -> Unit, onError: (String) -> Unit) {
        // Validation
        if (itemName.isBlank()) {
            onError("Enter item name")
            return
        }
        if (modelNumber.isBlank()) {
            onError("Enter model number")
            return
        }
        if (selectedCategory == null) {
            onError("Select a category")
            return
        }
        if (selectedCondition == null) {
            onError("Select a condition")
            return
        }
        val phoneRows = validPhoneRows()
        if (isCellphoneCategory()) {
            if (phoneRows.isEmpty()) {
                onError("Add at least one phone row")
                return
            }
            if (phoneRows.any { it.serialNumber.isBlank() }) {
                onError("Serial number is required for every phone row")
                return
            }
            duplicatePhoneEntryMessage(phoneRows)?.let { message ->
                onError(message)
                return
            }
        } else if (serialNumbers.isEmpty()) {
            onError("Add at least one serial")
            return
        }

        isSaving = true
        viewModelScope.launch {
            val currentUser = UserSession.currentUser
            val createdBy = currentUser?.displayName ?: "Unknown"

            val batchItems = if (isCellphoneCategory()) {
                phoneRows.map { row ->
                    BatchItemRequest(
                        serialNumber = row.serialNumber,
                        modelNumber = modelNumber,
                        itemType = selectedType,
                        cellPhoneNumber = row.cellPhoneNumber.ifBlank { null },
                        imei1 = row.imei1.ifBlank { null },
                        imei2 = row.imei2.ifBlank { null }
                    )
                }
            } else {
                serialNumbers.map { serial ->
                    BatchItemRequest(
                        serialNumber = serial,
                        modelNumber = modelNumber,
                        itemType = selectedType
                    )
                }
            }

            val request = BatchItemsRequest(
                items = batchItems,
                createdBy = createdBy,
                itemName = itemName,
                description = description.ifBlank { null },
                categoryId = selectedCategory?.categoryId,
                categoryName = selectedCategory?.name,
                unitOfMeasure = selectedUnit,
                amount = amount.toDoubleOrNull(),
                startDate = if (selectedType == "Software/License") startDate.ifBlank { null } else null,
                endDate = if (selectedType == "Software/License") endDate.ifBlank { null } else null,
                conditionId = selectedCondition?.conditionId,
                vendorId = selectedVendor?.vendorId,
                warrantyYears = warrantyYears.toIntOrNull(),
                datePurchased = datePurchased.ifBlank { null },
                licenseNumber = licenseNumber.ifBlank { null },
                remarks = remarks.ifBlank { null }
            )

            when (val result = safeApiCall { repository.createBatchItems(request) }) {
                is ApiResult.Success -> {
                    val body = result.data
                    if (body.success) {
                        successMessage = "${body.createdCount} items created successfully"
                        resetForm()
                        onSuccess()
                    } else {
                        onError(body.message.ifBlank { "We couldn't create the items right now. Please try again." })
                    }
                }
                is ApiResult.HttpError -> {
                    onError(result.userMessageOr("We couldn't create the items right now. Please try again."))
                }
                is ApiResult.NetworkError -> {
                    onError(result.userMessageOr(SCANNER_NETWORK_MESSAGE))
                }
                is ApiResult.UnknownError -> {
                    onError(result.userMessageOr(SCANNER_GENERIC_MESSAGE))
                }
            }
            isSaving = false
        }
    }

    private fun resetForm() {
        itemName = ""
        description = ""
        selectedType = "Hardware"
        modelNumber = ""
        currentSerial = ""
        serialNumbers = emptyList()
        phoneItems = listOf(PhoneItemEntry())
        selectedCategory = null
        unitOptions = listOf("Unit", "Piece", "Box", "Pack", "Set")
        selectedUnit = unitOptions.first()
        amount = ""
        startDate = ""
        endDate = ""
        selectedCondition = conditions.find { 
            it.conditionName.equals("Good", ignoreCase = true) 
        } ?: conditions.firstOrNull()
        selectedVendor = null
        warrantyYears = ""
        datePurchased = ""
        licenseNumber = ""
        remarks = ""
    }
}


