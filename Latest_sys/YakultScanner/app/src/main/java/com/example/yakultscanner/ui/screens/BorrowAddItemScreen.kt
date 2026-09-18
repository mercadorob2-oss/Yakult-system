package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.BatchItemRequest
import com.example.yakultscanner.api.BatchItemsRequest
import com.example.yakultscanner.api.ConditionDto
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.VendorDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.ui.components.BatchDateField
import com.example.yakultscanner.ui.components.BatchDropdownField
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BorrowAddItemScreen(navController: NavController) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }

    val categories = remember { mutableStateListOf<ItemCategoryDto>() }
    val conditions = remember { mutableStateListOf<ConditionDto>() }
    val vendors = remember { mutableStateListOf<VendorDto>() }

    var itemName by rememberSaveable { mutableStateOf("") }
    var description by rememberSaveable { mutableStateOf("") }
    var itemType by rememberSaveable { mutableStateOf("Hardware") }
    var modelNumber by rememberSaveable { mutableStateOf("") }
    var serialNumber by rememberSaveable { mutableStateOf("") }
    var unitOfMeasure by rememberSaveable { mutableStateOf("Unit") }
    var amount by rememberSaveable { mutableStateOf("") }
    var warrantyYears by rememberSaveable { mutableStateOf("") }
    var datePurchased by rememberSaveable { mutableStateOf("") }
    var licenseNumber by rememberSaveable { mutableStateOf("") }
    var remarks by rememberSaveable { mutableStateOf("") }
    var startDate by rememberSaveable { mutableStateOf("") }
    var endDate by rememberSaveable { mutableStateOf("") }

    var selectedCategory by remember { mutableStateOf<ItemCategoryDto?>(null) }
    var selectedCondition by remember { mutableStateOf<ConditionDto?>(null) }
    var selectedVendor by remember { mutableStateOf<VendorDto?>(null) }

    var categoryExpanded by remember { mutableStateOf(false) }
    var conditionExpanded by remember { mutableStateOf(false) }
    var vendorExpanded by remember { mutableStateOf(false) }
    var unitExpanded by remember { mutableStateOf(false) }
    var typeExpanded by remember { mutableStateOf(false) }

    var loadingLookups by remember { mutableStateOf(true) }
    var saving by remember { mutableStateOf(false) }

    fun showMessage(message: String) {
        scope.launch { snackbarHostState.showSnackbar(message) }
    }

    LaunchedEffect(Unit) {
        loadingLookups = true

        when (val result = safeApiCall { ApiClient.service.getItemCategories() }) {
            is ApiResult.Success -> categories.addAll(result.data)
            is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load categories right now."))
            is ApiResult.NetworkError -> showMessage(result.userMessageOr())
            is ApiResult.UnknownError -> showMessage(result.userMessageOr())
        }

        when (val result = safeApiCall { ApiClient.service.getConditions() }) {
            is ApiResult.Success -> {
                conditions.addAll(result.data)
                selectedCondition = result.data.find { it.conditionName.equals("Good", ignoreCase = true) } ?: result.data.firstOrNull()
            }
            is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load conditions right now."))
            is ApiResult.NetworkError -> showMessage(result.userMessageOr())
            is ApiResult.UnknownError -> showMessage(result.userMessageOr())
        }

        when (val result = safeApiCall { ApiClient.service.getVendors() }) {
            is ApiResult.Success -> vendors.addAll(result.data)
            is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load vendors right now."))
            is ApiResult.NetworkError -> showMessage(result.userMessageOr())
            is ApiResult.UnknownError -> showMessage(result.userMessageOr())
        }

        loadingLookups = false
    }

    fun onDatePurchasedSelected(millis: Long) {
        datePurchased = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(Date(millis))
    }

    fun onStartDateSelected(millis: Long) {
        startDate = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(Date(millis))
    }

    fun onEndDateSelected(millis: Long) {
        endDate = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(Date(millis))
    }

    fun createItemAndContinue() {
        val normalizedSerial = serialNumber.trim().uppercase(Locale.ROOT)
        if (itemName.isBlank()) return showMessage("Enter an item name.")
        if (modelNumber.isBlank()) return showMessage("Enter a model number.")
        if (normalizedSerial.isBlank()) return showMessage("Enter a serial number.")
        if (selectedCategory == null) return showMessage("Select a category.")
        if (selectedCondition == null) return showMessage("Select a condition.")

        saving = true
        scope.launch {
            val createdBy = UserSession.currentUser?.displayName ?: "Unknown"
            val request = BatchItemsRequest(
                items = listOf(
                    BatchItemRequest(
                        serialNumber = normalizedSerial,
                        modelNumber = modelNumber.trim(),
                        itemType = itemType
                    )
                ),
                createdBy = createdBy,
                itemName = itemName.trim(),
                description = description.trim().takeIf { it.isNotBlank() },
                categoryId = selectedCategory?.categoryId,
                categoryName = selectedCategory?.name,
                unitOfMeasure = unitOfMeasure,
                amount = amount.toDoubleOrNull(),
                startDate = if (itemType == "Software/License") startDate.takeIf { it.isNotBlank() } else null,
                endDate = if (itemType == "Software/License") endDate.takeIf { it.isNotBlank() } else null,
                conditionId = selectedCondition?.conditionId,
                vendorId = selectedVendor?.vendorId,
                warrantyYears = warrantyYears.toIntOrNull(),
                datePurchased = datePurchased.takeIf { it.isNotBlank() },
                licenseNumber = licenseNumber.takeIf { it.isNotBlank() },
                remarks = remarks.takeIf { it.isNotBlank() }
            )

            when (val result = safeApiCall { ApiClient.service.createBatchItems(request) }) {
                is ApiResult.Success -> {
                    if (result.data.success && result.data.createdCount > 0) {
                        navController.previousBackStackEntry?.savedStateHandle?.set("created_borrow_serial", normalizedSerial)
                        navController.popBackStack()
                    } else {
                        showMessage(result.data.message.ifBlank { "We couldn't create the item right now." })
                    }
                }
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't create the item right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
            saving = false
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Add New Item") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(
                    Brush.verticalGradient(
                        colors = listOf(
                            MaterialTheme.colorScheme.primary,
                            MaterialTheme.colorScheme.primaryContainer,
                            MaterialTheme.colorScheme.background
                        )
                    )
                )
                .padding(paddingValues)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 20.dp, vertical = 12.dp),
                verticalArrangement = Arrangement.spacedBy(18.dp)
            ) {
                Text(
                    text = "Create the item first, then the borrow form will continue with its serial already filled in.",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.92f)
                )

                Card(
                    shape = RoundedCornerShape(24.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 10.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(14.dp)
                    ) {
                        Text("Item Details", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleMedium)

                        OutlinedTextField(value = itemName, onValueChange = { itemName = it }, label = { Text("Item name") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                        OutlinedTextField(value = description, onValueChange = { description = it }, label = { Text("Description") }, modifier = Modifier.fillMaxWidth())

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            BatchDropdownField(
                                label = "Type",
                                selectedValue = itemType,
                                options = listOf("Hardware", "Software/License", "Services"),
                                optionLabel = { it },
                                onOptionSelected = { itemType = it },
                                expanded = typeExpanded,
                                onExpandedChange = { typeExpanded = it },
                                modifier = Modifier.weight(1f)
                            )
                            BatchDropdownField(
                                label = "Unit",
                                selectedValue = unitOfMeasure,
                                options = if (itemType == "Hardware") listOf("Unit", "Piece", "Box", "Pack", "Set") else listOf("Monthly", "Annually", "One Time"),
                                optionLabel = { it },
                                onOptionSelected = { unitOfMeasure = it },
                                expanded = unitExpanded,
                                onExpandedChange = { unitExpanded = it },
                                modifier = Modifier.weight(1f)
                            )
                        }

                        OutlinedTextField(value = modelNumber, onValueChange = { modelNumber = it }, label = { Text("Model number") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                        OutlinedTextField(value = serialNumber, onValueChange = { serialNumber = it }, label = { Text("Serial number") }, modifier = Modifier.fillMaxWidth(), singleLine = true)

                        BatchDropdownField(
                            label = "Category",
                            selectedValue = selectedCategory?.name.orEmpty(),
                            options = categories,
                            optionLabel = { it.name },
                            onOptionSelected = { selectedCategory = it },
                            expanded = categoryExpanded,
                            onExpandedChange = { categoryExpanded = it },
                            modifier = Modifier.fillMaxWidth(),
                            placeholder = if (loadingLookups) "Loading..." else "Select category"
                        )

                        BatchDropdownField(
                            label = "Condition",
                            selectedValue = selectedCondition?.conditionName.orEmpty(),
                            options = conditions,
                            optionLabel = { it.conditionName },
                            onOptionSelected = { selectedCondition = it },
                            expanded = conditionExpanded,
                            onExpandedChange = { conditionExpanded = it },
                            modifier = Modifier.fillMaxWidth(),
                            placeholder = if (loadingLookups) "Loading..." else "Select condition"
                        )

                        BatchDropdownField(
                            label = "Vendor",
                            selectedValue = selectedVendor?.vendorName.orEmpty(),
                            options = vendors,
                            optionLabel = { it.vendorName },
                            onOptionSelected = { selectedVendor = it },
                            expanded = vendorExpanded,
                            onExpandedChange = { vendorExpanded = it },
                            modifier = Modifier.fillMaxWidth(),
                            placeholder = "(Optional)"
                        )

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            OutlinedTextField(value = amount, onValueChange = { amount = it }, label = { Text("Amount") }, modifier = Modifier.weight(1f), singleLine = true)
                            OutlinedTextField(value = warrantyYears, onValueChange = { if (it.all { ch -> ch.isDigit() }) warrantyYears = it }, label = { Text("Warranty years") }, modifier = Modifier.weight(1f), singleLine = true)
                        }

                        if (itemType == "Software/License") {
                            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                BatchDateField(label = "Start date", value = startDate, onDateSelected = ::onStartDateSelected, modifier = Modifier.weight(1f))
                                BatchDateField(label = "End date", value = endDate, onDateSelected = ::onEndDateSelected, modifier = Modifier.weight(1f))
                            }
                            OutlinedTextField(value = licenseNumber, onValueChange = { licenseNumber = it }, label = { Text("License number") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                        }

                        BatchDateField(label = "Date purchased", value = datePurchased, onDateSelected = ::onDatePurchasedSelected, modifier = Modifier.fillMaxWidth())
                        OutlinedTextField(value = remarks, onValueChange = { remarks = it }, label = { Text("Remarks") }, modifier = Modifier.fillMaxWidth())

                        Spacer(modifier = Modifier.height(6.dp))

                        Button(
                            onClick = ::createItemAndContinue,
                            enabled = !saving,
                            modifier = Modifier.fillMaxWidth().height(52.dp),
                            shape = RoundedCornerShape(16.dp)
                        ) {
                            if (saving) {
                                CircularProgressIndicator(color = LocalContentColor.current)
                            } else {
                                Text("Create Item and Continue Borrow")
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(84.dp))
            }
        }
    }
}
