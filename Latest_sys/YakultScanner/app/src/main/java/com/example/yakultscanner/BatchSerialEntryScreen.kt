package com.example.yakultscanner

import android.widget.Toast
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.R
import com.example.yakultscanner.ui.components.BatchDateField
import com.example.yakultscanner.ui.components.BatchDropdownField
import com.example.yakultscanner.ui.components.BatchSectionHeader
import com.example.yakultscanner.ui.components.SerialItemCard
import com.example.yakultscanner.viewmodels.BatchEntryViewModel
import com.example.yakultscanner.viewmodels.PhoneItemEntry

import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BatchSerialEntryScreen(
    navController: NavController,
    viewModel: BatchEntryViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val snackbarHostState = remember { SnackbarHostState() }
    var showConfirmDialog by remember { mutableStateOf(false) }

    // Listen for scanned results (reactively) and add to serial list.
    val navBackStackEntry = navController.currentBackStackEntryAsState().value
    val scannedSerialFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }
    val scannedSerial = scannedSerialFlow?.collectAsState(initial = null)?.value

    val scannedSerialsFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<java.util.ArrayList<String>?>(SCAN_RESULT_SERIALS_KEY, null)
    }
    val scannedSerials = scannedSerialsFlow?.collectAsState(initial = null)?.value


    LaunchedEffect(scannedSerial) {
        val scanned = scannedSerial?.trim().orEmpty()
        if (scanned.isBlank()) return@LaunchedEffect

        viewModel.updateCurrentSerial(scanned)
        if (viewModel.isCellphoneCategory()) viewModel.addPhoneSerialFromScan(scanned) else viewModel.addSerial()
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
    }

    LaunchedEffect(scannedSerials) {
        val batch = scannedSerials?.toList().orEmpty()
        if (batch.isEmpty()) return@LaunchedEffect

        for (value in batch) {
            val scanned = value.trim()
            if (scanned.isBlank()) continue
            viewModel.updateCurrentSerial(scanned)
            if (viewModel.isCellphoneCategory()) viewModel.addPhoneSerialFromScan(scanned) else viewModel.addSerial()
        }
        navBackStackEntry?.savedStateHandle?.remove<java.util.ArrayList<String>>(SCAN_RESULT_SERIALS_KEY)
    }


    // Hero Gradient
    val backgroundBrush = Brush.verticalGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            Color(0xFFB71C1C) // Darker Red
        )
    )

    // Show errors/success messages via Snackbar
    LaunchedEffect(viewModel.errorMessage) {
        viewModel.errorMessage?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.clearError()
        }
    }

    LaunchedEffect(viewModel.successMessage) {
        viewModel.successMessage?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.clearSuccess()
        }
    }

    Scaffold(
        topBar = {
            IconButton(
                onClick = { navController.popBackStack() },
                modifier = Modifier.padding(8.dp)
            ) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = Color.White)
            }
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(backgroundBrush)
                .padding(paddingValues)
        ) {
            // Background Texture
            Image(
                painter = painterResource(id = R.drawable.yor_splash_login_bg),
                contentDescription = null,
                modifier = Modifier
                    .fillMaxSize()
                    .graphicsLayer(alpha = 0.5f),
                contentScale = ContentScale.Crop
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 24.dp, vertical = 20.dp),
                verticalArrangement = Arrangement.spacedBy(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Header Branding
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(
                        text = stringResource(id = R.string.batch_entry_title),
                        style = MaterialTheme.typography.displaySmall,
                        fontWeight = FontWeight.Bold,
                        color = Color.White,
                        textAlign = TextAlign.Center
                    )

                    Text(
                        text = stringResource(id = R.string.batch_entry_subtitle),
                        style = MaterialTheme.typography.bodyLarge,
                        color = Color.White.copy(alpha = 0.8f)
                    )
                }

                // Floating White Card
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(28.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surface
                    ),
                    elevation = CardDefaults.cardElevation(defaultElevation = 12.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(24.dp),
                        verticalArrangement = Arrangement.spacedBy(16.dp)
                    ) {
                        
                        BatchSectionHeader(text = stringResource(id = R.string.item_details_header))

                        OutlinedTextField(
                            value = viewModel.itemName,
                            onValueChange = { viewModel.updateItemName(it) },
                            label = { Text(stringResource(id = R.string.item_name_label)) },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp)
                        )

                        OutlinedTextField(
                            value = viewModel.description,
                            onValueChange = { viewModel.updateDescription(it) },
                            label = { Text(stringResource(id = R.string.description_label)) },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp)
                        )

                        BatchDropdownField(
                            label = stringResource(id = R.string.type_label),
                            selectedValue = viewModel.selectedType,
                            options = viewModel.typeOptions,
                            optionLabel = { it },
                            onOptionSelected = { viewModel.updateSelectedType(it) },
                            expanded = viewModel.typeExpanded,
                            onExpandedChange = { viewModel.typeExpanded = it }
                        )

                        if (viewModel.selectedType == "Software/License") {
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                BatchDateField(
                                    label = stringResource(id = R.string.start_date_label),
                                    value = viewModel.startDate,
                                    onDateSelected = { viewModel.onStartDateSelected(it) },
                                    modifier = Modifier.weight(1f)
                                )
                                BatchDateField(
                                    label = stringResource(id = R.string.end_date_label),
                                    value = viewModel.endDate,
                                    onDateSelected = { viewModel.onEndDateSelected(it) },
                                    modifier = Modifier.weight(1f)
                                )
                            }
                        }

                        OutlinedTextField(
                            value = viewModel.modelNumber,
                            onValueChange = { viewModel.updateModelNumber(it) },
                            label = { Text(stringResource(id = R.string.model_number_label)) },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp)
                        )

                        BatchDropdownField(
                            label = stringResource(id = R.string.category_label),
                            selectedValue = viewModel.selectedCategory?.name ?: "",
                            options = viewModel.categories,
                            optionLabel = { it.name },
                            onOptionSelected = { viewModel.updateSelectedCategory(it) },
                            expanded = viewModel.categoryExpanded,
                            onExpandedChange = { viewModel.categoryExpanded = it },
                            placeholder = stringResource(id = R.string.select_option)
                        )

                        BatchDropdownField(
                            label = stringResource(id = R.string.condition_label),
                            selectedValue = viewModel.selectedCondition?.conditionName ?: "",
                            options = viewModel.conditions,
                            optionLabel = { it.conditionName },
                            onOptionSelected = { viewModel.updateSelectedCondition(it) },
                            expanded = viewModel.conditionExpanded,
                            onExpandedChange = { viewModel.conditionExpanded = it }
                        )

                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            BatchDropdownField(
                                label = stringResource(id = R.string.unit_label),
                                selectedValue = viewModel.selectedUnit,
                                options = viewModel.unitOptions,
                                optionLabel = { it },
                                onOptionSelected = { viewModel.updateSelectedUnit(it) },
                                expanded = viewModel.unitExpanded,
                                onExpandedChange = { viewModel.unitExpanded = it },
                                modifier = Modifier.weight(1f)
                            )

                            OutlinedTextField(
                                value = viewModel.amount,
                                onValueChange = { viewModel.updateAmount(it) },
                                label = { Text(stringResource(id = R.string.amount_label)) },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(12.dp)
                            )
                        }

                        if (viewModel.selectedType != "Hardware") {
                            OutlinedTextField(
                                value = viewModel.licenseNumber,
                                onValueChange = { viewModel.updateLicenseNumber(it) },
                                label = { Text(stringResource(id = R.string.remarks_label)) },
                                modifier = Modifier.fillMaxWidth(),
                                singleLine = true,
                                shape = RoundedCornerShape(12.dp)
                            )
                        }

                        Spacer(modifier = Modifier.height(8.dp))
                        
                        BatchSectionHeader(text = stringResource(id = R.string.purchase_details_header))

                        BatchDropdownField(
                            label = stringResource(id = R.string.vendor_label),
                            selectedValue = viewModel.selectedVendor?.vendorName ?: "",
                            options = viewModel.vendors,
                            optionLabel = { it.vendorName },
                            onOptionSelected = { viewModel.updateSelectedVendor(it) },
                            expanded = viewModel.vendorExpanded,
                            onExpandedChange = { viewModel.vendorExpanded = it },
                            placeholder = "(None)"
                        )

                        if (viewModel.selectedType != "Software/License") {
                            OutlinedTextField(
                                value = viewModel.warrantyYears,
                                onValueChange = { viewModel.updateWarrantyYears(it) },
                                label = { Text(stringResource(id = R.string.warranty_label)) },
                                modifier = Modifier.fillMaxWidth(),
                                singleLine = true,
                                shape = RoundedCornerShape(12.dp)
                            )
                        }

                        BatchDateField(
                            label = stringResource(id = R.string.date_purchased_label),
                            value = viewModel.datePurchased,
                            onDateSelected = { viewModel.onDatePurchasedSelected(it) }
                        )

                        OutlinedTextField(
                            value = viewModel.remarks,
                            onValueChange = { viewModel.updateRemarks(it) },
                            label = { Text(stringResource(id = R.string.remarks_label)) },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp)
                        )

                        Spacer(modifier = Modifier.height(8.dp))

                        BatchSectionHeader(text = stringResource(id = R.string.serial_numbers_header))

                        if (viewModel.isCellphoneCategory()) {
                            var phoneRowCount by remember { mutableStateOf("") }
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                OutlinedTextField(
                                    value = phoneRowCount,
                                    onValueChange = { value -> phoneRowCount = value.filter { it.isDigit() }.take(3) },
                                    label = { Text("Phone quantity") },
                                    modifier = Modifier.weight(1f),
                                    singleLine = true,
                                    shape = RoundedCornerShape(12.dp)
                                )
                                Button(onClick = { viewModel.generatePhoneRows(phoneRowCount) }) {
                                    Text("Create")
                                }
                                TextButton(onClick = { viewModel.addPhoneRow() }) {
                                    Text("+ Add Row")
                                }
                            }

                            viewModel.phoneItems.forEachIndexed { index, row ->
                                PhoneItemEntryCard(
                                    index = index,
                                    row = row,
                                    onChange = { viewModel.updatePhoneRow(index, it) },
                                    onRemove = { viewModel.removePhoneRow(index) },
                                    canRemove = viewModel.phoneItems.size > 1
                                )
                            }
                        } else {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                OutlinedTextField(
                                    value = viewModel.currentSerial,
                                    onValueChange = { viewModel.updateCurrentSerial(it) },
                                    label = { Text(stringResource(id = R.string.enter_serial_placeholder)) },
                                    modifier = Modifier.weight(1f),
                                    singleLine = true,
                                    shape = RoundedCornerShape(12.dp)
                                )
                                IconButton(
                                    onClick = {
                                        navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                        navController.navigate("scanner_camera")
                                    },
                                    modifier = Modifier.padding(top = 8.dp)
                                ) {
                                    Icon(Icons.Default.QrCodeScanner, "Scan serial", tint = MaterialTheme.colorScheme.primary)
                                }
                                IconButton(
                                    onClick = { viewModel.addSerial() },
                                    modifier = Modifier.padding(top = 8.dp)
                                ) {
                                    Icon(Icons.Default.Add, "Add serial", tint = MaterialTheme.colorScheme.primary)
                                }
                            }

                            viewModel.serialNumbers.forEach { serial ->
                                SerialItemCard(
                                    serial = serial,
                                    onDelete = { viewModel.removeSerial(serial) }
                                )
                            }
                        }

                        Spacer(modifier = Modifier.height(16.dp))

                        Button(
                            onClick = { showConfirmDialog = true },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(50.dp),
                            enabled = !viewModel.isSaving && viewModel.isFormValid(),
                            shape = RoundedCornerShape(12.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.primary
                            )
                        ) {
                            if (viewModel.isSaving) {
                                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
                            } else {
                                Text(
                                    text = stringResource(id = R.string.save_items_button_text, viewModel.serialNumbers.size),
                                    fontWeight = FontWeight.Bold
                                )
                            }
                        }
                    }
                }
                
                // Add bottom spacer to account for the floating navigation bar
                Spacer(modifier = Modifier.height(100.dp))
            }
        }
    }

    if (showConfirmDialog) {
        BatchSubmitConfirmDialog(
            itemName = viewModel.itemName,
            itemType = viewModel.selectedType,
            category = viewModel.selectedCategory?.name ?: "",
            condition = viewModel.selectedCondition?.conditionName ?: "",
            modelNumber = viewModel.modelNumber,
            vendor = viewModel.selectedVendor?.vendorName ?: "",
            serialNumbers = viewModel.serialNumbers.toList(),
            onDismiss = { showConfirmDialog = false },
            onConfirm = {
                showConfirmDialog = false
                viewModel.saveBatchItems(
                    onSuccess = {
                        Toast.makeText(context, "Items saved successfully", Toast.LENGTH_SHORT).show()
                    },
                    onError = { error ->
                        Toast.makeText(context, error, Toast.LENGTH_LONG).show()
                    }
                )
            }
        )
    }
}

@Composable
private fun PhoneItemEntryCard(
    index: Int,
    row: PhoneItemEntry,
    onChange: (PhoneItemEntry) -> Unit,
    onRemove: () -> Unit,
    canRemove: Boolean
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f))
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Phone ${index + 1}", fontWeight = FontWeight.Bold, modifier = Modifier.weight(1f))
                if (canRemove) TextButton(onClick = onRemove) { Text("Remove") }
            }
            OutlinedTextField(
                value = row.cellPhoneNumber,
                onValueChange = { onChange(row.copy(cellPhoneNumber = it)) },
                label = { Text("Cell Phone Number") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )
            OutlinedTextField(
                value = row.serialNumber,
                onValueChange = { onChange(row.copy(serialNumber = it)) },
                label = { Text("Serial Number *") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = row.imei1,
                    onValueChange = { onChange(row.copy(imei1 = it)) },
                    label = { Text("IMEI 1") },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp)
                )
                OutlinedTextField(
                    value = row.imei2,
                    onValueChange = { onChange(row.copy(imei2 = it)) },
                    label = { Text("IMEI 2") },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp)
                )
            }
        }
    }
}
@Composable
private fun BatchSubmitConfirmDialog(
    itemName: String,
    itemType: String,
    category: String,
    condition: String,
    modelNumber: String,
    vendor: String,
    serialNumbers: List<String>,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit
) {
    val headerBrush = Brush.verticalGradient(
        colors = listOf(Color(0xFFD32F2F), Color(0xFFB71C1C))
    )

    Dialog(onDismissRequest = onDismiss) {
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp)
        ) {
            Column {
                // Red gradient header
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(headerBrush)
                        .padding(horizontal = 20.dp, vertical = 16.dp)
                ) {
                    Column {
                        Text(
                            text = "Confirm Submission",
                            color = Color.White,
                            fontWeight = FontWeight.Bold,
                            style = MaterialTheme.typography.titleLarge
                        )
                        Text(
                            text = "Review item details before saving",
                            color = Color.White.copy(alpha = 0.8f),
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }

                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    // Item details section
                    Text(
                        text = "Item Details",
                        fontWeight = FontWeight.SemiBold,
                        style = MaterialTheme.typography.titleSmall,
                        color = MaterialTheme.colorScheme.primary
                    )

                    @Composable
                    fun DetailRow(label: String, value: String) {
                        if (value.isNotBlank()) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = label,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = Color.Gray,
                                    modifier = Modifier.weight(1f)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    text = value,
                                    style = MaterialTheme.typography.bodySmall,
                                    fontWeight = FontWeight.Medium,
                                    textAlign = TextAlign.End,
                                    modifier = Modifier.weight(1.5f)
                                )
                            }
                        }
                    }

                    Card(
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                        )
                    ) {
                        Column(
                            modifier = Modifier.padding(12.dp),
                            verticalArrangement = Arrangement.spacedBy(6.dp)
                        ) {
                            DetailRow("Name", itemName)
                            DetailRow("Type", itemType)
                            DetailRow("Category", category)
                            DetailRow("Condition", condition)
                            DetailRow("Model", modelNumber)
                            DetailRow("Vendor", vendor)
                        }
                    }

                    HorizontalDivider()

                    // Serial numbers summary bar
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.CheckCircle,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(20.dp)
                        )
                        Text(
                            text = "${serialNumbers.size} Serial Number(s) to submit",
                            fontWeight = FontWeight.SemiBold,
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }

                    // Scrollable serial list
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(min = 40.dp, max = 180.dp)
                            .border(
                                1.dp,
                                MaterialTheme.colorScheme.outlineVariant,
                                RoundedCornerShape(10.dp)
                            ),
                        verticalArrangement = Arrangement.spacedBy(0.dp)
                    ) {
                        itemsIndexed(serialNumbers) { index, serial ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .background(
                                        if (index % 2 == 0) Color.Transparent
                                        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f)
                                    )
                                    .padding(horizontal = 12.dp, vertical = 8.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(10.dp)
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(24.dp)
                                        .background(
                                            MaterialTheme.colorScheme.primary.copy(alpha = 0.15f),
                                            CircleShape
                                        ),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Text(
                                        text = "${index + 1}",
                                        fontSize = 10.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = MaterialTheme.colorScheme.primary
                                    )
                                }
                                Text(
                                    text = serial,
                                    style = MaterialTheme.typography.bodySmall,
                                    modifier = Modifier.weight(1f)
                                )
                            }
                        }
                    }

                    // Action buttons
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        TextButton(
                            onClick = onDismiss,
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Cancel")
                        }
                        Button(
                            onClick = onConfirm,
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(10.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.primary
                            )
                        ) {
                            Text(
                                text = "Submit ${serialNumbers.size} Item(s)",
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.sp
                            )
                        }
                    }
                }
            }
        }
    }
}


