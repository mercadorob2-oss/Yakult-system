package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallConditionDto
import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.ResolutionRequest
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.ResolutionUiState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CallMarkAsResolvedScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val snackbarHostState = remember { SnackbarHostState() }
    val resolutionState by viewModel.resolutionState.collectAsStateWithLifecycle()
    val outItems by viewModel.resolutionOutItems.collectAsStateWithLifecycle()
    val stockItems by viewModel.resolutionStockItems.collectAsStateWithLifecycle()
    val conditions by viewModel.resolutionConditions.collectAsStateWithLifecycle()
    val categories by viewModel.resolutionCategories.collectAsStateWithLifecycle()
    val isLoadingLookups by viewModel.resolutionLoadingLookups.collectAsStateWithLifecycle()

    var wizardStep by remember { mutableStateOf(0) }  // 0=Type, 1=Details, 2=Review

    // Resolution type
    var isReplacement by remember { mutableStateOf(false) }

    // Old item selection
    var useUnlisted by remember { mutableStateOf(false) }
    var oldCategoryFilter by remember { mutableStateOf("All Categories") }
    var selectedOldItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var unlistedName by remember { mutableStateOf("") }
    var unlistedModel by remember { mutableStateOf("") }
    var unlistedSerial by remember { mutableStateOf("") }
    var unlistedUnit by remember { mutableStateOf("Unit") }
    var unlistedDesc by remember { mutableStateOf("") }
    var unlistedCategory by remember { mutableStateOf<ItemCategoryDto?>(null) }

    // Condition
    var selectedCondition by remember { mutableStateOf<CallConditionDto?>(null) }
    var conditionRemarks by remember { mutableStateOf("") }

    // Old unit action
    var repairAction by remember { mutableStateOf("Repaired") }
    var userManuallySetRepairAction by remember { mutableStateOf(false) }

    // New item
    var newCategoryFilter by remember { mutableStateOf("All Categories") }
    var selectedNewItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var quantity by remember { mutableStateOf("1") }
    var isTemporary by remember { mutableStateOf(false) }

    // Remarks
    var remarks by remember { mutableStateOf("") }

    // Derived data
    val outCategories = remember(outItems) {
        listOf("All Categories") + outItems.map { it.category ?: "" }.filter { it.isNotBlank() }.distinct().sorted()
    }
    val stockCategories = remember(stockItems) {
        listOf("All Categories") + stockItems.map { it.category ?: "" }.filter { it.isNotBlank() }.distinct().sorted()
    }
    val filteredOldItems = remember(outItems, oldCategoryFilter) {
        if (oldCategoryFilter == "All Categories") outItems
        else outItems.filter { it.category == oldCategoryFilter }
    }
    val filteredNewItems = remember(stockItems, newCategoryFilter) {
        if (newCategoryFilter == "All Categories") stockItems
        else stockItems.filter { it.category == newCategoryFilter }
    }
    val selectedNewItemStock = selectedNewItem?.stockOnHand ?: 0
    val qty = quantity.toIntOrNull() ?: 1

    val canConfirm = when {
        !isReplacement -> remarks.isNotBlank()
        useUnlisted -> unlistedName.isNotBlank() && unlistedModel.isNotBlank() && unlistedCategory != null && selectedCondition != null && selectedNewItem != null && qty > 0 && qty <= selectedNewItemStock && remarks.isNotBlank()
        else -> selectedOldItem != null && selectedNewItem != null && selectedOldItem?.itemId != selectedNewItem?.itemId && selectedCondition != null && qty > 0 && qty <= selectedNewItemStock && remarks.isNotBlank()
    }

    val isProcessing = resolutionState is ResolutionUiState.Loading

    // Load lookups on entry
    LaunchedEffect(ticketId) {
        viewModel.loadResolutionLookups()
    }

    // Auto-select "Unrepaired" when condition is Damaged (unless user manually chose an action)
    LaunchedEffect(selectedCondition) {
        val condition = selectedCondition
        if (condition != null && !userManuallySetRepairAction) {
            if (condition.conditionName.contains("Damage", ignoreCase = true)) {
                repairAction = "Unrepaired"
            }
        }
    }

    // Observe resolution result
    LaunchedEffect(resolutionState) {
        when (val state = resolutionState) {
            is ResolutionUiState.Success -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
                viewModel.loadTicketDetail(ticketId)
                navController.popBackStack()
            }
            is ResolutionUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Mark As Resolved") },
                navigationIcon = {
                    IconButton(onClick = { if (wizardStep > 0) wizardStep-- else navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    titleContentColor = MaterialTheme.colorScheme.onPrimaryContainer
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = {
            if (wizardStep < 2) {
                Button(
                    onClick = { wizardStep = if (wizardStep == 0) (if (isReplacement) 1 else 2) else 2 },
                    modifier = Modifier.fillMaxWidth().padding(16.dp).height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    enabled = when (wizardStep) {
                        0 -> if (isReplacement) true else remarks.isNotBlank()
                        1 -> {
                            if (!isReplacement) false
                            else if (useUnlisted) unlistedName.isNotBlank() && unlistedModel.isNotBlank() && unlistedCategory != null && selectedCondition != null && selectedNewItem != null && qty > 0 && qty <= selectedNewItemStock
                            else selectedOldItem != null && selectedCondition != null && selectedNewItem != null && qty > 0 && qty <= selectedNewItemStock
                        }
                        else -> false
                    }
                ) {
                    Text(if (wizardStep == 0 && !isReplacement) "Continue" else "Next")
                }
            } else {
                Button(
                    onClick = {
                        val request = buildResolutionRequest(ticketId, isReplacement, useUnlisted, selectedOldItem,
                            unlistedName, unlistedModel, unlistedSerial, unlistedUnit, unlistedDesc, unlistedCategory,
                            selectedCondition, conditionRemarks, repairAction, selectedNewItem, qty, isTemporary, remarks)
                        viewModel.applyResolution(request)
                    },
                    modifier = Modifier.fillMaxWidth().padding(16.dp).height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    enabled = canConfirm && !isProcessing
                ) {
                    if (isProcessing) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = MaterialTheme.colorScheme.onPrimary)
                    else Text("Confirm Resolution")
                }
            }
        }
    ) { padding ->
        if (isLoadingLookups) {
            Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .padding(horizontal = 16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Spacer(Modifier.height(4.dp))

                // Step indicator
                Text(
                    text = when {
                        wizardStep == 0 -> "Step 1: Resolution Type"
                        wizardStep == 1 -> "Step 2: Replacement Details"
                        else -> if (isReplacement) "Step 3: Review & Confirm" else "Step 2: Review & Confirm"
                    },
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )

                // Step 0: Resolution type
                if (wizardStep == 0) {
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Resolution Type", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { isReplacement = false }) {
                                RadioButton(selected = !isReplacement, onClick = { isReplacement = false })
                                Text("Service Only (No Parts)", modifier = Modifier.padding(start = 4.dp))
                            }
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { isReplacement = true }) {
                                RadioButton(selected = isReplacement, onClick = { isReplacement = true })
                                Text("Replacement (Parts Used)", modifier = Modifier.padding(start = 4.dp))
                            }
                            if (!isReplacement) {
                                Spacer(Modifier.height(4.dp))
                                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { isTemporary = !isTemporary }) {
                                    Checkbox(checked = isTemporary, onCheckedChange = { isTemporary = it })
                                    Text("Temporary service (marks as Resolved (Temporary))", style = MaterialTheme.typography.bodySmall)
                                }
                                Spacer(Modifier.height(4.dp))
                                Text("Enter service notes about the resolution.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            } else {
                                Spacer(Modifier.height(4.dp))
                                Text("Record the replaced item and the new item issued from stock.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }

                    // Service notes (always shown in step 0)
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text(if (isReplacement) "Replacement remarks" else "Service notes", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            OutlinedTextField(
                                value = remarks,
                                onValueChange = { remarks = it },
                                placeholder = { Text("Describe the resolution...") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                maxLines = 4
                            )
                        }
                    }
                }

                // Step 1: Replacement details
                if (wizardStep == 1 && isReplacement) {
                    // Old Item Section
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Item Being Replaced (Old Unit)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { useUnlisted = false }) {
                                RadioButton(selected = !useUnlisted, onClick = { useUnlisted = false })
                                Text("Listed in Inventory", modifier = Modifier.padding(start = 4.dp))
                            }
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { useUnlisted = true }) {
                                RadioButton(selected = useUnlisted, onClick = { useUnlisted = true })
                                Text("Not Listed", modifier = Modifier.padding(start = 4.dp))
                            }

                            if (useUnlisted) {
                                OutlinedTextField(value = unlistedName, onValueChange = { unlistedName = it }, label = { Text("Name*") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                OutlinedTextField(value = unlistedModel, onValueChange = { unlistedModel = it }, label = { Text("Model*") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                OutlinedTextField(value = unlistedSerial, onValueChange = { unlistedSerial = it }, label = { Text("Serial") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                UnitDropdown(value = unlistedUnit, onValueChange = { unlistedUnit = it })
                                OutlinedTextField(value = unlistedDesc, onValueChange = { unlistedDesc = it }, label = { Text("Description") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), maxLines = 2)
                                ItemCategoryDropdown(
                                    categories = categories,
                                    selected = unlistedCategory,
                                    onSelected = { unlistedCategory = it }
                                )
                            } else {
                                CategoryDropdown(label = "Filter by Category", categories = outCategories, selected = oldCategoryFilter, onSelected = { oldCategoryFilter = it })
                                ItemDropdown(label = "Select Old Item", items = filteredOldItems, selected = selectedOldItem, onSelected = { selectedOldItem = it })
                            }
                        }
                    }

                    // Condition
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Old Item Condition", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            ConditionDropdown(conditions = conditions, selected = selectedCondition, onSelected = { selectedCondition = it })
                            OutlinedTextField(value = conditionRemarks, onValueChange = { conditionRemarks = it }, label = { Text("Remarks") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), maxLines = 2)
                        }
                    }

                    // Old Unit Action
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            Text("Old Unit Action (After Pullout)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            listOf("Repaired", "Unrepaired", "Repaired - Spare inventory").forEach { action ->
                                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { repairAction = action; userManuallySetRepairAction = true }) {
                                    RadioButton(selected = repairAction == action, onClick = { repairAction = action; userManuallySetRepairAction = true })
                                    Text(action, modifier = Modifier.padding(start = 4.dp))
                                }
                            }
                        }
                    }

                    // New Item
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Replacement Item (New Unit)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            CategoryDropdown(label = "Filter by Category", categories = stockCategories, selected = newCategoryFilter, onSelected = { newCategoryFilter = it })
                            ItemDropdown(label = "Select Replacement Item", items = filteredNewItems, selected = selectedNewItem, onSelected = { selectedNewItem = it })

                            val newItem = selectedNewItem
                            if (newItem != null) {
                                val stock = newItem.stockOnHand ?: 0
                                Text("Available stock: $stock", style = MaterialTheme.typography.bodySmall, color = if (stock > 0) Color(0xFF2E7D32) else Color(0xFFC62828))
                            }

                            OutlinedTextField(
                                value = quantity,
                                onValueChange = { q -> if (q.all { it.isDigit() }) quantity = q },
                                label = { Text("Quantity") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                singleLine = true,
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                            )
                            if (selectedNewItem != null && qty > selectedNewItemStock) {
                                Text(
                                    "Quantity exceeds available stock ($selectedNewItemStock)",
                                    color = MaterialTheme.colorScheme.error,
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }

                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Checkbox(checked = isTemporary, onCheckedChange = { isTemporary = it })
                                Text("Temporary replacement (marks as Resolved (Temporary))", style = MaterialTheme.typography.bodySmall)
                            }
                        }
                    }

                    // Remarks (shown here too for replacement)
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Replacement remarks", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            OutlinedTextField(
                                value = remarks,
                                onValueChange = { remarks = it },
                                placeholder = { Text("Describe the resolution...") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                maxLines = 4
                            )
                        }
                    }
                }

                // Step 2: Preview & Confirm
                if (wizardStep == 2) {
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f))) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                Text("Preview", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                StatusBadge(
                                    text = if (isTemporary) "Resolved (Temporary)" else "Solved",
                                    color = if (isTemporary) Color(0xFF00838F) else Color(0xFF2E7D32)
                                )
                            }
                            HorizontalDivider()

                            if (isReplacement) {
                                val oldText = if (useUnlisted) "$unlistedName ($unlistedModel)" else (selectedOldItem?.displayText ?: "(Unknown)")
                                val newText = selectedNewItem?.displayText ?: "(Unknown)"
                                val condText = selectedCondition?.conditionName ?: "(Unknown)"
                                val actionText = repairAction.ifBlank { "Repaired" }
                                val stockAfter = selectedNewItemStock - qty
                                val oldImpact = if (repairAction == "Unrepaired") "0 (kept out of stock)" else "+$qty"

                                Text("Resolution: ${if (isTemporary) "Temporary Replacement" else "Permanent Replacement"}", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)

                                GroupLabel("Old item:")
                                DetailLine(oldText)
                                DetailLine("Condition: $condText")
                                DetailLine("Action: $actionText")
                                DetailLine("Stock impact: $oldImpact")
                                Spacer(Modifier.height(4.dp))

                                GroupLabel("New item:")
                                DetailLine(newText)
                                DetailLine("Current stock: $selectedNewItemStock")
                                DetailLine("Stock impact: -$qty")
                                DetailLine("Stock after save: $stockAfter")
                                Spacer(Modifier.height(4.dp))

                                GroupLabel("Follow-up:")
                                DetailLine(if (isTemporary) "This ticket remains Resolved (Temporary) until the replacement item is returned." else "No temporary return is required.")
                            } else {
                                Text("Resolution: ${if (isTemporary) "Temporary Service" else "Service Only"}", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold)
                                Text("Status after save: ${if (isTemporary) "Resolved (Temporary)" else "Solved"}", style = MaterialTheme.typography.bodySmall)
                                DetailLine("Inventory impact: None")
                                if (isTemporary) DetailLine("This ticket remains Resolved (Temporary) until the service fix is confirmed permanent.")
                            }

                            if (remarks.isNotBlank()) {
                                Spacer(Modifier.height(4.dp))
                                GroupLabel("Remarks:")
                                DetailLine(remarks)
                            }
                        }
                    }

                    Spacer(Modifier.height(80.dp))
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CategoryDropdown(label: String, categories: List<String>, selected: String, onSelected: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = selected,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            categories.forEach { cat ->
                DropdownMenuItem(text = { Text(cat) }, onClick = { onSelected(cat); expanded = false })
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItemDropdown(label: String, items: List<CallItemLookupDto>, selected: CallItemLookupDto?, onSelected: (CallItemLookupDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.displayText ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            items.forEach { item ->
                DropdownMenuItem(
                    text = {
                        Column {
                            Text(item.displayText, style = MaterialTheme.typography.bodyMedium)
                            if (item.stockOnHand != null) {
                                Text("Stock: ${item.stockOnHand}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    },
                    onClick = { onSelected(item); expanded = false }
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ConditionDropdown(conditions: List<CallConditionDto>, selected: CallConditionDto?, onSelected: (CallConditionDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.conditionName ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text("Condition") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            conditions.forEach { cond ->
                DropdownMenuItem(text = { Text(cond.conditionName) }, onClick = { onSelected(cond); expanded = false })
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun UnitDropdown(value: String, onValueChange: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val units = listOf("Unit", "Piece", "Set", "Box")
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = value,
            onValueChange = {},
            readOnly = true,
            label = { Text("Unit") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            units.forEach { unit ->
                DropdownMenuItem(text = { Text(unit) }, onClick = { onValueChange(unit); expanded = false })
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItemCategoryDropdown(
    categories: List<ItemCategoryDto>,
    selected: ItemCategoryDto?,
    onSelected: (ItemCategoryDto) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.name ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text("Category") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            categories.forEach { cat ->
                DropdownMenuItem(text = { Text(cat.name) }, onClick = { onSelected(cat); expanded = false })
            }
        }
    }
}

@Composable
private fun GroupLabel(text: String) {
    Text(text, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f))
}

@Composable
private fun DetailLine(text: String) {
    Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurface)
}

private fun buildResolutionRequest(
    ticketId: Int,
    isReplacement: Boolean,
    useUnlisted: Boolean,
    selectedOldItem: CallItemLookupDto?,
    unlistedName: String,
    unlistedModel: String,
    unlistedSerial: String,
    unlistedUnit: String,
    unlistedDesc: String,
    unlistedCategory: ItemCategoryDto?,
    selectedCondition: CallConditionDto?,
    conditionRemarks: String,
    repairAction: String,
    selectedNewItem: CallItemLookupDto?,
    quantity: Int,
    isTemporary: Boolean,
    remarks: String
): ResolutionRequest {
    if (!isReplacement) {
        return ResolutionRequest(
            ticketId = ticketId,
            resolutionType = "Service Only",
            remarks = remarks,
            userId = UserSession.currentUser?.userId,
            isTemporary = isTemporary
        )
    }

    return ResolutionRequest(
        ticketId = ticketId,
        resolutionType = "Replacement",
        remarks = remarks,
        userId = UserSession.currentUser?.userId,
        isTemporary = isTemporary,
        useUnlistedOldItem = useUnlisted,
        oldItemId = if (useUnlisted) null else selectedOldItem?.itemId,
        newItemId = selectedNewItem?.itemId,
        quantity = quantity,
        oldItemConditionId = selectedCondition?.conditionId,
        oldItemConditionRemarks = conditionRemarks.ifBlank { null },
        oldItemRepairAction = repairAction,
        unlistedOldItemName = unlistedName.ifBlank { null },
        unlistedOldItemDescription = unlistedDesc.ifBlank { null },
        unlistedOldItemCategoryId = unlistedCategory?.categoryId,
        unlistedOldItemCategoryName = unlistedCategory?.name,
        unlistedOldItemSerialNumber = unlistedSerial.ifBlank { null },
        unlistedOldItemModelNumber = unlistedModel.ifBlank { null },
        unlistedOldItemUnitOfMeasure = unlistedUnit.ifBlank { null }
    )
}
