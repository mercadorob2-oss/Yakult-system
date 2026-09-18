package com.example.yakultscanner.ui.screens

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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallLookupItem
import com.example.yakultscanner.api.CreateTicketRequest
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.CreateTicketUiState
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CreateTicketScreen(
    navController: NavController,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val createState by viewModel.createState.collectAsState()
    val scrollState = rememberScrollState()

    val companies by viewModel.companies.collectAsState()
    val departments by viewModel.departments.collectAsState()
    val branches by viewModel.branches.collectAsState()
    val callerEmployees by viewModel.callerEmployees.collectAsState()
    val itEmployees by viewModel.itEmployees.collectAsState()
    val escalationSettings by viewModel.escalationSettings.collectAsState()

    var selectedCompany by remember { mutableStateOf<CallLookupItem?>(null) }
    var selectedDepartment by remember { mutableStateOf<CallLookupItem?>(null) }
    var selectedBranch by remember { mutableStateOf<CallLookupItem?>(null) }
    var selectedCaller by remember { mutableStateOf<CallLookupItem?>(null) }
    var selectedAssignee by remember { mutableStateOf<CallLookupItem?>(null) }
    var contactEmail by remember { mutableStateOf("") }

    var issue by remember { mutableStateOf("") }
    var providedSolution by remember { mutableStateOf("") }
    var priority by remember { mutableStateOf("Medium") }
    var issueType by remember { mutableStateOf("Hardware") }

    val escalationDefaultSup = escalationSettings?.daysToSupervisor ?: 2
    val escalationDefaultMgr = escalationSettings?.daysToManager ?: 3
    var escalationEnabled by remember { mutableStateOf(false) }
    var escalationDaysSup by remember { mutableIntStateOf(2) }
    var escalationDaysMgr by remember { mutableIntStateOf(3) }
    var escalationReason by remember { mutableStateOf("") }
    var reviewRequest by remember { mutableStateOf<CreateTicketRequest?>(null) }
    var reviewDaysToSupervisor by remember { mutableStateOf<Int?>(null) }
    var reviewDaysToManager by remember { mutableStateOf<Int?>(null) }
    var reviewReason by remember { mutableStateOf<String?>(null) }

    val priorities = listOf("Critical", "High", "Medium", "Low")
    val issueTypes = listOf("Hardware", "Software", "Network", "Account", "Peripheral", "Setup", "Other")

    LaunchedEffect(Unit) {
        viewModel.loadCompanies()
        viewModel.loadItEmployees()
        viewModel.loadEscalationSettings()
    }

    LaunchedEffect(escalationSettings) {
        escalationSettings?.let { s ->
            if (!escalationEnabled) {
                escalationDaysSup = s.daysToSupervisor
                escalationDaysMgr = s.daysToManager
            }
        }
    }

    LaunchedEffect(selectedCompany) {
        selectedDepartment = null
        selectedBranch = null
        selectedCaller = null
        viewModel.clearCallerEmployees()
        viewModel.loadDepartments(selectedCompany?.id)
        viewModel.loadBranches(selectedCompany?.id, null)
    }

    LaunchedEffect(selectedDepartment, selectedBranch) {
        selectedCaller = null
        viewModel.clearCallerEmployees()
        val deptId = selectedDepartment?.id
        if (deptId != null && deptId > 0) {
            viewModel.loadCallerEmployees(deptId, selectedCompany?.id, selectedBranch?.id)
        }
    }

    fun isValid(): Boolean {
        if (selectedCompany == null || selectedCaller == null || issue.isBlank()) return false
        if (escalationEnabled && escalationReason.isBlank()) return false
        return true
    }

    LaunchedEffect(createState) {
        when (val state = createState) {
            is CreateTicketUiState.Success -> {
                val msg = buildString {
                    append("Ticket created: ${state.ticket?.ticketCode ?: ""}")
                    if (state.escalationWarning != null) append(" — ${state.escalationWarning}")
                }
                snackbarHostState.showSnackbar(msg)
                viewModel.resetCreateState()
                navController.popBackStack()
            }
            is CreateTicketUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetCreateState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Create IT Ticket") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    titleContentColor = MaterialTheme.colorScheme.onPrimaryContainer
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 16.dp)
                .verticalScroll(scrollState),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Spacer(Modifier.height(12.dp))

            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.55f))
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("CALL IT TICKET", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onPrimaryContainer)
                    Text(
                        "For onsite IT support. This ticket will be recorded in ITCM for follow-up.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onPrimaryContainer
                    )
                }
            }

            // Caller Details — cascading dropdowns
            FormSectionCard(title = "Caller Details") {
                LookupDropdown(
                    label = "Company *",
                    selected = selectedCompany,
                    items = companies,
                    onSelect = { selectedCompany = it }
                )
                LookupDropdown(
                    label = "Department",
                    selected = selectedDepartment,
                    items = departments,
                    enabled = companies.isNotEmpty(),
                    onSelect = { selectedDepartment = it }
                )
                LookupDropdown(
                    label = "Branch",
                    selected = selectedBranch,
                    items = branches,
                    enabled = departments.isNotEmpty(),
                    onSelect = { selectedBranch = it }
                )
                LookupDropdown(
                    label = "Caller Name *",
                    selected = selectedCaller,
                    items = callerEmployees,
                    enabled = selectedDepartment != null,
                    placeholder = if (selectedDepartment == null) "Select a department first" else "Select caller",
                    onSelect = { selectedCaller = it }
                )
                OutlinedTextField(
                    value = contactEmail,
                    onValueChange = { contactEmail = it },
                    label = { Text("Contact Email (for status updates)") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 1,
                    singleLine = true
                )
            }

            // Issue Details
            FormSectionCard(title = "Issue Details") {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StaticDropdown(
                        label = "Priority *",
                        value = priority,
                        options = priorities,
                        onSelect = { priority = it },
                        modifier = Modifier.weight(1f)
                    )
                    StaticDropdown(
                        label = "Issue Type *",
                        value = issueType,
                        options = issueTypes,
                        onSelect = { issueType = it },
                        modifier = Modifier.weight(1f)
                    )
                }
                OutlinedTextField(
                    value = issue,
                    onValueChange = { issue = it },
                    label = { Text("Technical Problem / Issue *") },
                    modifier = Modifier.fillMaxWidth().height(120.dp),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 5
                )
                OutlinedTextField(
                    value = providedSolution,
                    onValueChange = { providedSolution = it },
                    label = { Text("Initial Solution / Notes (optional)") },
                    modifier = Modifier.fillMaxWidth().height(100.dp),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 4
                )
            }

            // Escalation Override (optional)
            FormSectionCard(title = "Escalation Override") {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text("Set custom escalation timing", style = MaterialTheme.typography.bodyMedium)
                        Text(
                            "Defaults: Supervisor ${escalationDefaultSup}d · Manager ${escalationDefaultMgr}d",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = escalationEnabled,
                        onCheckedChange = { enabled ->
                            escalationEnabled = enabled
                            if (enabled) {
                                escalationDaysSup = escalationDefaultSup
                                escalationDaysMgr = escalationDefaultMgr
                            }
                        }
                    )
                }

                if (escalationEnabled) {
                    DaysStepper(
                        label = "Supervisor escalation (days)",
                        value = escalationDaysSup,
                        min = escalationDefaultSup,
                        onValueChange = {
                            escalationDaysSup = it
                            if (escalationDaysMgr < it) escalationDaysMgr = it
                        }
                    )
                    DaysStepper(
                        label = "Manager escalation (days)",
                        value = escalationDaysMgr,
                        min = maxOf(escalationDefaultMgr, escalationDaysSup),
                        onValueChange = { escalationDaysMgr = it }
                    )
                    OutlinedTextField(
                        value = escalationReason,
                        onValueChange = { escalationReason = it },
                        label = { Text("Reason for override (required)") },
                        modifier = Modifier.fillMaxWidth().height(90.dp),
                        shape = RoundedCornerShape(12.dp),
                        maxLines = 3,
                        isError = escalationReason.isBlank()
                    )
                    if (escalationReason.isBlank()) {
                        Text(
                            "Reason is required when override is enabled",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                }
            }

            // Assignment — IT employees only
            FormSectionCard(title = "Assignment") {
                LookupDropdown(
                    label = "Assign To IT Staff (optional)",
                    selected = selectedAssignee,
                    items = itEmployees,
                    placeholder = "Leave blank — unassigned",
                    allowClear = true,
                    onSelect = { selectedAssignee = it }
                )
            }

            Spacer(Modifier.height(8.dp))
            val isSubmitting = createState is CreateTicketUiState.Loading
            Button(
                onClick = {
                    if (!isValid()) {
                        scope.launch {
                            snackbarHostState.showSnackbar("Company, Caller Name and Issue are required")
                        }
                        return@Button
                    }
                    val userId = UserSession.currentUser?.userId?.takeIf { it > 0 }
                    val request = CreateTicketRequest(
                        company = selectedCompany!!.name,
                        callerName = selectedCaller!!.name,
                        contactEmail = contactEmail.trim().ifBlank { null },
                        issue = issue,
                        comId = selectedCompany?.id,
                        deptId = selectedDepartment?.id,
                        branchId = selectedBranch?.id,
                        providedSolution = providedSolution.ifBlank { null },
                        issueType = issueType,
                        priority = priority,
                        assignedToEmpId = selectedAssignee?.id?.takeIf { it > 0 },
                        createdByUserId = userId,
                        ticketSource = "CallIT"
                    )
                    reviewDaysToSupervisor = if (escalationEnabled) escalationDaysSup else null
                    reviewDaysToManager = if (escalationEnabled) escalationDaysMgr else null
                    reviewReason = if (escalationEnabled) escalationReason.trim() else null
                    reviewRequest = request
                },
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(14.dp),
                enabled = !isSubmitting
            ) {
                if (isSubmitting) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp
                    )
                } else {
                    Icon(Icons.Filled.Check, contentDescription = null, modifier = Modifier.size(20.dp))
                    Spacer(Modifier.width(8.dp))
                    Text("Create Ticket", fontWeight = FontWeight.Bold)
                }
            }

            TextButton(
                onClick = { navController.popBackStack() },
                modifier = Modifier.fillMaxWidth()
            ) { Text("Cancel") }

            Spacer(Modifier.height(80.dp))
        }
    }

    reviewRequest?.let { request ->
        AlertDialog(
            onDismissRequest = { reviewRequest = null },
            title = { Text("Review Call IT Ticket") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "Confirm these details before sending the ticket to ITCM.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    ReviewLine("Source", "Call IT")
                    ReviewLine("Caller", request.callerName)
                    request.contactEmail?.let { ReviewLine("Contact email", it) }
                    ReviewLine("Company", request.company)
                    if (request.deptId != null) ReviewLine("Department", selectedDepartment?.name ?: request.deptId.toString())
                    ReviewLine("Priority", request.priority)
                    ReviewLine("Issue type", request.issueType)
                    ReviewLine("Assigned to", selectedAssignee?.name ?: "Unassigned")
                    Text("Issue", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold)
                    Text(request.issue, style = MaterialTheme.typography.bodySmall)
                    if (reviewDaysToSupervisor != null && reviewDaysToManager != null) {
                        Text(
                            "Custom escalation: Supervisor ${reviewDaysToSupervisor}d · Manager ${reviewDaysToManager}d",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.primary
                        )
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        reviewRequest = null
                        viewModel.createTicketWithEscalation(
                            request = request,
                            daysToSupervisor = reviewDaysToSupervisor,
                            daysToManager = reviewDaysToManager,
                            reason = reviewReason
                        )
                    },
                    enabled = createState !is CreateTicketUiState.Loading
                ) { Text("Submit Ticket") }
            },
            dismissButton = {
                androidx.compose.material3.TextButton(onClick = { reviewRequest = null }) { Text("Edit") }
            }
        )
    }
}

@Composable
private fun ReviewLine(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun FormSectionCard(title: String, modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(title, style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold), color = MaterialTheme.colorScheme.primary)
            content()
        }
    }
}

@Composable
private fun LookupDropdown(
    label: String,
    selected: CallLookupItem?,
    items: List<CallLookupItem>,
    onSelect: (CallLookupItem) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    placeholder: String = "Select…",
    allowClear: Boolean = false
) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = modifier.fillMaxWidth()) {
        OutlinedTextField(
            value = selected?.name ?: "",
            onValueChange = {},
            label = { Text(label) },
            placeholder = { Text(placeholder, style = MaterialTheme.typography.bodySmall) },
            readOnly = true,
            enabled = enabled,
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp),
            trailingIcon = {
                IconButton(onClick = { if (enabled) expanded = !expanded }) {
                    Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select")
                }
            }
        )
        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier.heightIn(max = 280.dp)
        ) {
            if (allowClear && selected != null) {
                DropdownMenuItem(
                    text = { Text("— None —", style = MaterialTheme.typography.bodyMedium) },
                    onClick = { onSelect(CallLookupItem(0, "")); expanded = false },
                    modifier = Modifier.height(40.dp)
                )
            }
            items.forEach { item ->
                DropdownMenuItem(
                    text = { Text(item.name, style = MaterialTheme.typography.bodyMedium) },
                    onClick = { onSelect(item); expanded = false },
                    modifier = Modifier.height(40.dp),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp, vertical = 4.dp)
                )
            }
            if (items.isEmpty()) {
                DropdownMenuItem(
                    text = { Text("No options available", style = MaterialTheme.typography.bodySmall) },
                    onClick = { expanded = false },
                    modifier = Modifier.height(40.dp)
                )
            }
        }
    }
}

@Composable
private fun DaysStepper(
    label: String,
    value: Int,
    min: Int,
    max: Int = 30,
    onValueChange: (Int) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(label, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(
                onClick = { if (value > min) onValueChange(value - 1) },
                enabled = value > min
            ) {
                Icon(Icons.Filled.Remove, contentDescription = "Decrease", modifier = Modifier.size(18.dp))
            }
            Text(
                text = "$value",
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                modifier = Modifier.widthIn(min = 32.dp),
                textAlign = TextAlign.Center
            )
            IconButton(
                onClick = { if (value < max) onValueChange(value + 1) },
                enabled = value < max
            ) {
                Icon(Icons.Filled.Add, contentDescription = "Increase", modifier = Modifier.size(18.dp))
            }
        }
    }
}

@Composable
private fun StaticDropdown(
    label: String,
    value: String,
    options: List<String>,
    onSelect: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = modifier) {
        OutlinedTextField(
            value = value,
            onValueChange = {},
            label = { Text(label) },
            readOnly = true,
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp),
            trailingIcon = {
                IconButton(onClick = { expanded = !expanded }) {
                    Icon(Icons.Filled.KeyboardArrowDown, contentDescription = "Select")
                }
            }
        )
        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier.heightIn(max = 240.dp)
        ) {
            options.forEach { option ->
                DropdownMenuItem(
                    text = { Text(option, style = MaterialTheme.typography.bodyMedium) },
                    onClick = { onSelect(option); expanded = false },
                    modifier = Modifier.height(40.dp),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp, vertical = 4.dp)
                )
            }
        }
    }
}
