package com.example.yakultscanner.ui.screens

import android.app.DatePickerDialog
import android.app.TimePickerDialog
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
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallLookupItem
import com.example.yakultscanner.api.CallTicketListItem
import com.example.yakultscanner.api.CreateCallEmployeeRequest
import com.example.yakultscanner.api.CreateTicketRequest
import com.example.yakultscanner.api.UpdateCallEmployeeLocationRequest
import com.example.yakultscanner.viewmodels.AddEmployeeUiState
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.ContactEmailUiState
import com.example.yakultscanner.viewmodels.CreateTicketUiState
import com.example.yakultscanner.viewmodels.UpdateLocationUiState
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Calendar
import java.util.Locale

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

    // (a) Backdate — mirrors BorrowItemsScreen borrowBackdateEnabled pattern.
    var backdateEnabled by rememberSaveable { mutableStateOf(false) }
    var backdateMillis by rememberSaveable { mutableStateOf(System.currentTimeMillis()) }
    val context = LocalContext.current
    val backdateInFuture = backdateEnabled && backdateMillis > System.currentTimeMillis() + 60_000L
    val backdateInstant: String? = if (backdateEnabled) Instant.ofEpochMilli(backdateMillis).toString() else null

    // (b) Priority suggestion — desktop SuggestPriority parity; chip applies on tap only.
    val prioritySuggestion = remember(issue) { suggestCreateTicketPriority(issue) }
    val showPrioritySuggestion = issue.isNotBlank() && prioritySuggestion.first != priority

    // (c) Recent-open-ticket warning per caller (non-blocking).
    var callerOpenTickets by remember { mutableStateOf<List<CallTicketListItem>>(emptyList()) }

    // (e) Contact email resolve + link-to-caller.
    val contactEmailState by viewModel.contactEmailState.collectAsState()
    var contactEmailSource by remember { mutableStateOf<String?>(null) }
    var linkEmailToCaller by remember { mutableStateOf(false) }
    // Caller empId comes from the picked lookup item (CallLookupItem.id).
    val callerEmpId: Int? = selectedCaller?.id?.takeIf { it > 0 }

    // Employee quick-add / location update (desktop parity). The cascade guard lets the
    // post-save org apply set company → department/branch → caller without the intermediate
    // LaunchedEffects wiping each level before the next one is applied.
    var showAddEmployee by remember { mutableStateOf(false) }
    var showUpdateLocation by remember { mutableStateOf(false) }
    var suppressCallerCascade by remember { mutableStateOf(false) }
    val addEmployeeState by viewModel.addEmployeeState.collectAsState()
    val updateLocationState by viewModel.updateLocationState.collectAsState()

    val escalationDefaultSup = escalationSettings?.daysToSupervisor ?: 2
    val escalationDefaultMgr = escalationSettings?.daysToManager ?: 3
    var escalationEnabled by remember { mutableStateOf(false) }
    var escalationDaysSup by remember { mutableIntStateOf(2) }
    var escalationDaysMgr by remember { mutableIntStateOf(3) }
    var escalationReason by remember { mutableStateOf("") }
    var reviewRequest by remember { mutableStateOf<CreateTicketRequest?>(null) }
    var reviewCreateAndNew by remember { mutableStateOf(false) }
    var reviewBackdateLabel by remember { mutableStateOf<String?>(null) }
    var reviewCallerEmpId by remember { mutableStateOf<Int?>(null) }
    var reviewLinkContactEmail by remember { mutableStateOf<Boolean?>(null) }
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
        if (suppressCallerCascade) {
            viewModel.loadDepartments(selectedCompany?.id)
            viewModel.loadBranches(selectedCompany?.id, null)
            return@LaunchedEffect
        }
        selectedDepartment = null
        selectedBranch = null
        selectedCaller = null
        viewModel.clearCallerEmployees()
        viewModel.loadDepartments(selectedCompany?.id)
        viewModel.loadBranches(selectedCompany?.id, null)
    }

    LaunchedEffect(selectedDepartment, selectedBranch) {
        if (suppressCallerCascade) {
            val deptId = selectedDepartment?.id
            if (deptId != null && deptId > 0) {
                viewModel.loadCallerEmployees(deptId, selectedCompany?.id, selectedBranch?.id)
            }
            return@LaunchedEffect
        }
        selectedCaller = null
        viewModel.clearCallerEmployees()
        val deptId = selectedDepartment?.id
        if (deptId != null && deptId > 0) {
            viewModel.loadCallerEmployees(deptId, selectedCompany?.id, selectedBranch?.id)
        }
    }

    /**
     * Applies a saved employee's org to the caller cascade and auto-selects the employee
     * (desktop: the ticket form auto-refreshes to the new employee). The caller item is
     * constructed directly so auto-select works even when the employee has no department
     * (the dept-scoped caller list cannot list those).
     */
    fun applySavedEmployeeOrg(comId: Int?, branchId: Int?, deptId: Int?, empId: Int, empName: String) {
        scope.launch {
            suppressCallerCascade = true
            try {
                if (comId != null && comId > 0) {
                    viewModel.companies.value.firstOrNull { it.id == comId }?.let { selectedCompany = it }
                    viewModel.loadDepartments(comId)
                    viewModel.loadBranches(comId, null)
                    withTimeoutOrNull(3000) {
                        combine(viewModel.departments, viewModel.branches) { depts, brs ->
                            (deptId == null || depts.any { it.id == deptId }) &&
                                (branchId == null || brs.any { it.id == branchId })
                        }.first { it }
                    }
                }
                selectedDepartment = deptId?.let { id -> viewModel.departments.value.firstOrNull { it.id == id } }
                if (branchId != null && branchId > 0) {
                    viewModel.branches.value.firstOrNull { it.id == branchId }?.let { selectedBranch = it }
                } else {
                    selectedBranch = null
                }
                val deptForCallers = selectedDepartment?.id?.takeIf { it > 0 } ?: deptId
                if (deptForCallers != null && deptForCallers > 0) {
                    viewModel.loadCallerEmployees(deptForCallers, comId, branchId)
                }
                selectedCaller = CallLookupItem(id = empId, name = empName, deptId = deptId, branchId = branchId, comId = comId)
                // Let the suppressed cascades run (they reload lists without clearing), then release.
                kotlinx.coroutines.delay(500)
            } finally {
                suppressCallerCascade = false
            }
        }
    }

    LaunchedEffect(addEmployeeState) {
        if (addEmployeeState is AddEmployeeUiState.Success) {
            val emp = (addEmployeeState as AddEmployeeUiState.Success).employee
            viewModel.resetAddEmployeeState()
            showAddEmployee = false
            if (emp != null && emp.empId > 0) {
                applySavedEmployeeOrg(emp.comId, emp.branchId, emp.deptId, emp.empId, emp.name.orEmpty())
            } else {
                selectedDepartment?.id?.takeIf { it > 0 }?.let {
                    viewModel.loadCallerEmployees(it, selectedCompany?.id, selectedBranch?.id)
                }
            }
            snackbarHostState.showSnackbar("Employee added.")
        }
    }

    LaunchedEffect(updateLocationState) {
        if (updateLocationState is UpdateLocationUiState.Success) {
            val emp = (updateLocationState as UpdateLocationUiState.Success).employee
            viewModel.resetUpdateLocationState()
            showUpdateLocation = false
            if (emp != null && emp.empId > 0 && emp.empId == callerEmpId) {
                applySavedEmployeeOrg(emp.comId, emp.branchId, emp.deptId, emp.empId, emp.name ?: selectedCaller?.name.orEmpty())
            } else {
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, selectedDepartment?.id)
                selectedDepartment?.id?.takeIf { it > 0 }?.let {
                    viewModel.loadCallerEmployees(it, selectedCompany?.id, selectedBranch?.id)
                }
            }
            snackbarHostState.showSnackbar("Employee location saved successfully.")
        }
    }

    fun isValid(): Boolean {
        if (selectedCompany == null || selectedCaller == null || issue.isBlank()) return false
        if (escalationEnabled && escalationReason.isBlank()) return false
        if (backdateInFuture) return false
        return true
    }

    fun resetFormForNew() {
        issue = ""
        providedSolution = ""
        priority = "Medium"
        backdateEnabled = false
        backdateMillis = System.currentTimeMillis()
        escalationEnabled = false
        escalationReason = ""
        viewModel.resetContactEmailState()
        contactEmailSource = null
        callerOpenTickets = emptyList()
    }

    // (c) Duplicate-warning check: pending tickets matching the caller name.
    LaunchedEffect(selectedCaller?.name) {
        val name = selectedCaller?.name?.trim().orEmpty()
        if (name.isEmpty()) {
            callerOpenTickets = emptyList()
            return@LaunchedEffect
        }
        kotlinx.coroutines.delay(400)
        callerOpenTickets = viewModel.searchOpenTicketsForCaller(name)
    }

    // (e) Auto-resolve contact email when the caller employee is picked.
    LaunchedEffect(selectedCaller?.id, selectedCompany?.id, selectedDepartment?.id, selectedBranch?.id) {
        val empId = callerEmpId
        if (empId == null) {
            viewModel.resetContactEmailState()
            contactEmailSource = null
            return@LaunchedEffect
        }
        viewModel.resolveTicketContactEmail(
            callerEmpId = empId,
            comId = selectedCompany?.id,
            deptId = selectedDepartment?.id,
            branchId = selectedBranch?.id
        )
    }
    LaunchedEffect(contactEmailState) {
        when (val state = contactEmailState) {
            is ContactEmailUiState.Success -> {
                contactEmailSource = state.source
                if (contactEmail.isBlank() && state.email.isNotBlank()) {
                    contactEmail = state.email
                }
                // Default checked unless the email already is the employee's personal email.
                linkEmailToCaller = state.source != "Employee personal email" && callerEmpId != null
            }
            is ContactEmailUiState.Idle -> {
                contactEmailSource = null
            }
            else -> {}
        }
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
                if (reviewCreateAndNew) {
                    reviewCreateAndNew = false
                    resetFormForNew()
                } else {
                    navController.popBackStack()
                }
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
                // Desktop parity: quick-add beside the caller field; location update needs a picked caller.
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(
                        onClick = { showAddEmployee = true },
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Add Employee")
                    }
                    OutlinedButton(
                        onClick = { showUpdateLocation = true },
                        enabled = callerEmpId != null,
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Update Employee Location")
                    }
                }
                OutlinedTextField(
                    value = contactEmail,
                    onValueChange = { contactEmail = it },
                    label = { Text("Contact Email (for status updates)") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 1,
                    singleLine = true
                )
                if (contactEmailState is ContactEmailUiState.Loading) {
                    Text(
                        "Looking up caller email…",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                contactEmailSource?.let { source ->
                    Text(
                        "from $source",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (callerEmpId != null) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Checkbox(
                            checked = linkEmailToCaller,
                            onCheckedChange = { linkEmailToCaller = it }
                        )
                        Text(
                            "Link this email to caller",
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                }
            }

            // (c) Recent-open-ticket warning per caller (non-blocking).
            if (callerOpenTickets.isNotEmpty()) {
                val latest = callerOpenTickets.first()
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.tertiaryContainer
                    )
                ) {
                    Row(
                        modifier = Modifier.padding(14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Icon(
                            Icons.Filled.Warning,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onTertiaryContainer
                        )
                        Text(
                            "Caller has ${callerOpenTickets.size} open ticket(s) — latest ${latest.ticketCode ?: ""} ${latest.status ?: ""}".trim(),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onTertiaryContainer
                        )
                    }
                }
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
                // (b) Priority suggestion chip — applies on tap, never overwrites silently.
                if (showPrioritySuggestion) {
                    AssistChip(
                        onClick = { priority = prioritySuggestion.first },
                        label = {
                            Text(prioritySuggestion.second ?: "Suggested: ${prioritySuggestion.first}")
                        }
                    )
                }
                OutlinedTextField(
                    value = providedSolution,
                    onValueChange = { providedSolution = it },
                    label = { Text("Initial Solution / Notes (optional)") },
                    modifier = Modifier.fillMaxWidth().height(100.dp),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 4
                )
            }

            // (a) Backdate — mirrors BorrowItemsScreen timing pattern (toggle + pickers, future blocked).
            FormSectionCard(title = "Timing (optional)") {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text("Backdate ticket", style = MaterialTheme.typography.bodyMedium)
                        Text(
                            if (backdateEnabled) "Using the actual reported date and time."
                            else "Off by default. Turn this on only for late-encoded tickets.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = backdateEnabled,
                        onCheckedChange = { enabled ->
                            backdateEnabled = enabled
                            if (enabled) backdateMillis = System.currentTimeMillis()
                        }
                    )
                }
                if (backdateEnabled) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        OutlinedButton(
                            onClick = { showTicketBackdateDatePicker(context, backdateMillis) { backdateMillis = it } },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(formatTicketBackdateDate(backdateMillis))
                        }
                        OutlinedButton(
                            onClick = { showTicketBackdateTimePicker(context, backdateMillis) { backdateMillis = it } },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(formatTicketBackdateTime(backdateMillis))
                        }
                    }
                    Text(
                        "Recorded as: ${formatTicketBackdateDate(backdateMillis)} ${formatTicketBackdateTime(backdateMillis)}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    if (backdateInFuture) {
                        Text(
                            "Backdate cannot be in the future.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                }
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
                            val msg = if (backdateInFuture) "Backdate cannot be in the future."
                            else "Company, Caller Name and Issue are required"
                            snackbarHostState.showSnackbar(msg)
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
                    reviewBackdateLabel = if (backdateEnabled) "${formatTicketBackdateDate(backdateMillis)} ${formatTicketBackdateTime(backdateMillis)}" else null
                    reviewCallerEmpId = callerEmpId
                    reviewLinkContactEmail = linkEmailToCaller.takeIf { callerEmpId != null }
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

    if (showAddEmployee) {
        CreateTicketAddEmployeeDialog(
            viewModel = viewModel,
            initialCompany = selectedCompany,
            initialDepartment = selectedDepartment,
            initialBranch = selectedBranch,
            onDismiss = {
                showAddEmployee = false
                viewModel.resetAddEmployeeState()
                // The dialog reloads the shared lookup lists for its own company;
                // restore the main form's company-scoped lists on close.
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, null)
            }
        )
    }

    val updateLocationCaller = selectedCaller
    if (showUpdateLocation && updateLocationCaller != null && callerEmpId != null) {
        CreateTicketUpdateLocationDialog(
            viewModel = viewModel,
            caller = updateLocationCaller,
            onDismiss = {
                showUpdateLocation = false
                viewModel.resetUpdateLocationState()
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, selectedDepartment?.id)
            }
        )
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
                    request.contactEmail?.let {
                        ReviewLine("Contact email", it + (contactEmailSource?.let { src -> " (from $src)" } ?: ""))
                    }
                    if (reviewLinkContactEmail == true) {
                        Text(
                            "This email will be linked to the caller.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    ReviewLine("Company", request.company)
                    if (request.deptId != null) ReviewLine("Department", selectedDepartment?.name ?: request.deptId.toString())
                    if (request.branchId != null) ReviewLine("Branch", selectedBranch?.name ?: request.branchId.toString())
                    ReviewLine("Priority", request.priority)
                    ReviewLine("Issue type", request.issueType)
                    ReviewLine("Assigned to", selectedAssignee?.name ?: "Unassigned")
                    reviewBackdateLabel?.let { ReviewLine("Reported at", it) }
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
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    TextButton(
                        onClick = {
                            reviewCreateAndNew = true
                            val req = request
                            val backdate = reviewBackdateLabel?.let { backdateInstant }
                            val empId = reviewCallerEmpId
                            val link = reviewLinkContactEmail
                            reviewRequest = null
                            viewModel.createTicketWithEscalation(
                                request = req,
                                daysToSupervisor = reviewDaysToSupervisor,
                                daysToManager = reviewDaysToManager,
                                reason = reviewReason,
                                backdateInstant = backdate,
                                callerEmpId = empId,
                                linkContactEmail = link
                            )
                        },
                        enabled = createState !is CreateTicketUiState.Loading
                    ) { Text("Create & New") }
                    Button(
                        onClick = {
                            reviewCreateAndNew = false
                            val req = request
                            val backdate = reviewBackdateLabel?.let { backdateInstant }
                            val empId = reviewCallerEmpId
                            val link = reviewLinkContactEmail
                            reviewRequest = null
                            viewModel.createTicketWithEscalation(
                                request = req,
                                daysToSupervisor = reviewDaysToSupervisor,
                                daysToManager = reviewDaysToManager,
                                reason = reviewReason,
                                backdateInstant = backdate,
                                callerEmpId = empId,
                                linkContactEmail = link
                            )
                        },
                        enabled = createState !is CreateTicketUiState.Loading
                    ) { Text("Create") }
                }
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

/**
 * Desktop QuickAddEmployeeDialog parity. Title/Distributor omitted: the mobile app has no
 * title or distributor lookups loaded (contract sends titleId only when known — null here).
 */
@Composable
private fun CreateTicketAddEmployeeDialog(
    viewModel: CallMonitoringViewModel,
    initialCompany: CallLookupItem?,
    initialDepartment: CallLookupItem?,
    initialBranch: CallLookupItem?,
    onDismiss: () -> Unit
) {
    val companies by viewModel.companies.collectAsState()
    val departments by viewModel.departments.collectAsState()
    val branches by viewModel.branches.collectAsState()
    val addState by viewModel.addEmployeeState.collectAsState()

    var name by remember { mutableStateOf("") }
    var employeeNumber by remember { mutableStateOf("") }
    var position by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var company by remember { mutableStateOf(initialCompany) }
    var department by remember { mutableStateOf(initialDepartment) }
    var branch by remember { mutableStateOf(initialBranch) }
    var submitted by remember { mutableStateOf(false) }
    val isSaving = addState is AddEmployeeUiState.Loading
    val serverError = (addState as? AddEmployeeUiState.Error)?.message

    LaunchedEffect(Unit) {
        if (company == null) viewModel.loadCompanies()
        else {
            viewModel.loadDepartments(company?.id)
            viewModel.loadBranches(company?.id, null)
        }
    }
    var prevCompanyId by remember { mutableStateOf(company?.id) }
    LaunchedEffect(company?.id) {
        if (company?.id != prevCompanyId) {
            prevCompanyId = company?.id
            department = null
            branch = null
            viewModel.loadDepartments(company?.id)
            viewModel.loadBranches(company?.id, null)
        }
    }

    val nameError = submitted && name.isBlank()
    val companyError = submitted && company == null
    val branchError = submitted && branch == null

    AlertDialog(
        onDismissRequest = { if (!isSaving) onDismiss() },
        title = { Text("Add Employee") },
        text = {
            Column(
                modifier = Modifier.verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    "Fields marked * are required. Department is optional.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Employee Name *") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    singleLine = true,
                    enabled = !isSaving,
                    isError = nameError,
                    supportingText = if (nameError) ({ Text("Employee name is required.") }) else null
                )
                OutlinedTextField(
                    value = employeeNumber,
                    onValueChange = { employeeNumber = it },
                    label = { Text("Employee Number") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    singleLine = true,
                    enabled = !isSaving
                )
                OutlinedTextField(
                    value = position,
                    onValueChange = { position = it },
                    label = { Text("Position") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    singleLine = true,
                    enabled = !isSaving
                )
                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it },
                    label = { Text("Description") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    maxLines = 3,
                    enabled = !isSaving
                )
                LookupDropdown(
                    label = "Company *",
                    selected = company,
                    items = companies,
                    onSelect = { company = it },
                    enabled = !isSaving,
                    placeholder = "Select company"
                )
                if (companyError) {
                    Text("Company is required.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                LookupDropdown(
                    label = "Branch *",
                    selected = branch,
                    items = branches,
                    onSelect = { branch = it },
                    enabled = !isSaving && company != null,
                    placeholder = if (company == null) "Select a company first" else "Select branch"
                )
                if (branchError) {
                    Text("Branch is required.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                LookupDropdown(
                    label = "Department (optional)",
                    selected = department,
                    items = departments,
                    onSelect = { department = if (it.id > 0) it else null },
                    enabled = !isSaving && company != null,
                    placeholder = if (company == null) "Select a company first" else "Select department",
                    allowClear = true
                )
                if (serverError != null) {
                    Text(serverError, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    submitted = true
                    if (name.isBlank() || company == null || branch == null) return@Button
                    viewModel.createEmployee(
                        CreateCallEmployeeRequest(
                            name = name.trim(),
                            employeeNumber = employeeNumber.trim().ifBlank { null },
                            position = position.trim().ifBlank { null },
                            description = description.trim().ifBlank { null },
                            comId = company!!.id,
                            branchId = branch!!.id,
                            deptId = department?.id?.takeIf { it > 0 }
                        )
                    )
                },
                enabled = !isSaving
            ) {
                if (isSaving) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                } else {
                    Text("Save")
                }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, enabled = !isSaving) { Text("Cancel") }
        }
    )
}

/**
 * Desktop Update Employee Location parity (company/branch required, department optional,
 * prefilled from the employee's current org). The desktop Reason field is omitted: the
 * call-employee-update-location contract accepts only empId/comId/branchId/deptId.
 */
@Composable
private fun CreateTicketUpdateLocationDialog(
    viewModel: CallMonitoringViewModel,
    caller: CallLookupItem,
    onDismiss: () -> Unit
) {
    val companies by viewModel.companies.collectAsState()
    val departments by viewModel.departments.collectAsState()
    val branches by viewModel.branches.collectAsState()
    val updateState by viewModel.updateLocationState.collectAsState()

    var company by remember { mutableStateOf<CallLookupItem?>(null) }
    var department by remember { mutableStateOf<CallLookupItem?>(null) }
    var branch by remember { mutableStateOf<CallLookupItem?>(null) }
    var snapshotComId by remember { mutableStateOf<Int?>(null) }
    var snapshotDeptId by remember { mutableStateOf<Int?>(null) }
    var snapshotBranchId by remember { mutableStateOf<Int?>(null) }
    var loadingOrg by remember { mutableStateOf(true) }
    var orgError by remember { mutableStateOf<String?>(null) }
    var submitted by remember { mutableStateOf(false) }
    var prevUpdateCompanyId by remember { mutableStateOf<Int?>(null) }
    var prevUpdateDeptId by remember { mutableStateOf<Int?>(null) }
    val isSaving = updateState is UpdateLocationUiState.Loading
    val serverError = (updateState as? UpdateLocationUiState.Error)?.message

    LaunchedEffect(caller.id) {
        loadingOrg = true
        orgError = null
        try {
            val org = viewModel.loadEmployeeOrg(caller.id)
            if (org != null && org.success) {
                if (org.comId != null) {
                    viewModel.loadDepartments(org.comId)
                    viewModel.loadBranches(org.comId, null)
                    withTimeoutOrNull(3000) {
                        combine(viewModel.departments, viewModel.branches) { depts, brs ->
                            (org.deptId == null || depts.any { it.id == org.deptId }) &&
                                (org.branchId == null || brs.any { it.id == org.branchId })
                        }.first { it }
                    }
                }
                snapshotComId = org.comId
                snapshotDeptId = org.deptId
                snapshotBranchId = org.branchId
                company = org.comId?.let { id -> viewModel.companies.value.firstOrNull { it.id == id } }
                department = org.deptId?.let { id -> viewModel.departments.value.firstOrNull { it.id == id } }
                branch = org.branchId?.let { id -> viewModel.branches.value.firstOrNull { it.id == id } }
                prevUpdateCompanyId = company?.id
                prevUpdateDeptId = department?.id
            } else {
                orgError = "Could not load the employee's current location."
            }
        } catch (e: Exception) {
            orgError = "Network error: ${e.message}"
        } finally {
            loadingOrg = false
        }
    }
    LaunchedEffect(company?.id) {
        if (!loadingOrg && company?.id != prevUpdateCompanyId) {
            prevUpdateCompanyId = company?.id
            department = null
            branch = null
            viewModel.loadDepartments(company?.id)
            viewModel.loadBranches(company?.id, null)
        }
    }
    LaunchedEffect(department?.id, company?.id) {
        if (!loadingOrg && department?.id != prevUpdateDeptId && company != null) {
            prevUpdateDeptId = department?.id
            branch = null
            viewModel.loadBranches(company?.id, department?.id?.takeIf { it > 0 })
        }
    }

    val deptIdOrNull = department?.id?.takeIf { it > 0 }
    val changed = company?.id != snapshotComId || deptIdOrNull != snapshotDeptId || branch?.id != snapshotBranchId
    val companyError = submitted && company == null
    val branchError = submitted && branch == null
    val canSave = !isSaving && !loadingOrg && orgError == null && company != null && branch != null && changed

    AlertDialog(
        onDismissRequest = { if (!isSaving) onDismiss() },
        title = { Text("Update Employee Location") },
        text = {
            Column(
                modifier = Modifier.verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    caller.name,
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    "Update the employee's shared profile. Existing tickets keep their recorded location.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                if (loadingOrg) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                        Text("Loading current location…", style = MaterialTheme.typography.bodySmall)
                    }
                }
                if (orgError != null) {
                    Text(orgError!!, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                LookupDropdown(
                    label = "Company *",
                    selected = company,
                    items = companies,
                    onSelect = { company = it },
                    enabled = !isSaving && !loadingOrg,
                    placeholder = "Select company"
                )
                if (companyError) {
                    Text("Company is required.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                LookupDropdown(
                    label = "Department (optional)",
                    selected = department,
                    items = departments,
                    onSelect = {
                        department = if (it.id > 0) it else null
                    },
                    enabled = !isSaving && !loadingOrg && company != null,
                    placeholder = if (company == null) "Select a company first" else "Select department",
                    allowClear = true
                )
                LookupDropdown(
                    label = "Branch *",
                    selected = branch,
                    items = branches,
                    onSelect = { branch = it },
                    enabled = !isSaving && !loadingOrg && company != null,
                    placeholder = if (company == null) "Select a company first" else "Select branch"
                )
                if (branchError) {
                    Text("Branch is required.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                if (!changed && !loadingOrg && orgError == null) {
                    Text("No changes to save.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (serverError != null) {
                    Text(serverError, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    submitted = true
                    if (company == null || branch == null || !changed) return@Button
                    viewModel.updateEmployeeLocation(
                        UpdateCallEmployeeLocationRequest(
                            empId = caller.id,
                            comId = company!!.id,
                            branchId = branch!!.id,
                            deptId = deptIdOrNull
                        )
                    )
                },
                enabled = canSave
            ) {
                if (isSaving) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                } else {
                    Text("Save Location")
                }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, enabled = !isSaving) { Text("Cancel") }
        }
    )
}

/**
 * Desktop SuggestPriority parity (ConfirmTicketCreationDialog.SuggestPriority).
 * Returns (priority, reason); reason is null when no keyword matched.
 * Exact desktop keyword lists — keep in sync.
 */
private fun suggestCreateTicketPriority(issue: String): Pair<String, String?> {
    val text = issue.trim()
    if (text.isEmpty()) return "Medium" to null
    val lower = text.lowercase(Locale.getDefault())
    fun containsAny(vararg needles: String): Boolean =
        needles.any { !it.isBlank() && lower.contains(it.lowercase(Locale.getDefault())) }
    if (containsAny("system down", "server down", "network down", "cannot login", "can't login", "urgent", "critical", "production down")) {
        return "Critical" to "Suggested: Critical because the issue appears urgent or blocks access/service."
    }
    if (containsAny("not working", "printer not working", "offline", "no internet", "cannot print", "unable to print", "failed")) {
        return "High" to "Suggested: High because the issue likely blocks normal work."
    }
    if (containsAny("slow", "intermittent", "request", "setup", "install")) {
        return "Medium" to "Suggested: Medium based on the issue description."
    }
    return "Medium" to null
}

private fun showTicketBackdateDatePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    DatePickerDialog(context, { _, year, month, dayOfMonth ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.YEAR, year); updated.set(Calendar.MONTH, month); updated.set(Calendar.DAY_OF_MONTH, dayOfMonth)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH), calendar.get(Calendar.DAY_OF_MONTH)).show()
}

private fun showTicketBackdateTimePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    TimePickerDialog(context, { _, hourOfDay, minute ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.HOUR_OF_DAY, hourOfDay); updated.set(Calendar.MINUTE, minute); updated.set(Calendar.SECOND, 0); updated.set(Calendar.MILLISECOND, 0)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.HOUR_OF_DAY), calendar.get(Calendar.MINUTE), false).show()
}

private fun formatTicketBackdateDate(millis: Long): String =
    DateTimeFormatter.ofPattern("MMM d, yyyy", Locale.getDefault())
        .format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))

private fun formatTicketBackdateTime(millis: Long): String =
    DateTimeFormatter.ofPattern("h:mm a", Locale.getDefault())
        .format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))
