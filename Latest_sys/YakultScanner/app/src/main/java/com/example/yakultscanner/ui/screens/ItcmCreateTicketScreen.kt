package com.example.yakultscanner.ui.screens

import android.app.DatePickerDialog
import android.app.TimePickerDialog
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Warning
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
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
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
import com.example.yakultscanner.viewmodels.UpdateLocationUiState
import com.example.yakultscanner.ui.components.ItcmBottomSheetPicker
import com.example.yakultscanner.ui.components.ItcmExposedLookupDropdown
import com.example.yakultscanner.ui.components.ItcmExposedStringDropdown
import com.example.yakultscanner.ui.components.ItcmLookupPickerDialog
import com.example.yakultscanner.ui.components.ItcmPriorityChip
import com.example.yakultscanner.ui.components.ItcmSectionHeading
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.ContactEmailUiState
import com.example.yakultscanner.viewmodels.CreateTicketUiState
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
fun ItcmCreateTicketScreen(
    navController: NavController,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val snackbarHostState = remember { SnackbarHostState() }
    val createState by viewModel.createState.collectAsState()
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
    // Searchable caller — desktop parity: type >=2 chars to search across all employees, pick reverse-fills org and locks it.
    var callerQuery by rememberSaveable { mutableStateOf("") }
    var orgLockedFromSearch by rememberSaveable { mutableStateOf(false) }
    val employeeSearchResults by viewModel.employeeSearchResults.collectAsState()
    val employeeSearchLoading by viewModel.employeeSearchLoading.collectAsState()
    val callerScope = rememberCoroutineScope()
    var contactEmail by rememberSaveable { mutableStateOf("") }
    var issue by rememberSaveable { mutableStateOf("") }
    var providedSolution by rememberSaveable { mutableStateOf("") }
    var priority by rememberSaveable { mutableStateOf("Medium") }
    var issueType by rememberSaveable { mutableStateOf("Hardware") }
    var escalationEnabled by rememberSaveable { mutableStateOf(false) }
    var escalationDaysSup by rememberSaveable { mutableIntStateOf(2) }
    var escalationDaysMgr by rememberSaveable { mutableIntStateOf(3) }
    var escalationReason by rememberSaveable { mutableStateOf("") }
    var showReview by rememberSaveable { mutableStateOf(false) }
    var createAndNew by rememberSaveable { mutableStateOf(false) }

    // (a) Backdate — mirrors BorrowItemsScreen borrowBackdateEnabled pattern.
    var backdateEnabled by rememberSaveable { mutableStateOf(false) }
    var backdateMillis by rememberSaveable { mutableStateOf(System.currentTimeMillis()) }
    val context = LocalContext.current
    val backdateInFuture = backdateEnabled && backdateMillis > System.currentTimeMillis() + 60_000L
    val backdateInstant: String? = if (backdateEnabled) Instant.ofEpochMilli(backdateMillis).toString() else null

    // (b) Priority suggestion — desktop SuggestPriority parity; chip applies on tap only.
    val prioritySuggestion = remember(issue) { suggestItcmTicketPriority(issue) }
    val showPrioritySuggestion = issue.isNotBlank() && prioritySuggestion.first != priority

    // (c) Recent-open-ticket warning per caller (non-blocking).
    var callerOpenTickets by remember { mutableStateOf<List<CallTicketListItem>>(emptyList()) }

    // (e) Contact email resolve + link-to-caller. Caller empId comes from the picked lookup item.
    val contactEmailState by viewModel.contactEmailState.collectAsState()
    var contactEmailSource by remember { mutableStateOf<String?>(null) }
    var linkEmailToCaller by remember { mutableStateOf(false) }
    val callerEmpId: Int? = selectedCaller?.id?.takeIf { it > 0 }
    // Picker sheets live at screen root (after the Scaffold) so ModalBottomSheet
    // gets full-size constraints and anchors to the bottom edge.
    var showCallerPicker by remember { mutableStateOf(false) }
    var callerDialogSearch by rememberSaveable { mutableStateOf("") }
    var showAssigneePicker by remember { mutableStateOf(false) }
    var assigneeSearch by rememberSaveable { mutableStateOf("") }
    // Employee quick-add / location update (desktop parity). The cascade guard lets the
    // post-save org apply set company → department/branch → caller without the intermediate
    // LaunchedEffects wiping each level before the next one is applied.
    var showAddEmployee by remember { mutableStateOf(false) }
    var showUpdateLocation by remember { mutableStateOf(false) }
    var suppressCallerCascade by remember { mutableStateOf(false) }
    val addEmployeeState by viewModel.addEmployeeState.collectAsState()
    val updateLocationState by viewModel.updateLocationState.collectAsState()
    val applyScope = rememberCoroutineScope()

    val defaultSupervisorDays = escalationSettings?.daysToSupervisor ?: 2
    val defaultManagerDays = escalationSettings?.daysToManager ?: 3
    val isSubmitting = createState is CreateTicketUiState.Loading
    val isValid = selectedCompany != null && selectedCaller != null && issue.isNotBlank() && (!escalationEnabled || escalationReason.isNotBlank()) && !backdateInFuture

    fun resetFormForNew() {
        issue = ""
        providedSolution = ""
        priority = "Medium"
        backdateEnabled = false
        backdateMillis = System.currentTimeMillis()
        escalationEnabled = false
        escalationReason = ""
        createAndNew = false
        viewModel.resetContactEmailState()
        contactEmailSource = null
        callerOpenTickets = emptyList()
    }

    LaunchedEffect(Unit) {
        viewModel.loadCompanies()
        viewModel.loadItEmployees()
        viewModel.loadEscalationSettings()
    }
    LaunchedEffect(escalationSettings) {
        escalationSettings?.let { settings ->
            if (!escalationEnabled) {
                escalationDaysSup = settings.daysToSupervisor
                escalationDaysMgr = settings.daysToManager
            }
        }
    }
    LaunchedEffect(selectedCompany?.id) {
        if (orgLockedFromSearch || suppressCallerCascade) {
            if (suppressCallerCascade) {
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, null)
            }
            return@LaunchedEffect
        }
        selectedDepartment = null
        selectedBranch = null
        selectedCaller = null
        callerQuery = ""
        viewModel.clearCallerEmployees()
        viewModel.clearEmployeeSearchResults()
        viewModel.loadDepartments(selectedCompany?.id)
        viewModel.loadBranches(selectedCompany?.id, null)
    }
    LaunchedEffect(selectedDepartment?.id, selectedBranch?.id) {
        if (orgLockedFromSearch || suppressCallerCascade) {
            if (suppressCallerCascade) {
                selectedDepartment?.id?.takeIf { it > 0 }?.let { departmentId ->
                    viewModel.loadCallerEmployees(departmentId, selectedCompany?.id, selectedBranch?.id)
                }
            }
            return@LaunchedEffect
        }
        selectedCaller = null
        callerQuery = ""
        viewModel.clearCallerEmployees()
        viewModel.clearEmployeeSearchResults()
        selectedDepartment?.id?.takeIf { it > 0 }?.let { departmentId ->
            viewModel.loadCallerEmployees(departmentId, selectedCompany?.id, selectedBranch?.id)
        }
    }
    // Keep query in sync when a caller is picked via the dept-scoped list.
    LaunchedEffect(selectedCaller?.id) {
        selectedCaller?.let { callerQuery = it.name }
    }
    // Unlock org when the query is cleared below the search threshold (desktop: <2 chars).
    LaunchedEffect(callerQuery) {
        if (orgLockedFromSearch && callerQuery.trim().length < 2) {
            orgLockedFromSearch = false
            selectedCaller = null
            viewModel.clearEmployeeSearchResults()
        }
    }
    LaunchedEffect(createState) {
        when (val state = createState) {
            is CreateTicketUiState.Success -> {
                val message = buildString {
                    append("Ticket created: ${state.ticket?.ticketCode.orEmpty()}")
                    state.escalationWarning?.let { append(" — $it") }
                }
                snackbarHostState.showSnackbar(message)
                viewModel.resetCreateState()
                if (createAndNew) {
                    resetFormForNew()
                } else {
                    navController.popBackStack()
                }
            }
            is CreateTicketUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetCreateState()
            }
            else -> Unit
        }
    }

    /**
     * Applies a saved employee's org to the caller cascade and auto-selects the employee
     * (desktop: the ticket form auto-refreshes to the new employee). The caller item is
     * constructed directly so auto-select works even when the employee has no department
     * (the dept-scoped caller list cannot list those).
     */
    fun applySavedEmployeeOrg(comId: Int?, branchId: Int?, deptId: Int?, empId: Int, empName: String) {
        applyScope.launch {
            suppressCallerCascade = true
            try {
                orgLockedFromSearch = false
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
                callerQuery = empName
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

    // (c) Duplicate-warning check: pending tickets matching the caller name.
    LaunchedEffect(selectedCaller?.name) {        val name = selectedCaller?.name?.trim().orEmpty()
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
            else -> Unit
        }
    }

    fun buildRequest(): CreateTicketRequest = CreateTicketRequest(
        company = selectedCompany!!.name,
        callerName = selectedCaller!!.name,
        contactEmail = contactEmail.trim().ifBlank { null },
        issue = issue.trim(),
        comId = selectedCompany?.id,
        deptId = selectedDepartment?.id,
        branchId = selectedBranch?.id,
        providedSolution = providedSolution.ifBlank { null },
        issueType = issueType,
        priority = priority,
        assignedToEmpId = selectedAssignee?.id?.takeIf { it > 0 },
        createdByUserId = UserSession.currentUser?.userId?.takeIf { it > 0 },
        ticketSource = "CallIT"
    )

    Scaffold(
        containerColor = ItcmUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Create IT Ticket", color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Call IT workflow", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ItcmUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ItcmUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) },
        bottomBar = {
            Column(
                modifier = Modifier.fillMaxWidth().background(ItcmUi.Surface).padding(start = 16.dp, end = 16.dp, top = 10.dp, bottom = 8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = { if (isValid) showReview = true },
                    enabled = !isSubmitting,
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp)
                ) {
                    if (isSubmitting) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = ItcmUi.Surface, strokeWidth = 2.dp)
                    else Text("Review ticket", fontWeight = FontWeight.Bold)
                }
                if (!isValid) Text(
                    if (backdateInFuture) "Backdate cannot be in the future."
                    else "Company, caller, issue, and any enabled escalation reason are required.",
                    style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted
                )
            }
        }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().background(ItcmUi.Canvas).padding(padding).padding(horizontal = 16.dp).verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Spacer(Modifier.height(4.dp))
            ItcmSurfaceCard {
                ItcmSectionHeading("Ticket intake", "Create a traceable support request for onsite IT work.")
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    ItcmPriorityChip(priority)
                    Text(issueType, style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.SemiBold)
                }
            }
            ItcmSurfaceCard {
                ItcmSectionHeading("Requester and location", "Company selection drives the available organization and caller options.")
                var showCompanyPicker by remember { mutableStateOf(false) }
                var showDepartmentPicker by remember { mutableStateOf(false) }
                var showBranchPicker by remember { mutableStateOf(false) }
                Box(Modifier.fillMaxWidth()) {
                    ItcmExposedLookupDropdown(
                        label = "Company *",
                        selectedName = selectedCompany?.name.orEmpty(),
                        items = companies.map { it.name },
                        onSelectedByIndex = { if (!orgLockedFromSearch) selectedCompany = companies[it] },
                        enabled = !orgLockedFromSearch,
                        placeholder = if (orgLockedFromSearch) "Locked from caller search — clear caller to edit" else "Select company"
                    )
                    if (!orgLockedFromSearch) Box(Modifier.matchParentSize().clickable { showCompanyPicker = true })
                }
                if (showCompanyPicker) ItcmLookupPickerDialog(
                    title = "Select company",
                    items = companies,
                    itemLabel = { it.name },
                    onSelected = { selectedCompany = it; showCompanyPicker = false },
                    onDismiss = { showCompanyPicker = false }
                )
                Box(Modifier.fillMaxWidth()) {
                    ItcmExposedLookupDropdown(
                        label = "Department",
                        selectedName = selectedDepartment?.name.orEmpty(),
                        items = departments.map { it.name },
                        onSelectedByIndex = { if (!orgLockedFromSearch) selectedDepartment = departments[it] },
                        enabled = selectedCompany != null && !orgLockedFromSearch,
                        placeholder = if (orgLockedFromSearch) "Locked from caller search — clear caller to edit" else "Select company first"
                    )
                    if (selectedCompany != null && !orgLockedFromSearch) Box(Modifier.matchParentSize().clickable { showDepartmentPicker = true })
                }
                if (showDepartmentPicker) ItcmLookupPickerDialog(
                    title = "Select department",
                    items = departments,
                    itemLabel = { it.name },
                    onSelected = { selectedDepartment = it; showDepartmentPicker = false },
                    onDismiss = { showDepartmentPicker = false }
                )
                Box(Modifier.fillMaxWidth()) {
                    ItcmExposedLookupDropdown(
                        label = "Branch",
                        selectedName = selectedBranch?.name.orEmpty(),
                        items = branches.map { it.name },
                        onSelectedByIndex = { if (!orgLockedFromSearch) selectedBranch = branches[it] },
                        enabled = selectedCompany != null && !orgLockedFromSearch,
                        placeholder = if (orgLockedFromSearch) "Locked from caller search — clear caller to edit" else "Select company first"
                    )
                    if (selectedCompany != null && !orgLockedFromSearch) Box(Modifier.matchParentSize().clickable { showBranchPicker = true })
                }
                if (showBranchPicker) ItcmLookupPickerDialog(
                    title = "Select branch",
                    items = branches,
                    itemLabel = { it.name },
                    onSelected = { selectedBranch = it; showBranchPicker = false },
                    onDismiss = { showBranchPicker = false }
                )
                Box(Modifier.fillMaxWidth()) {
                    ItcmExposedLookupDropdown(
                        label = "Caller name *",
                        selectedName = callerQuery,
                        items = emptyList(),
                        onSelectedByIndex = {},
                        placeholder = "Search or pick caller"
                    )
                    Box(Modifier.matchParentSize().clickable { callerDialogSearch = ""; showCallerPicker = true })
                }
                val lockedCaller = selectedCaller
                if (orgLockedFromSearch && lockedCaller != null) {
                    val lockedOrgSummary = listOfNotNull(
                        selectedDepartment?.name?.takeIf { it.isNotBlank() },
                        selectedBranch?.name?.takeIf { it.isNotBlank() },
                        selectedCompany?.name?.takeIf { it.isNotBlank() }
                    ).joinToString(" • ")
                    if (lockedOrgSummary.isNotBlank()) {
                        Text(
                            "${lockedCaller.name} — $lockedOrgSummary",
                            style = MaterialTheme.typography.labelSmall,
                            color = ItcmUi.Muted
                        )
                    }
                    Text("Organization auto-filled from caller search — clear the caller name to edit again.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                }
                if (selectedCaller != null || callerQuery.isNotEmpty()) {
                    TextButton(
                        onClick = {
                            selectedCaller = null
                            callerQuery = ""
                            orgLockedFromSearch = false
                            viewModel.clearEmployeeSearchResults()
                        }
                    ) { Text("Clear caller") }
                }
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
                        Text("Update Location")
                    }
                }
                OutlinedTextField(value = contactEmail, onValueChange = { contactEmail = it }, label = { Text("Contact email (for status updates)") }, modifier = Modifier.fillMaxWidth(), singleLine = true, shape = RoundedCornerShape(16.dp))
                if (contactEmailState is ContactEmailUiState.Loading) {
                    Text("Looking up caller email…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                }
                contactEmailSource?.let { source ->
                    Text("from $source", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
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
                        Text("Link this email to caller", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink)
                    }
                }
            }
            // (c) Recent-open-ticket warning per caller (non-blocking).
            if (callerOpenTickets.isNotEmpty()) {
                val latest = callerOpenTickets.first()
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
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
            ItcmSurfaceCard {
                ItcmSectionHeading("Issue details")
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    ItcmExposedStringDropdown("Priority *", priority, listOf("Critical", "High", "Medium", "Low"), { priority = it }, Modifier.weight(1f))
                    ItcmExposedStringDropdown("Issue type *", issueType, listOf("Hardware", "Software", "Network", "Account", "Peripheral", "Setup", "Other"), { issueType = it }, Modifier.weight(1f))
                }
                OutlinedTextField(value = issue, onValueChange = { issue = it }, label = { Text("Technical problem / issue *") }, modifier = Modifier.fillMaxWidth(), minLines = 4, maxLines = 6, shape = RoundedCornerShape(16.dp))
                // (b) Priority suggestion chip — applies on tap, never overwrites silently.
                if (showPrioritySuggestion) {
                    AssistChip(
                        onClick = { priority = prioritySuggestion.first },
                        label = {
                            Text(prioritySuggestion.second ?: "Suggested: ${prioritySuggestion.first}")
                        }
                    )
                }
                OutlinedTextField(value = providedSolution, onValueChange = { providedSolution = it }, label = { Text("Initial solution / notes") }, modifier = Modifier.fillMaxWidth(), minLines = 3, maxLines = 5, shape = RoundedCornerShape(16.dp))
            }
            ItcmSurfaceCard {
                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                        ItcmSectionHeading("Timing", "Optional backdate. Off by default — turn on only for late-encoded tickets.")
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
                            onClick = { showItcmBackdateDatePicker(context, backdateMillis) { backdateMillis = it } },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(formatItcmBackdateDate(backdateMillis))
                        }
                        OutlinedButton(
                            onClick = { showItcmBackdateTimePicker(context, backdateMillis) { backdateMillis = it } },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text(formatItcmBackdateTime(backdateMillis))
                        }
                    }
                    Text(
                        "Recorded as: ${formatItcmBackdateDate(backdateMillis)} ${formatItcmBackdateTime(backdateMillis)}",
                        style = MaterialTheme.typography.labelSmall,
                        color = ItcmUi.Muted
                    )
                    if (backdateInFuture) {
                        Text(
                            "Backdate cannot be in the future.",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                }
            }
            ItcmSurfaceCard {
                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                        ItcmSectionHeading("Escalation override", "Optional custom timing. Defaults: supervisor ${defaultSupervisorDays}d · manager ${defaultManagerDays}d")
                    }
                    Switch(
                        checked = escalationEnabled,
                        onCheckedChange = { enabled ->
                            escalationEnabled = enabled
                            if (enabled) {
                                escalationDaysSup = defaultSupervisorDays
                                escalationDaysMgr = defaultManagerDays
                            }
                        }
                    )
                }
                if (escalationEnabled) {
                    ItcmDaysControl("Supervisor escalation", escalationDaysSup, defaultSupervisorDays) {
                        escalationDaysSup = it
                        if (escalationDaysMgr < it) escalationDaysMgr = it
                    }
                    ItcmDaysControl("Manager escalation", escalationDaysMgr, maxOf(defaultManagerDays, escalationDaysSup)) { escalationDaysMgr = it }
                    OutlinedTextField(value = escalationReason, onValueChange = { escalationReason = it }, label = { Text("Reason for override *") }, modifier = Modifier.fillMaxWidth(), minLines = 2, maxLines = 4, isError = escalationReason.isBlank(), shape = RoundedCornerShape(16.dp))
                }
            }
            ItcmSurfaceCard {
                ItcmSectionHeading("Assignment", "Optional. Leave unassigned for the team queue.")
                Box(Modifier.fillMaxWidth()) {
                    ItcmExposedLookupDropdown(
                        label = "Assign to IT staff",
                        selectedName = selectedAssignee?.name.orEmpty(),
                        items = itEmployees.map { it.name },
                        onSelectedByIndex = { selectedAssignee = itEmployees[it] },
                        placeholder = "Leave unassigned"
                    )
                    Box(Modifier.matchParentSize().clickable { assigneeSearch = ""; showAssigneePicker = true })
                }
                if (selectedAssignee != null) {
                    TextButton(onClick = { selectedAssignee = null }) { Text("Clear assignment") }
                }
            }
            Spacer(Modifier.height(12.dp))
        }
    }

    if (showAssigneePicker) {
        val assigneeQuery = assigneeSearch.trim()
        val isAssigneeSearching = assigneeQuery.length >= 2
        // Local filter: only 19 IT staff, no server round-trip needed.
        // Single-char input still filters (unlike the 2-char server search for callers).
        val assigneeItems = remember(assigneeSearch, itEmployees) {
            if (assigneeSearch.isBlank()) itEmployees
            else itEmployees.filter { it.name.contains(assigneeSearch.trim(), ignoreCase = true) }
        }
        ItcmBottomSheetPicker(
            title = "Assign to IT staff",
            supportingText = "Search 19 staff, or leave unassigned for the team queue.",
            items = assigneeItems,
            itemLabel = { it.name },
            selectedItem = selectedAssignee,
            onSelected = { selectedAssignee = it; assigneeSearch = ""; showAssigneePicker = false },
            onDismiss = { assigneeSearch = ""; showAssigneePicker = false },
            searchQuery = assigneeSearch,
            onSearchChange = { assigneeSearch = it },
            searchLabel = "Search IT staff",
            searchPlaceholder = "Type a name…",
            highlightQuery = assigneeSearch,
            emptyText = if (assigneeSearch.isNotBlank()) "No staff found for \"$assigneeQuery\"" else "No IT staff found.",
            emptyHint = if (assigneeSearch.isNotBlank()) "Try a different spelling or fewer characters." else null,
            resultCountText = if (isAssigneeSearching) {
                "${assigneeItems.size} result${if (assigneeItems.size == 1) "" else "s"} for \"$assigneeQuery\""
            } else {
                "${assigneeItems.size} staff member${if (assigneeItems.size == 1) "" else "s"}"
            },
            clearLabel = "Clear assignment",
            onClear = { selectedAssignee = null; assigneeSearch = ""; showAssigneePicker = false }
        )
    }

    if (showCallerPicker) {
        val isCallerSearchMode = callerDialogSearch.trim().length >= 2
        val callerDialogItems = if (isCallerSearchMode) employeeSearchResults else callerEmployees
        val departmentById = remember(departments) { departments.associateBy { it.id } }
        val branchById = remember(branches) { branches.associateBy { it.id } }
        fun callerSubtitle(item: CallLookupItem): String? {
            val deptName = item.deptId?.let { departmentById[it]?.name }
                ?: selectedDepartment?.name?.takeIf { !isCallerSearchMode }
            val branchName = item.branchId?.let { branchById[it]?.name }
                ?: selectedBranch?.name?.takeIf { !isCallerSearchMode }
            return listOfNotNull(
                deptName?.takeIf { it.isNotBlank() },
                branchName?.takeIf { it.isNotBlank() }
            ).joinToString(" • ").takeIf { it.isNotBlank() }
        }
        val callerQueryTrimmed = callerDialogSearch.trim()
        ItcmBottomSheetPicker(
            title = "Select caller",
            supportingText = if (isCallerSearchMode) "Search results — tap to auto-fill organization" else "Pick from the department list, or search all employees",
            items = callerDialogItems,
            itemLabel = { it.name },
            itemSubtitle = ::callerSubtitle,
            selectedItem = selectedCaller,
            onSelected = { picked ->
                showCallerPicker = false
                if (isCallerSearchMode) {
                    // Global search pick — reverse-fill org and lock (desktop parity: HandleCallerEmployeeSelectedAsync).
                    callerScope.launch {
                        val org = viewModel.loadEmployeeOrg(picked.id)
                        if (org != null && org.success) {
                            // Ensure dept/branch lists are loaded for the resolved company before selecting.
                            if (org.comId != null) {
                                viewModel.loadDepartments(org.comId)
                                viewModel.loadBranches(org.comId, null)
                                // Wait for the fresh lists to contain the resolved IDs (3s cap) instead of a fixed delay.
                                withTimeoutOrNull(3000) {
                                    combine(viewModel.departments, viewModel.branches) { depts, branches ->
                                        depts.any { it.id == org.deptId } &&
                                            (org.branchId == null || branches.any { it.id == org.branchId })
                                    }.first { it }
                                }
                            }
                            org.comId?.let { id -> viewModel.companies.value.firstOrNull { it.id == id }?.let { selectedCompany = it } }
                            org.deptId?.let { id -> viewModel.departments.value.firstOrNull { it.id == id }?.let { selectedDepartment = it } }
                            org.branchId?.let { id -> viewModel.branches.value.firstOrNull { it.id == id }?.let { selectedBranch = it } }
                            orgLockedFromSearch = true
                            if (org.deptId != null) viewModel.loadCallerEmployees(org.deptId, org.comId, org.branchId)
                        }
                        selectedCaller = picked
                        callerQuery = picked.name
                        viewModel.clearEmployeeSearchResults()
                    }
                } else {
                    selectedCaller = picked
                    callerQuery = picked.name
                }
            },
            onDismiss = { showCallerPicker = false; viewModel.clearEmployeeSearchResults() },
            searchQuery = callerDialogSearch,
            onSearchChange = {
                callerDialogSearch = it
                if (it.trim().length >= 2) viewModel.searchEmployeesByName(it)
                else viewModel.clearEmployeeSearchResults()
            },
            isLoading = isCallerSearchMode && employeeSearchLoading,
            emptyText = if (isCallerSearchMode) "No caller found for \"$callerQueryTrimmed\"" else "No callers in this department",
            emptyHint = if (isCallerSearchMode) "Try a different spelling or fewer characters." else "Pick a department first, or search all employees by name",
            searchLabel = "Search caller",
            searchPlaceholder = "Type at least 2 characters…",
            highlightQuery = callerDialogSearch,
            resultCountText = if (isCallerSearchMode) {
                "${callerDialogItems.size} result${if (callerDialogItems.size == 1) "" else "s"} for \"$callerQueryTrimmed\""
            } else {
                "${callerDialogItems.size} option${if (callerDialogItems.size == 1) "" else "s"}"
            },
            clearLabel = "Clear selection",
            onClear = {
                showCallerPicker = false
                selectedCaller = null
                callerQuery = ""
                orgLockedFromSearch = false
                viewModel.clearEmployeeSearchResults()
            }
        )
    }

    if (showAddEmployee) {
        ItcmAddEmployeeSheet(
            viewModel = viewModel,
            initialCompany = selectedCompany,
            initialDepartment = selectedDepartment,
            initialBranch = selectedBranch,
            onDismiss = {
                showAddEmployee = false
                viewModel.resetAddEmployeeState()
                // The sheet reloads the shared lookup lists for its own company;
                // restore the main form's company-scoped lists on close.
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, null)
            }
        )
    }

    val itcmUpdateCaller = selectedCaller
    if (showUpdateLocation && itcmUpdateCaller != null && callerEmpId != null) {
        ItcmUpdateLocationSheet(
            viewModel = viewModel,
            caller = itcmUpdateCaller,
            onDismiss = {
                showUpdateLocation = false
                viewModel.resetUpdateLocationState()
                viewModel.loadDepartments(selectedCompany?.id)
                viewModel.loadBranches(selectedCompany?.id, selectedDepartment?.id)
            }
        )
    }

    if (showReview) {
        val request = buildRequest()
        ModalBottomSheet(onDismissRequest = { showReview = false }, containerColor = ItcmUi.Surface) {
            Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                ItcmSectionHeading("Review ticket", "Confirm details before adding this request to IT Call Monitoring.")
                Column(modifier = Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    ItcmSurfaceCard {
                        ItcmCreateReviewLine("Source", "Call IT")
                        ItcmCreateReviewLine("Caller", request.callerName)
                        request.contactEmail?.let {
                            ItcmCreateReviewLine(
                                "Contact email",
                                it + (contactEmailSource?.let { src -> " (from $src)" } ?: "")
                            )
                        }
                        if (callerEmpId != null && linkEmailToCaller) {
                            Text(
                                "This email will be linked to the caller.",
                                style = MaterialTheme.typography.labelSmall,
                                color = ItcmUi.Muted
                            )
                        }
                        ItcmCreateReviewLine("Company", request.company)
                        selectedDepartment?.name?.let { ItcmCreateReviewLine("Department", it) }
                        selectedBranch?.name?.let { ItcmCreateReviewLine("Branch", it) }
                        ItcmCreateReviewLine("Priority", request.priority)
                        ItcmCreateReviewLine("Issue type", request.issueType)
                        ItcmCreateReviewLine("Assigned to", selectedAssignee?.name?.takeIf { it.isNotBlank() } ?: "Unassigned")
                        if (backdateEnabled) {
                            ItcmCreateReviewLine(
                                "Reported at",
                                "${formatItcmBackdateDate(backdateMillis)} ${formatItcmBackdateTime(backdateMillis)}"
                            )
                        }
                        if (escalationEnabled) ItcmCreateReviewLine("Custom escalation", "Supervisor ${escalationDaysSup}d · Manager ${escalationDaysMgr}d")
                        Text("Issue", style = MaterialTheme.typography.labelMedium, color = ItcmUi.Muted, fontWeight = FontWeight.Bold)
                        Text(request.issue, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink)
                    }
                }
                Button(
                    onClick = {
                        createAndNew = false
                        showReview = false
                        viewModel.createTicketWithEscalation(
                            request = request,
                            daysToSupervisor = escalationDaysSup.takeIf { escalationEnabled },
                            daysToManager = escalationDaysMgr.takeIf { escalationEnabled },
                            reason = escalationReason.trim().takeIf { escalationEnabled },
                            backdateInstant = backdateInstant,
                            callerEmpId = callerEmpId,
                            linkContactEmail = linkEmailToCaller.takeIf { callerEmpId != null }
                        )
                    },
                    enabled = !isSubmitting,
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp)
                ) { Text("Create ticket", fontWeight = FontWeight.Bold) }
                OutlinedButton(
                    onClick = {
                        createAndNew = true
                        showReview = false
                        viewModel.createTicketWithEscalation(
                            request = request,
                            daysToSupervisor = escalationDaysSup.takeIf { escalationEnabled },
                            daysToManager = escalationDaysMgr.takeIf { escalationEnabled },
                            reason = escalationReason.trim().takeIf { escalationEnabled },
                            backdateInstant = backdateInstant,
                            callerEmpId = callerEmpId,
                            linkContactEmail = linkEmailToCaller.takeIf { callerEmpId != null }
                        )
                    },
                    enabled = !isSubmitting,
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp)
                ) { Text("Create & new") }
                Spacer(Modifier.height(20.dp))
            }
        }
    }
}

@Composable
private fun ItcmDaysControl(label: String, value: Int, minimum: Int, onValueChange: (Int) -> Unit) {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
        Text("$label (days)", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, modifier = Modifier.weight(1f))
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = { if (value > minimum) onValueChange(value - 1) }, enabled = value > minimum) { Icon(Icons.Filled.Remove, contentDescription = "Decrease $label") }
            Text(value.toString(), style = MaterialTheme.typography.titleSmall, color = ItcmUi.Ink, fontWeight = FontWeight.Bold, modifier = Modifier.width(32.dp))
            IconButton(onClick = { if (value < 30) onValueChange(value + 1) }, enabled = value < 30) { Icon(Icons.Filled.Add, contentDescription = "Increase $label") }
        }
    }
}

@Composable
private fun ItcmCreateReviewLine(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted, modifier = Modifier.weight(0.4f))
        Text(value, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(0.6f))
    }
}

/**
 * Desktop SuggestPriority parity (ConfirmTicketCreationDialog.SuggestPriority).
 * Returns (priority, reason); reason is null when no keyword matched.
 * Exact desktop keyword lists — keep in sync.
 */
private fun suggestItcmTicketPriority(issue: String): Pair<String, String?> {
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

private fun showItcmBackdateDatePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    DatePickerDialog(context, { _, year, month, dayOfMonth ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.YEAR, year); updated.set(Calendar.MONTH, month); updated.set(Calendar.DAY_OF_MONTH, dayOfMonth)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH), calendar.get(Calendar.DAY_OF_MONTH)).show()
}

private fun showItcmBackdateTimePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    TimePickerDialog(context, { _, hourOfDay, minute ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.HOUR_OF_DAY, hourOfDay); updated.set(Calendar.MINUTE, minute); updated.set(Calendar.SECOND, 0); updated.set(Calendar.MILLISECOND, 0)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.HOUR_OF_DAY), calendar.get(Calendar.MINUTE), false).show()
}

private fun formatItcmBackdateDate(millis: Long): String =
    DateTimeFormatter.ofPattern("MMM d, yyyy", Locale.getDefault())
        .format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))

private fun formatItcmBackdateTime(millis: Long): String =
    DateTimeFormatter.ofPattern("h:mm a", Locale.getDefault())
        .format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))

/**
 * Desktop QuickAddEmployeeDialog parity in the ITCM sheet style. Title/Distributor omitted:
 * the mobile app has no title or distributor lookups loaded (contract sends titleId only
 * when known — null here).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmAddEmployeeSheet(
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

    var name by rememberSaveable { mutableStateOf("") }
    var employeeNumber by rememberSaveable { mutableStateOf("") }
    var position by rememberSaveable { mutableStateOf("") }
    var description by rememberSaveable { mutableStateOf("") }
    var company by remember { mutableStateOf(initialCompany) }
    var department by remember { mutableStateOf(initialDepartment) }
    var branch by remember { mutableStateOf(initialBranch) }
    var submitted by rememberSaveable { mutableStateOf(false) }
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

    ModalBottomSheet(onDismissRequest = { if (!isSaving) onDismiss() }, containerColor = ItcmUi.Surface) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            ItcmSectionHeading("Add employee", "Fields marked * are required. Department is optional.")
            Column(
                modifier = Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                ItcmSurfaceCard {
                    OutlinedTextField(
                        value = name, onValueChange = { name = it },
                        label = { Text("Employee Name *") },
                        modifier = Modifier.fillMaxWidth(), singleLine = true,
                        shape = RoundedCornerShape(16.dp), enabled = !isSaving,
                        isError = nameError,
                        supportingText = if (nameError) ({ Text("Employee name is required.") }) else null
                    )
                    OutlinedTextField(
                        value = employeeNumber, onValueChange = { employeeNumber = it },
                        label = { Text("Employee Number") },
                        modifier = Modifier.fillMaxWidth(), singleLine = true,
                        shape = RoundedCornerShape(16.dp), enabled = !isSaving
                    )
                    OutlinedTextField(
                        value = position, onValueChange = { position = it },
                        label = { Text("Position") },
                        modifier = Modifier.fillMaxWidth(), singleLine = true,
                        shape = RoundedCornerShape(16.dp), enabled = !isSaving
                    )
                    OutlinedTextField(
                        value = description, onValueChange = { description = it },
                        label = { Text("Description") },
                        modifier = Modifier.fillMaxWidth(), minLines = 2, maxLines = 4,
                        shape = RoundedCornerShape(16.dp), enabled = !isSaving
                    )
                }
                ItcmSurfaceCard {
                    ItcmExposedLookupDropdown(
                        label = "Company *",
                        selectedName = company?.name.orEmpty(),
                        items = companies.map { it.name },
                        onSelectedByIndex = { company = companies[it] },
                        enabled = !isSaving,
                        placeholder = "Select company",
                        isError = companyError,
                        supportingText = if (companyError) "Company is required." else null
                    )
                    ItcmExposedLookupDropdown(
                        label = "Branch *",
                        selectedName = branch?.name.orEmpty(),
                        items = branches.map { it.name },
                        onSelectedByIndex = { branch = branches[it] },
                        enabled = !isSaving && company != null,
                        placeholder = if (company == null) "Select a company first" else "Select branch",
                        isError = branchError,
                        supportingText = if (branchError) "Branch is required." else null
                    )
                    ItcmExposedLookupDropdown(
                        label = "Department (optional)",
                        selectedName = department?.name.orEmpty(),
                        items = departments.map { it.name },
                        onSelectedByIndex = { department = departments[it] },
                        enabled = !isSaving && company != null,
                        placeholder = if (company == null) "Select a company first" else "Select department"
                    )
                    if (department != null) {
                        TextButton(onClick = { department = null }, enabled = !isSaving) { Text("Clear department") }
                    }
                    if (serverError != null) {
                        Text(serverError, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.error)
                    }
                }
            }
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
                enabled = !isSaving,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) {
                if (isSaving) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = ItcmUi.Surface, strokeWidth = 2.dp)
                else Text("Save employee", fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(20.dp))
        }
    }
}

/**
 * Desktop Update Employee Location parity in the ITCM sheet style (company/branch required,
 * department optional, prefilled from the employee's current org). The desktop Reason field
 * is omitted: the call-employee-update-location contract accepts only empId/comId/branchId/deptId.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmUpdateLocationSheet(
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
    var prevCompanyId by remember { mutableStateOf<Int?>(null) }
    var prevDeptId by remember { mutableStateOf<Int?>(null) }
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
                prevCompanyId = company?.id
                prevDeptId = department?.id
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
        if (!loadingOrg && company?.id != prevCompanyId) {
            prevCompanyId = company?.id
            department = null
            branch = null
            viewModel.loadDepartments(company?.id)
            viewModel.loadBranches(company?.id, null)
        }
    }
    LaunchedEffect(department?.id, company?.id) {
        if (!loadingOrg && department?.id != prevDeptId && company != null) {
            prevDeptId = department?.id
            branch = null
            viewModel.loadBranches(company?.id, department?.id?.takeIf { it > 0 })
        }
    }

    val deptIdOrNull = department?.id?.takeIf { it > 0 }
    val changed = company?.id != snapshotComId || deptIdOrNull != snapshotDeptId || branch?.id != snapshotBranchId
    val companyError = submitted && company == null
    val branchError = submitted && branch == null
    val canSave = !isSaving && !loadingOrg && orgError == null && company != null && branch != null && changed

    ModalBottomSheet(onDismissRequest = { if (!isSaving) onDismiss() }, containerColor = ItcmUi.Surface) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            ItcmSectionHeading("Update employee location", caller.name)
            Column(
                modifier = Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                ItcmSurfaceCard {
                    Text(
                        "Update the employee's shared profile. Existing tickets keep their recorded location.",
                        style = MaterialTheme.typography.labelSmall,
                        color = ItcmUi.Muted
                    )
                    if (loadingOrg) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                            Text("Loading current location…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                        }
                    }
                    if (orgError != null) {
                        Text(orgError!!, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.error)
                    }
                    ItcmExposedLookupDropdown(
                        label = "Company *",
                        selectedName = company?.name.orEmpty(),
                        items = companies.map { it.name },
                        onSelectedByIndex = { company = companies[it] },
                        enabled = !isSaving && !loadingOrg,
                        placeholder = "Select company",
                        isError = companyError,
                        supportingText = if (companyError) "Company is required." else null
                    )
                    ItcmExposedLookupDropdown(
                        label = "Department (optional)",
                        selectedName = department?.name.orEmpty(),
                        items = departments.map { it.name },
                        onSelectedByIndex = { department = departments[it] },
                        enabled = !isSaving && !loadingOrg && company != null,
                        placeholder = if (company == null) "Select a company first" else "Select department"
                    )
                    if (department != null) {
                        TextButton(onClick = { department = null }, enabled = !isSaving && !loadingOrg) { Text("Clear department") }
                    }
                    ItcmExposedLookupDropdown(
                        label = "Branch *",
                        selectedName = branch?.name.orEmpty(),
                        items = branches.map { it.name },
                        onSelectedByIndex = { branch = branches[it] },
                        enabled = !isSaving && !loadingOrg && company != null,
                        placeholder = if (company == null) "Select a company first" else "Select branch",
                        isError = branchError,
                        supportingText = if (branchError) "Branch is required." else null
                    )
                    if (!changed && !loadingOrg && orgError == null) {
                        Text("No changes to save.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                    if (serverError != null) {
                        Text(serverError, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.error)
                    }
                }
            }
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
                enabled = canSave,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) {
                if (isSaving) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = ItcmUi.Surface, strokeWidth = 2.dp)
                else Text("Save location", fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(20.dp))
        }
    }
}