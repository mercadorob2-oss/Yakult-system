package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
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
import com.example.yakultscanner.api.RepairForwardPreviewDto
import com.example.yakultscanner.api.RepairForwardRequest
import com.example.yakultscanner.ui.components.ItcmChoiceCard
import com.example.yakultscanner.ui.components.ItcmSectionHeading
import com.example.yakultscanner.ui.components.ItcmStatusChip
import com.example.yakultscanner.ui.components.ItcmStepProgress
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.ResolutionUiState
import com.example.yakultscanner.viewmodels.RepairForwardUiState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ItcmResolutionScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val snackbarHostState = remember { SnackbarHostState() }
    val resolutionState by viewModel.resolutionState.collectAsStateWithLifecycle()
    val repairForwardState by viewModel.repairForwardState.collectAsStateWithLifecycle()
    val outItems by viewModel.resolutionOutItems.collectAsStateWithLifecycle()
    val stockItems by viewModel.resolutionStockItems.collectAsStateWithLifecycle()
    val conditions by viewModel.resolutionConditions.collectAsStateWithLifecycle()
    val categories by viewModel.resolutionCategories.collectAsStateWithLifecycle()
    val isLoadingLookups by viewModel.resolutionLoadingLookups.collectAsStateWithLifecycle()

    var wizardStep by rememberSaveable { mutableStateOf(0) }
    var isReplacement by rememberSaveable { mutableStateOf(false) }
    var useUnlisted by rememberSaveable { mutableStateOf(false) }
    var oldCategoryFilter by rememberSaveable { mutableStateOf("All Categories") }
    var newCategoryFilter by rememberSaveable { mutableStateOf("All Categories") }
    var selectedOldItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var selectedNewItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var selectedCondition by remember { mutableStateOf<CallConditionDto?>(null) }
    var unlistedCategory by remember { mutableStateOf<ItemCategoryDto?>(null) }
    var unlistedName by rememberSaveable { mutableStateOf("") }
    var unlistedModel by rememberSaveable { mutableStateOf("") }
    var unlistedSerial by rememberSaveable { mutableStateOf("") }
    var unlistedUnit by rememberSaveable { mutableStateOf("Unit") }
    var unlistedDesc by rememberSaveable { mutableStateOf("") }
    var conditionRemarks by rememberSaveable { mutableStateOf("") }
    var repairAction by rememberSaveable { mutableStateOf("Repaired") }
    var userManuallySetRepairAction by rememberSaveable { mutableStateOf(false) }
    var quantity by rememberSaveable { mutableStateOf("1") }
    var isTemporary by rememberSaveable { mutableStateOf(false) }
    var remarks by rememberSaveable { mutableStateOf("") }

    // IT CALL → Repair Portal state. Repair creation is explicit and happens before the parent
    // IT CALL outcome is selected, matching the desktop workflow.
    var forwardToRepair by rememberSaveable { mutableStateOf(false) }
    var selectedServiceForwardItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var pendingForwardRequest by remember { mutableStateOf<RepairForwardRequest?>(null) }
    var forwardPreview by remember { mutableStateOf<RepairForwardPreviewDto?>(null) }
    var showForwardPreview by remember { mutableStateOf(false) }
    var showForwardOutcome by remember { mutableStateOf(false) }
    var parentOutcome by rememberSaveable { mutableStateOf("Forwarded to Repair") }
    var linkedRepairTicketId by remember { mutableStateOf<Int?>(null) }
    var linkedRepairTicketCode by remember { mutableStateOf<String?>(null) }
    var linkedOldItemId by remember { mutableStateOf<Int?>(null) }

    val outCategories = remember(outItems) {
        listOf("All Categories") + outItems.mapNotNull { it.category?.takeIf(String::isNotBlank) }.distinct().sorted()
    }
    val stockCategories = remember(stockItems) {
        listOf("All Categories") + stockItems.mapNotNull { it.category?.takeIf(String::isNotBlank) }.distinct().sorted()
    }
    val filteredOldItems = remember(outItems, oldCategoryFilter) {
        if (oldCategoryFilter == "All Categories") outItems else outItems.filter { it.category == oldCategoryFilter }
    }
    val filteredNewItems = remember(stockItems, newCategoryFilter) {
        if (newCategoryFilter == "All Categories") stockItems else stockItems.filter { it.category == newCategoryFilter }
    }
    val qty = quantity.toIntOrNull() ?: 1
    val selectedNewItemStock = selectedNewItem?.stockOnHand ?: 0
    val detailValid = if (useUnlisted) {
        unlistedName.isNotBlank() && unlistedModel.isNotBlank() && unlistedCategory != null && selectedCondition != null && selectedNewItem != null && qty in 1..selectedNewItemStock
    } else {
        selectedOldItem != null && selectedNewItem != null && selectedOldItem?.itemId != selectedNewItem?.itemId && selectedCondition != null && qty in 1..selectedNewItemStock
    }
    val forwardItemValid = !forwardToRepair || if (isReplacement) {
        useUnlisted || selectedOldItem != null
    } else {
        selectedServiceForwardItem != null
    }
    val canConfirm = (if (!isReplacement) remarks.isNotBlank() else detailValid && remarks.isNotBlank()) && forwardItemValid
    val isProcessing = resolutionState is ResolutionUiState.Loading ||
        repairForwardState is RepairForwardUiState.PreviewLoading || repairForwardState is RepairForwardUiState.CreateLoading
    val totalSteps = if (isReplacement) 3 else 2
    val displayStep = if (isReplacement) wizardStep + 1 else if (wizardStep == 2) 2 else 1
    val stepLabels = if (isReplacement) listOf("Resolution type", "Replacement details", "Review") else listOf("Resolution type", "Review")

    LaunchedEffect(ticketId) { viewModel.loadResolutionLookups() }
    LaunchedEffect(selectedCondition) {
        if (!userManuallySetRepairAction && selectedCondition?.conditionName?.contains("Damage", ignoreCase = true) == true) {
            repairAction = "Unrepaired"
        }
    }
    LaunchedEffect(resolutionState) {
        when (val state = resolutionState) {
            is ResolutionUiState.Success -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
                viewModel.loadTicketDetail(ticketId)
                val repairTicketId = linkedRepairTicketId
                if (repairTicketId != null) {
                    navController.navigate("repair_ticket_detail/$repairTicketId") {
                        popUpTo("call_ticket_resolve/$ticketId") { inclusive = true }
                    }
                } else {
                    navController.popBackStack()
                }
            }
            is ResolutionUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
            }
            else -> Unit
        }
    }
    LaunchedEffect(repairForwardState) {
        when (val forward = repairForwardState) {
            is RepairForwardUiState.Preview -> {
                forwardPreview = forward.response.preview
                showForwardPreview = true
                viewModel.resetRepairForwardState()
            }
            is RepairForwardUiState.Created -> {
                linkedRepairTicketId = forward.response.ticket?.repairTicketId
                linkedRepairTicketCode = forward.response.ticket?.ticketCode
                linkedOldItemId = forward.response.oldItemId ?: forward.response.ticket?.itemId
                showForwardPreview = false
                showForwardOutcome = true
                viewModel.resetRepairForwardState()
            }
            is RepairForwardUiState.Error -> {
                snackbarHostState.showSnackbar(forward.message)
                viewModel.resetRepairForwardState()
            }
            else -> Unit
        }
    }

    fun goBack() {
        wizardStep = when {
            wizardStep == 0 -> {
                navController.popBackStack()
                0
            }
            !isReplacement && wizardStep == 2 -> 0
            else -> wizardStep - 1
        }
    }

    Scaffold(
        containerColor = ItcmUi.Canvas,
        topBar = {
            TopAppBar(
                title = { Text("Mark as Resolved", color = ItcmUi.Ink, fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = { goBack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ItcmUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ItcmUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) },
        bottomBar = {
            ItcmResolutionActionDock(
                step = wizardStep,
                isReplacement = isReplacement,
                canContinue = when (wizardStep) {
                    0 -> isReplacement || remarks.isNotBlank()
                    1 -> detailValid
                    else -> canConfirm
                },
                isProcessing = isProcessing,
                onBack = { goBack() },
                onNext = {
                    when (wizardStep) {
                        0 -> wizardStep = if (isReplacement) 1 else 2
                        1 -> wizardStep = 2
                        else -> {
                            if (forwardToRepair) {
                                val forwardRequest = buildRepairForwardRequest(
                                    ticketId = ticketId,
                                    isReplacement = isReplacement,
                                    useUnlisted = useUnlisted,
                                    selectedOldItem = selectedOldItem,
                                    selectedServiceItem = selectedServiceForwardItem,
                                    unlistedName = unlistedName,
                                    unlistedModel = unlistedModel,
                                    unlistedSerial = unlistedSerial,
                                    unlistedUnit = unlistedUnit,
                                    unlistedDesc = unlistedDesc,
                                    unlistedCategory = unlistedCategory,
                                    selectedCondition = selectedCondition,
                                    conditionRemarks = conditionRemarks,
                                    remarks = remarks
                                )
                                pendingForwardRequest = forwardRequest
                                viewModel.previewRepairForward(forwardRequest)
                            } else {
                                viewModel.applyResolution(
                                    buildItcmResolutionRequest(
                                        ticketId = ticketId,
                                        isReplacement = isReplacement,
                                        useUnlisted = useUnlisted,
                                        selectedOldItem = selectedOldItem,
                                        unlistedName = unlistedName,
                                        unlistedModel = unlistedModel,
                                        unlistedSerial = unlistedSerial,
                                        unlistedUnit = unlistedUnit,
                                        unlistedDesc = unlistedDesc,
                                        unlistedCategory = unlistedCategory,
                                        selectedCondition = selectedCondition,
                                        conditionRemarks = conditionRemarks,
                                        repairAction = repairAction,
                                        selectedNewItem = selectedNewItem,
                                        quantity = qty,
                                        isTemporary = isTemporary,
                                        remarks = remarks
                                    )
                                )
                            }
                        }
                    }
                }
            )
        }
    ) { padding ->
        if (isLoadingLookups) {
            Box(Modifier.fillMaxSize().padding(padding).background(ItcmUi.Canvas), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ItcmUi.Canvas)
                    .padding(padding)
                    .padding(horizontal = 16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Spacer(Modifier.height(4.dp))
                ItcmStepProgress(
                    currentStep = displayStep,
                    totalSteps = totalSteps,
                    labels = stepLabels,
                    modifier = Modifier.fillMaxWidth()
                )
                if (wizardStep == 0) {
                    ItcmResolutionTypeStep(
                        isReplacement = isReplacement,
                        remarks = remarks,
                        onReplacementChanged = { isReplacement = it },
                        onRemarksChanged = { remarks = it },
                        isTemporary = isTemporary,
                        onTemporaryChanged = { isTemporary = it }
                    )
                }
                if (wizardStep == 1 && isReplacement) {
                    ItcmReplacementDetailsStep(
                        useUnlisted = useUnlisted,
                        onUseUnlistedChanged = { useUnlisted = it },
                        oldCategoryFilter = oldCategoryFilter,
                        onOldCategoryFilterChanged = { oldCategoryFilter = it },
                        outCategories = outCategories,
                        filteredOldItems = filteredOldItems,
                        selectedOldItem = selectedOldItem,
                        onOldItemSelected = { selectedOldItem = it },
                        unlistedName = unlistedName,
                        onUnlistedNameChanged = { unlistedName = it },
                        unlistedModel = unlistedModel,
                        onUnlistedModelChanged = { unlistedModel = it },
                        unlistedSerial = unlistedSerial,
                        onUnlistedSerialChanged = { unlistedSerial = it },
                        unlistedUnit = unlistedUnit,
                        onUnlistedUnitChanged = { unlistedUnit = it },
                        unlistedDesc = unlistedDesc,
                        onUnlistedDescChanged = { unlistedDesc = it },
                        categories = categories,
                        selectedUnlistedCategory = unlistedCategory,
                        onUnlistedCategorySelected = { unlistedCategory = it },
                        conditions = conditions,
                        selectedCondition = selectedCondition,
                        onConditionSelected = { selectedCondition = it },
                        conditionRemarks = conditionRemarks,
                        onConditionRemarksChanged = { conditionRemarks = it },
                        repairAction = repairAction,
                        onRepairActionSelected = {
                            repairAction = it
                            userManuallySetRepairAction = true
                        },
                        stockCategories = stockCategories,
                        newCategoryFilter = newCategoryFilter,
                        onNewCategoryFilterChanged = { newCategoryFilter = it },
                        filteredNewItems = filteredNewItems,
                        selectedNewItem = selectedNewItem,
                        onNewItemSelected = { selectedNewItem = it },
                        quantity = quantity,
                        onQuantityChanged = { value -> if (value.all(Char::isDigit)) quantity = value },
                        isTemporary = isTemporary,
                        onTemporaryChanged = { isTemporary = it },
                        remarks = remarks,
                        onRemarksChanged = { remarks = it },
                        stockOnHand = selectedNewItemStock,
                        selectedQty = qty
                    )
                }
                if (wizardStep == 2) {
                    ItcmResolutionReviewStep(
                        isReplacement = isReplacement,
                        useUnlisted = useUnlisted,
                        selectedOldItem = selectedOldItem,
                        unlistedName = unlistedName,
                        unlistedModel = unlistedModel,
                        selectedCondition = selectedCondition,
                        repairAction = repairAction,
                        selectedNewItem = selectedNewItem,
                        quantity = qty,
                        stockOnHand = selectedNewItemStock,
                        isTemporary = isTemporary,
                        remarks = remarks
                    )
                    ItcmRepairForwardStep(
                        forwardToRepair = forwardToRepair,
                        onForwardChanged = { forwardToRepair = it },
                        isReplacement = isReplacement,
                        useUnlisted = useUnlisted,
                        selectedOldItem = selectedOldItem,
                        selectedServiceItem = selectedServiceForwardItem,
                        serviceItems = filteredOldItems,
                        onServiceItemSelected = { selectedServiceForwardItem = it }
                    )
                }
                Spacer(Modifier.height(14.dp))
            }
        }
    }

    if (showForwardPreview && forwardPreview != null) {
        val preview = forwardPreview!!
        AlertDialog(
            onDismissRequest = { showForwardPreview = false },
            title = { Text("Review Repair Ticket") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("${preview.callTicketCode ?: "IT Call"} will create or retrieve one permanently linked Repair Ticket.")
                    Text("Item: ${preview.repairItem?.name ?: "Selected item"}", fontWeight = FontWeight.Bold)
                    preview.repairItem?.modelNumber?.let { Text("Model: $it", style = MaterialTheme.typography.bodySmall) }
                    preview.repairItem?.serialNumber?.let { Text("Serial: $it", style = MaterialTheme.typography.bodySmall) }
                    Text("Priority: ${preview.priority ?: "Medium"}")
                    Text("Problem", fontWeight = FontWeight.Bold)
                    Text(preview.problem ?: "", style = MaterialTheme.typography.bodySmall)
                    Text("The parent IT Call is not changed until you choose its outcome on the next step.", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                }
            },
            confirmButton = {
                Button(onClick = { pendingForwardRequest?.let(viewModel::createRepairForward) }, enabled = pendingForwardRequest != null) {
                    Text("Create linked ticket")
                }
            },
            dismissButton = { TextButton(onClick = { showForwardPreview = false }) { Text("Back") } }
        )
    }
    if (showForwardOutcome && linkedRepairTicketId != null) {
        AlertDialog(
            onDismissRequest = { showForwardOutcome = false },
            title = { Text("Choose IT Call outcome") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Repair Ticket ${linkedRepairTicketCode ?: linkedRepairTicketId} is permanently linked. Choose the parent IT Call outcome.")
                    listOf(
                        "Forwarded to Repair" to "Keep the support case active while Repair assesses the item.",
                        "Solved" to "Close the support case after forwarding.",
                        "Resolved (Temporary)" to "Mark a temporary resolution while repair work continues."
                    ).forEach { (outcome, description) ->
                        ItcmChoiceCard(
                            selected = parentOutcome == outcome,
                            title = outcome,
                            description = description,
                            onClick = { parentOutcome = outcome }
                        )
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    showForwardOutcome = false
                    viewModel.applyResolution(
                        buildItcmResolutionRequest(
                            ticketId = ticketId,
                            isReplacement = isReplacement,
                            useUnlisted = useUnlisted,
                            selectedOldItem = selectedOldItem,
                            unlistedName = unlistedName,
                            unlistedModel = unlistedModel,
                            unlistedSerial = unlistedSerial,
                            unlistedUnit = unlistedUnit,
                            unlistedDesc = unlistedDesc,
                            unlistedCategory = unlistedCategory,
                            selectedCondition = selectedCondition,
                            conditionRemarks = conditionRemarks,
                            repairAction = repairAction,
                            selectedNewItem = selectedNewItem,
                            quantity = qty,
                            isTemporary = isTemporary,
                            remarks = remarks,
                            forwardedRepairTicketId = linkedRepairTicketId,
                            forwardedOldItemId = linkedOldItemId,
                            parentOutcome = parentOutcome
                        )
                    )
                }) { Text("Save outcome") }
            },
            dismissButton = { TextButton(onClick = { showForwardOutcome = false }) { Text("Keep IT Call active") } }
        )
    }
}

@Composable
private fun ItcmResolutionTypeStep(
    isReplacement: Boolean,
    remarks: String,
    onReplacementChanged: (Boolean) -> Unit,
    onRemarksChanged: (String) -> Unit,
    isTemporary: Boolean,
    onTemporaryChanged: (Boolean) -> Unit
) {
    ItcmSectionHeading("Choose a resolution path", "The workflow adapts to the type of work completed.")
    ItcmChoiceCard(
        selected = !isReplacement,
        title = "Service only",
        description = "Resolve the ticket without issuing a replacement item.",
        onClick = { onReplacementChanged(false) }
    )
    ItcmChoiceCard(
        selected = isReplacement,
        title = "Replacement",
        description = "Record the old item, condition, replacement stock, and follow-up.",
        onClick = { onReplacementChanged(true) }
    )
    if (!isReplacement) {
        ItcmSurfaceCard {
            Row(modifier = Modifier.fillMaxWidth().clickable { onTemporaryChanged(!isTemporary) }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                Checkbox(checked = isTemporary, onCheckedChange = onTemporaryChanged)
                Text("Temporary service (marks as Resolved (Temporary))", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink)
            }
        }
    }
    ItcmSurfaceCard {
        ItcmSectionHeading(if (isReplacement) "Replacement remarks" else "Service notes", "Required before the resolution can be confirmed.")
        OutlinedTextField(
            value = remarks,
            onValueChange = onRemarksChanged,
            modifier = Modifier.fillMaxWidth(),
            minLines = 4,
            maxLines = 6,
            placeholder = { Text("Describe the resolution and work performed…") },
            shape = RoundedCornerShape(16.dp)
        )
    }
}

@Composable
private fun ItcmReplacementDetailsStep(
    useUnlisted: Boolean,
    onUseUnlistedChanged: (Boolean) -> Unit,
    oldCategoryFilter: String,
    onOldCategoryFilterChanged: (String) -> Unit,
    outCategories: List<String>,
    filteredOldItems: List<CallItemLookupDto>,
    selectedOldItem: CallItemLookupDto?,
    onOldItemSelected: (CallItemLookupDto) -> Unit,
    unlistedName: String,
    onUnlistedNameChanged: (String) -> Unit,
    unlistedModel: String,
    onUnlistedModelChanged: (String) -> Unit,
    unlistedSerial: String,
    onUnlistedSerialChanged: (String) -> Unit,
    unlistedUnit: String,
    onUnlistedUnitChanged: (String) -> Unit,
    unlistedDesc: String,
    onUnlistedDescChanged: (String) -> Unit,
    categories: List<ItemCategoryDto>,
    selectedUnlistedCategory: ItemCategoryDto?,
    onUnlistedCategorySelected: (ItemCategoryDto) -> Unit,
    conditions: List<CallConditionDto>,
    selectedCondition: CallConditionDto?,
    onConditionSelected: (CallConditionDto) -> Unit,
    conditionRemarks: String,
    onConditionRemarksChanged: (String) -> Unit,
    repairAction: String,
    onRepairActionSelected: (String) -> Unit,
    stockCategories: List<String>,
    newCategoryFilter: String,
    onNewCategoryFilterChanged: (String) -> Unit,
    filteredNewItems: List<CallItemLookupDto>,
    selectedNewItem: CallItemLookupDto?,
    onNewItemSelected: (CallItemLookupDto) -> Unit,
    quantity: String,
    onQuantityChanged: (String) -> Unit,
    isTemporary: Boolean,
    onTemporaryChanged: (Boolean) -> Unit,
    remarks: String,
    onRemarksChanged: (String) -> Unit,
    stockOnHand: Int,
    selectedQty: Int
) {
    ItcmSectionHeading("Replacement details", "Record inventory movement and the old item’s next state.")
    ItcmSurfaceCard {
        ItcmSectionHeading("Old item", "Identify the unit being pulled out.")
        ItcmChoiceCard(
            selected = !useUnlisted,
            title = "Listed in inventory",
            description = "Select an existing item recorded in inventory.",
            onClick = { onUseUnlistedChanged(false) }
        )
        ItcmChoiceCard(
            selected = useUnlisted,
            title = "Not listed",
            description = "Provide the available details for an unregistered item.",
            onClick = { onUseUnlistedChanged(true) }
        )
        if (useUnlisted) {
            ItcmTextInput("Item name *", unlistedName, onUnlistedNameChanged)
            ItcmTextInput("Model *", unlistedModel, onUnlistedModelChanged)
            ItcmTextInput("Serial number", unlistedSerial, onUnlistedSerialChanged)
            ItcmValueDropdown("Unit", unlistedUnit, listOf("Unit", "Piece", "Set", "Box"), onUnlistedUnitChanged)
            ItcmTextInput("Description", unlistedDesc, onUnlistedDescChanged, minLines = 2)
            ItcmItemCategoryDropdown("Category *", categories, selectedUnlistedCategory, onUnlistedCategorySelected)
        } else {
            ItcmValueDropdown("Filter by category", oldCategoryFilter, outCategories, onOldCategoryFilterChanged)
            ItcmInventoryItemDropdown("Select old item *", filteredOldItems, selectedOldItem, onOldItemSelected)
        }
    }
    ItcmSurfaceCard {
        ItcmSectionHeading("Condition and pullout action")
        ItcmConditionDropdown("Condition *", conditions, selectedCondition, onConditionSelected)
        ItcmTextInput("Condition remarks", conditionRemarks, onConditionRemarksChanged, minLines = 2)
        listOf("Repaired", "Unrepaired", "Repaired - Spare inventory").forEach { action ->
            ItcmSelectionRow(action, selected = repairAction == action, onClick = { onRepairActionSelected(action) })
        }
    }
    ItcmSurfaceCard {
        ItcmSectionHeading("Replacement item", "Select stock that will be issued to the requester.")
        ItcmValueDropdown("Filter by category", newCategoryFilter, stockCategories, onNewCategoryFilterChanged)
        ItcmInventoryItemDropdown("Select replacement item *", filteredNewItems, selectedNewItem, onNewItemSelected)
        if (selectedNewItem != null) {
            Text(
                text = "Available stock: $stockOnHand",
                style = MaterialTheme.typography.bodySmall,
                color = if (stockOnHand > 0) ItcmUi.Resolved else ItcmUi.Critical,
                fontWeight = FontWeight.Bold
            )
        }
        OutlinedTextField(
            value = quantity,
            onValueChange = onQuantityChanged,
            label = { Text("Quantity *") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            isError = selectedNewItem != null && selectedQty > stockOnHand,
            shape = RoundedCornerShape(16.dp)
        )
        if (selectedNewItem != null && selectedQty > stockOnHand) {
            Text("Quantity exceeds available stock ($stockOnHand).", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Critical)
        }
        Row(modifier = Modifier.fillMaxWidth().clickable { onTemporaryChanged(!isTemporary) }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
            Checkbox(checked = isTemporary, onCheckedChange = onTemporaryChanged)
            Text("Temporary replacement", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink)
        }
        if (isTemporary) Text("The ticket will remain Resolved (Temporary) until follow-up is completed.", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
    }
    ItcmSurfaceCard {
        ItcmSectionHeading("Resolution remarks", "Required before confirmation.")
        OutlinedTextField(
            value = remarks,
            onValueChange = onRemarksChanged,
            modifier = Modifier.fillMaxWidth(),
            minLines = 4,
            maxLines = 6,
            placeholder = { Text("Summarize the replacement and outcome…") },
            shape = RoundedCornerShape(16.dp)
        )
    }
}

@Composable
private fun ItcmResolutionReviewStep(
    isReplacement: Boolean,
    useUnlisted: Boolean,
    selectedOldItem: CallItemLookupDto?,
    unlistedName: String,
    unlistedModel: String,
    selectedCondition: CallConditionDto?,
    repairAction: String,
    selectedNewItem: CallItemLookupDto?,
    quantity: Int,
    stockOnHand: Int,
    isTemporary: Boolean,
    remarks: String
) {
    ItcmSectionHeading("Review resolution", "Confirm the impact before saving this final ticket update.")
    ItcmSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text("Outcome", style = MaterialTheme.typography.titleSmall, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
            ItcmStatusChip(if (isTemporary) "Resolved (Temporary)" else "Solved")
        }
        HorizontalDivider(color = ItcmUi.Divider)
        if (!isReplacement) {
            ItcmReviewLine("Resolution", if (isTemporary) "Temporary service" else "Service only")
            ItcmReviewLine("Status after save", if (isTemporary) "Resolved (Temporary)" else "Solved")
            ItcmReviewLine("Inventory impact", "None")
        } else {
            val oldItemText = if (useUnlisted) "$unlistedName ($unlistedModel)" else selectedOldItem?.displayText.orEmpty()
            ItcmReviewLine("Old item", oldItemText.ifBlank { "Not selected" })
            ItcmReviewLine("Condition", selectedCondition?.conditionName ?: "Not selected")
            ItcmReviewLine("Old item action", repairAction)
            ItcmReviewLine("Replacement item", selectedNewItem?.displayText ?: "Not selected")
            ItcmReviewLine("Quantity", quantity.toString())
            ItcmReviewLine("Stock after", (stockOnHand - quantity).toString())
            ItcmReviewLine("Follow-up", if (isTemporary) "Temporary replacement" else "Permanent replacement")
        }
        HorizontalDivider(color = ItcmUi.Divider)
        ItcmReviewLine("Remarks", remarks.ifBlank { "Required" })
    }
}

@Composable
private fun ItcmRepairForwardStep(
    forwardToRepair: Boolean,
    onForwardChanged: (Boolean) -> Unit,
    isReplacement: Boolean,
    useUnlisted: Boolean,
    selectedOldItem: CallItemLookupDto?,
    selectedServiceItem: CallItemLookupDto?,
    serviceItems: List<CallItemLookupDto>,
    onServiceItemSelected: (CallItemLookupDto) -> Unit
) {
    ItcmSurfaceCard {
        ItcmSectionHeading("Forward to Repair Portal", "Optional: create a shared Repair Ticket for the physical unit after this resolution.")
        Row(
            modifier = Modifier.fillMaxWidth().clickable { onForwardChanged(!forwardToRepair) }.padding(vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Checkbox(checked = forwardToRepair, onCheckedChange = onForwardChanged)
            Column {
                Text("Forward affected item to Repair", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
                Text("The Repair Ticket is reviewed and created before the parent IT Call outcome is saved.", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
            }
        }
        if (forwardToRepair) {
            if (isReplacement) {
                val itemLabel = if (useUnlisted) "Unlisted old item (it will be registered before linking)" else selectedOldItem?.displayText ?: "Select the old item above"
                Text("Forwarded physical item: $itemLabel", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink)
            } else {
                Text("Select the physical item that needs repair.", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                ItcmInventoryItemDropdown("Item to forward *", serviceItems, selectedServiceItem, onServiceItemSelected)
            }
        }
    }
}

@Composable
private fun ItcmResolutionActionDock(
    step: Int,
    isReplacement: Boolean,
    canContinue: Boolean,
    isProcessing: Boolean,
    onBack: () -> Unit,
    onNext: () -> Unit
) {
    val label = when (step) {
        0 -> if (isReplacement) "Next" else "Review resolution"
        1 -> "Review resolution"
        else -> "Confirm resolution"
    }
    Column(
        modifier = Modifier.fillMaxWidth().background(ItcmUi.Surface).padding(start = 16.dp, end = 16.dp, top = 10.dp, bottom = 8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            OutlinedButton(onClick = onBack, modifier = Modifier.height(52.dp), shape = RoundedCornerShape(16.dp)) { Text(if (step == 0) "Cancel" else "Back") }
            Button(
                onClick = onNext,
                enabled = canContinue && !isProcessing,
                modifier = Modifier.weight(1f).height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) {
                if (isProcessing) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = Color.White, strokeWidth = 2.dp)
                else Text(label, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun ItcmTextInput(label: String, value: String, onValueChange: (String) -> Unit, minLines: Int = 1) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        modifier = Modifier.fillMaxWidth(),
        minLines = minLines,
        maxLines = if (minLines > 1) 4 else 1,
        shape = RoundedCornerShape(16.dp)
    )
}

@Composable
private fun ItcmSelectionRow(label: String, selected: Boolean, onClick: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(if (selected) ItcmUi.BrandSoft else ItcmUi.SurfaceSubtle).clickable(onClick = onClick).padding(12.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Box(modifier = Modifier.size(18.dp).clip(RoundedCornerShape(99.dp)).background(if (selected) ItcmUi.Brand else ItcmUi.Divider), contentAlignment = Alignment.Center) {
            if (selected) Box(modifier = Modifier.size(7.dp).clip(RoundedCornerShape(99.dp)).background(Color.White))
        }
        Text(label, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal)
    }
}

@Composable
private fun ItcmValueDropdown(label: String, value: String, values: List<String>, onSelected: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = value,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            modifier = Modifier.fillMaxWidth(),
            trailingIcon = { IconButton(onClick = { expanded = true }) { Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select $label") } },
            shape = RoundedCornerShape(16.dp)
        )
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }, modifier = Modifier.heightIn(max = 280.dp)) {
            values.forEach { item -> DropdownMenuItem(text = { Text(item) }, onClick = { onSelected(item); expanded = false }) }
        }
    }
}

@Composable
private fun ItcmInventoryItemDropdown(label: String, items: List<CallItemLookupDto>, selected: CallItemLookupDto?, onSelected: (CallItemLookupDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selected?.displayText.orEmpty(),
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            modifier = Modifier.fillMaxWidth(),
            trailingIcon = { IconButton(onClick = { expanded = true }) { Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select item") } },
            shape = RoundedCornerShape(16.dp)
        )
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }, modifier = Modifier.heightIn(max = 320.dp)) {
            if (items.isEmpty()) DropdownMenuItem(text = { Text("No items available") }, onClick = { expanded = false })
            items.forEach { item ->
                DropdownMenuItem(
                    text = {
                        Column {
                            Text(item.displayText, style = MaterialTheme.typography.bodyMedium)
                            item.stockOnHand?.let { Text("Stock: $it", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
                        }
                    },
                    onClick = { onSelected(item); expanded = false }
                )
            }
        }
    }
}

@Composable
private fun ItcmConditionDropdown(label: String, conditions: List<CallConditionDto>, selected: CallConditionDto?, onSelected: (CallConditionDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selected?.conditionName.orEmpty(),
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            modifier = Modifier.fillMaxWidth(),
            trailingIcon = { IconButton(onClick = { expanded = true }) { Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select condition") } },
            shape = RoundedCornerShape(16.dp)
        )
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            conditions.forEach { condition -> DropdownMenuItem(text = { Text(condition.conditionName) }, onClick = { onSelected(condition); expanded = false }) }
        }
    }
}

@Composable
private fun ItcmItemCategoryDropdown(label: String, categories: List<ItemCategoryDto>, selected: ItemCategoryDto?, onSelected: (ItemCategoryDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = Modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selected?.name.orEmpty(),
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            modifier = Modifier.fillMaxWidth(),
            trailingIcon = { IconButton(onClick = { expanded = true }) { Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select category") } },
            shape = RoundedCornerShape(16.dp)
        )
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }, modifier = Modifier.heightIn(max = 280.dp)) {
            categories.forEach { category -> DropdownMenuItem(text = { Text(category.name) }, onClick = { onSelected(category); expanded = false }) }
        }
    }
}

@Composable
private fun ItcmReviewLine(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted, modifier = Modifier.weight(0.42f))
        Text(value, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(0.58f))
    }
}

private fun buildItcmResolutionRequest(
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
    remarks: String,
    forwardedRepairTicketId: Int? = null,
    forwardedOldItemId: Int? = null,
    parentOutcome: String? = null
): ResolutionRequest {
    if (!isReplacement) {
        return ResolutionRequest(
            ticketId = ticketId,
            resolutionType = "Service Only",
            remarks = remarks,
            userId = UserSession.currentUser?.userId,
            isTemporary = isTemporary,
            forwardedRepairTicketId = forwardedRepairTicketId,
            forwardedOldItemId = forwardedOldItemId,
            parentOutcome = parentOutcome
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
        unlistedOldItemUnitOfMeasure = unlistedUnit.ifBlank { null },
        forwardedRepairTicketId = forwardedRepairTicketId,
        forwardedOldItemId = forwardedOldItemId,
        parentOutcome = parentOutcome
    )
}

private fun buildRepairForwardRequest(
    ticketId: Int,
    isReplacement: Boolean,
    useUnlisted: Boolean,
    selectedOldItem: CallItemLookupDto?,
    selectedServiceItem: CallItemLookupDto?,
    unlistedName: String,
    unlistedModel: String,
    unlistedSerial: String,
    unlistedUnit: String,
    unlistedDesc: String,
    unlistedCategory: ItemCategoryDto?,
    selectedCondition: CallConditionDto?,
    conditionRemarks: String,
    remarks: String
): RepairForwardRequest {
    val repairItemId = if (isReplacement) selectedOldItem?.itemId else selectedServiceItem?.itemId
    return RepairForwardRequest(
        ticketId = ticketId,
        resolutionType = if (isReplacement) "Replacement" else "Service Only",
        remarks = remarks.ifBlank { null },
        repairItemId = if (useUnlisted && isReplacement) null else repairItemId,
        oldItemId = if (isReplacement && !useUnlisted) selectedOldItem?.itemId else null,
        useUnlistedOldItem = isReplacement && useUnlisted,
        unlistedOldItemName = unlistedName.ifBlank { null },
        unlistedOldItemDescription = unlistedDesc.ifBlank { null },
        unlistedOldItemCategoryId = unlistedCategory?.categoryId,
        unlistedOldItemCategoryName = unlistedCategory?.name,
        unlistedOldItemSerialNumber = unlistedSerial.ifBlank { null },
        unlistedOldItemModelNumber = unlistedModel.ifBlank { null },
        unlistedOldItemUnitOfMeasure = unlistedUnit.ifBlank { null },
        oldItemConditionId = selectedCondition?.conditionId,
        oldItemConditionRemarks = conditionRemarks.ifBlank { null }
    )
}
