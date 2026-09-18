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
import androidx.compose.material3.Button
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallLookupItem
import com.example.yakultscanner.api.CreateTicketRequest
import com.example.yakultscanner.ui.components.ItcmBottomSheetPicker
import com.example.yakultscanner.ui.components.ItcmExposedLookupDropdown
import com.example.yakultscanner.ui.components.ItcmExposedStringDropdown
import com.example.yakultscanner.ui.components.ItcmLookupPickerDialog
import com.example.yakultscanner.ui.components.ItcmPriorityChip
import com.example.yakultscanner.ui.components.ItcmSectionHeading
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.CreateTicketUiState
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull

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
    // Picker sheets live at screen root (after the Scaffold) so ModalBottomSheet
    // gets full-size constraints and anchors to the bottom edge.
    var showCallerPicker by remember { mutableStateOf(false) }
    var callerDialogSearch by rememberSaveable { mutableStateOf("") }
    var showAssigneePicker by remember { mutableStateOf(false) }
    var assigneeSearch by rememberSaveable { mutableStateOf("") }

    val defaultSupervisorDays = escalationSettings?.daysToSupervisor ?: 2
    val defaultManagerDays = escalationSettings?.daysToManager ?: 3
    val isSubmitting = createState is CreateTicketUiState.Loading
    val isValid = selectedCompany != null && selectedCaller != null && issue.isNotBlank() && (!escalationEnabled || escalationReason.isNotBlank())

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
        if (orgLockedFromSearch) return@LaunchedEffect
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
        if (orgLockedFromSearch) return@LaunchedEffect
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
                navController.popBackStack()
            }
            is CreateTicketUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetCreateState()
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
                if (!isValid) Text("Company, caller, issue, and any enabled escalation reason are required.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
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
                OutlinedTextField(value = contactEmail, onValueChange = { contactEmail = it }, label = { Text("Contact email (for status updates)") }, modifier = Modifier.fillMaxWidth(), singleLine = true, shape = RoundedCornerShape(16.dp))
            }
            ItcmSurfaceCard {
                ItcmSectionHeading("Issue details")
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    ItcmExposedStringDropdown("Priority *", priority, listOf("Critical", "High", "Medium", "Low"), { priority = it }, Modifier.weight(1f))
                    ItcmExposedStringDropdown("Issue type *", issueType, listOf("Hardware", "Software", "Network", "Account", "Peripheral", "Setup", "Other"), { issueType = it }, Modifier.weight(1f))
                }
                OutlinedTextField(value = issue, onValueChange = { issue = it }, label = { Text("Technical problem / issue *") }, modifier = Modifier.fillMaxWidth(), minLines = 4, maxLines = 6, shape = RoundedCornerShape(16.dp))
                OutlinedTextField(value = providedSolution, onValueChange = { providedSolution = it }, label = { Text("Initial solution / notes") }, modifier = Modifier.fillMaxWidth(), minLines = 3, maxLines = 5, shape = RoundedCornerShape(16.dp))
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

    if (showReview) {
        val request = buildRequest()
        ModalBottomSheet(onDismissRequest = { showReview = false }, containerColor = ItcmUi.Surface) {
            Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                ItcmSectionHeading("Review ticket", "Confirm details before adding this request to IT Call Monitoring.")
                Column(modifier = Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    ItcmSurfaceCard {
                        ItcmCreateReviewLine("Source", "Call IT")
                        ItcmCreateReviewLine("Caller", request.callerName)
                        request.contactEmail?.let { ItcmCreateReviewLine("Contact email", it) }
                        ItcmCreateReviewLine("Company", request.company)
                        selectedDepartment?.name?.let { ItcmCreateReviewLine("Department", it) }
                        selectedBranch?.name?.let { ItcmCreateReviewLine("Branch", it) }
                        ItcmCreateReviewLine("Priority", request.priority)
                        ItcmCreateReviewLine("Issue type", request.issueType)
                        ItcmCreateReviewLine("Assigned to", selectedAssignee?.name?.takeIf { it.isNotBlank() } ?: "Unassigned")
                        if (escalationEnabled) ItcmCreateReviewLine("Custom escalation", "Supervisor ${escalationDaysSup}d · Manager ${escalationDaysMgr}d")
                        Text("Issue", style = MaterialTheme.typography.labelMedium, color = ItcmUi.Muted, fontWeight = FontWeight.Bold)
                        Text(request.issue, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink)
                    }
                }
                Button(
                    onClick = {
                        showReview = false
                        viewModel.createTicketWithEscalation(
                            request = request,
                            daysToSupervisor = escalationDaysSup.takeIf { escalationEnabled },
                            daysToManager = escalationDaysMgr.takeIf { escalationEnabled },
                            reason = escalationReason.trim().takeIf { escalationEnabled }
                        )
                    },
                    enabled = !isSubmitting,
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp)
                ) { Text("Create ticket", fontWeight = FontWeight.Bold) }
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