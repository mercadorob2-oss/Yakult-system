package com.example.yakultscanner.ui.screens

import android.net.Uri
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import com.example.yakultscanner.api.CreateRepairTicketRequest
import com.example.yakultscanner.api.RepairLookupItemDto
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketDetailResponse
import com.example.yakultscanner.api.RepairTicketSummaryDto
import com.example.yakultscanner.viewmodels.RepairActionUiState
import com.example.yakultscanner.viewmodels.RepairCreateUiState
import com.example.yakultscanner.viewmodels.RepairDetailUiState
import com.example.yakultscanner.viewmodels.RepairPortalViewModel
import com.example.yakultscanner.viewmodels.RepairTicketsUiState
import kotlinx.coroutines.delay
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

enum class RepairWorkspace(val label: String, val scope: String, val caption: String) {
    Requests("My requests", "mine", "Your submitted and requested repairs"),
    Technician("Technician queue", "technician", "Shared work requiring technician attention"),
    Reports("Reports", "reports", "Attendance & repair summaries")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairPortalHomeScreen(
    navController: NavController,
    viewModel: RepairPortalViewModel = hiltViewModel()
) {
    RepairSystemBars()
    val state by viewModel.ticketsState.collectAsStateWithLifecycle()
    var workspaceName by rememberSaveable { mutableStateOf(RepairWorkspace.Requests.name) }
    val workspace = RepairWorkspace.entries.firstOrNull { it.name == workspaceName } ?: RepairWorkspace.Requests
    var searchQuery by rememberSaveable { mutableStateOf("") }
    var selectedPriority by rememberSaveable { mutableStateOf<String?>(null) }

    LaunchedEffect(workspace, searchQuery, selectedPriority) {
        if (workspace == RepairWorkspace.Reports) return@LaunchedEffect
        if (searchQuery.isNotBlank()) delay(300)
        viewModel.loadTickets(
            scope = workspace.scope,
            priority = selectedPriority,
            search = searchQuery.takeIf { it.isNotBlank() }
        )
    }

    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Repair Portal", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Equipment care workspace", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = RepairUi.Ink)
                    }
                },
                actions = {
                    IconButton(onClick = {
                        if (workspace == RepairWorkspace.Reports) viewModel.loadReports()
                        else viewModel.loadTickets(
                            scope = workspace.scope,
                            priority = selectedPriority,
                            search = searchQuery.takeIf { it.isNotBlank() }
                        )
                    }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh", tint = RepairUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = RepairUi.Surface)
            )
        }
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(RepairUi.Canvas)
                .padding(padding)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
            ) {
                // Tabs — always visible, with outer horizontal padding only here
                Column(modifier = Modifier.padding(horizontal = 16.dp)) {
                    Spacer(Modifier.height(12.dp))
                    RepairWorkspaceTabs(workspace) { workspaceName = it.name }
                    Spacer(Modifier.height(12.dp))
                }

                if (workspace == RepairWorkspace.Reports) {
                    // Reports tab — owns its own scroll & refresh, no search/priority bar
                    Box(modifier = Modifier.fillMaxSize().weight(1f)) {
                        RepairReportsTabContent(navController = navController, viewModel = viewModel)
                    }
                } else {
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(horizontal = 16.dp)
                            .weight(1f)
                    ) {
                        OutlinedTextField(
                            value = searchQuery,
                            onValueChange = { searchQuery = it },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            placeholder = { Text("Search code, item, serial, or problem", color = RepairUi.Muted) },
                            leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null, tint = RepairUi.Muted) },
                            trailingIcon = {
                                Icon(Icons.Filled.FilterList, contentDescription = null, tint = RepairUi.Muted)
                            },
                            shape = RoundedCornerShape(12.dp),
                            colors = repairTextFieldColors()
                        )
                        Spacer(Modifier.height(10.dp))
                        RepairPriorityFilters(selectedPriority) { selectedPriority = it }
                        Spacer(Modifier.height(14.dp))

                        when (val current = state) {
                            RepairTicketsUiState.Idle, RepairTicketsUiState.Loading -> RepairLoading("Loading repair tickets…")
                            is RepairTicketsUiState.Error -> RepairErrorPanel(
                                message = current.message,
                                onRetry = {
                                    viewModel.loadTickets(
                                        scope = workspace.scope,
                                        priority = selectedPriority,
                                        search = searchQuery.takeIf { it.isNotBlank() }
                                    )
                                },
                                onBackToRequests = { workspaceName = RepairWorkspace.Requests.name }
                            )
                            is RepairTicketsUiState.Success -> {
                                if (current.response.tickets.isEmpty()) {
                                    RepairEmptyQueue(
                                        workspace = workspace,
                                        filtered = searchQuery.isNotBlank() || selectedPriority != null,
                                        onCreate = { navController.navigate("repair_ticket_create") }
                                    )
                                } else {
                                    LazyColumn(
                                        modifier = Modifier.fillMaxSize(),
                                        contentPadding = PaddingValues(top = 2.dp, bottom = 96.dp),
                                        verticalArrangement = Arrangement.spacedBy(12.dp)
                                    ) {
                                        item {
                                            Row(
                                                modifier = Modifier.fillMaxWidth(),
                                                horizontalArrangement = Arrangement.SpaceBetween,
                                                verticalAlignment = Alignment.CenterVertically
                                            ) {
                                                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                                    Text(workspace.label, style = MaterialTheme.typography.titleMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                                                    Text(workspace.caption, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                                                }
                                                Text(
                                                    "${current.response.tickets.size} shown",
                                                    style = MaterialTheme.typography.labelSmall,
                                                    color = RepairUi.Muted,
                                                    fontWeight = FontWeight.SemiBold
                                                )
                                            }
                                        }
                                        items(current.response.tickets, key = { it.repairTicketId }) { ticket ->
                                            RepairTicketCard(ticket) {
                                                navController.navigate("repair_ticket_detail/${ticket.repairTicketId}")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (workspace != RepairWorkspace.Reports) {
                ExtendedFloatingActionButton(
                    onClick = { navController.navigate("repair_ticket_create") },
                    modifier = Modifier.align(Alignment.BottomEnd).padding(end = 16.dp, bottom = 16.dp),
                    containerColor = RepairUi.Brand,
                    contentColor = RepairUi.Surface,
                    icon = { Icon(Icons.Filled.Add, contentDescription = null) },
                    text = { Text("New repair", fontWeight = FontWeight.Bold) }
                )
            }
        }
    }
}

@Composable
private fun RepairWorkspaceTabs(selected: RepairWorkspace, onSelected: (RepairWorkspace) -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(RepairUi.SurfaceSubtle)
            .padding(4.dp),
        horizontalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        RepairWorkspace.entries.forEach { workspace ->
            val active = workspace == selected
            Box(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(10.dp))
                    .background(if (active) RepairUi.Surface else RepairUi.SurfaceSubtle)
                    .clickable { onSelected(workspace) }
                    .padding(vertical = 10.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    workspace.label,
                    style = MaterialTheme.typography.labelMedium,
                    color = if (active) RepairUi.Ink else RepairUi.Muted,
                    fontWeight = if (active) FontWeight.Bold else FontWeight.Medium
                )
            }
        }
    }
}

@Composable
private fun RepairPriorityFilters(selectedPriority: String?, onSelected: (String?) -> Unit) {
    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        item {
            FilterChip(
                selected = selectedPriority == null,
                onClick = { onSelected(null) },
                label = { Text("All priorities") },
                leadingIcon = if (selectedPriority == null) ({ Icon(Icons.Filled.CheckCircle, null, modifier = Modifier.size(16.dp)) }) else null
            )
        }
        items(listOf("High", "Critical", "Medium", "Low")) { priority ->
            FilterChip(
                selected = selectedPriority == priority,
                onClick = { onSelected(if (selectedPriority == priority) null else priority) },
                label = { Text(priority) }
            )
        }
    }
}

@Composable
private fun RepairTicketCard(ticket: RepairTicketSummaryDto, onClick: () -> Unit) {
    val item = ticket.itemName?.takeIf { it.isNotBlank() } ?: "Unknown equipment"
    val modelSerial = listOfNotNull(ticket.modelNumber, ticket.serialNumber?.let { "S/N $it" }).joinToString(" · ")
    RepairSurfaceCard(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick)
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(ticket.ticketCode ?: "Repair #${ticket.repairTicketId}", style = MaterialTheme.typography.labelMedium, color = RepairUi.Brand, fontWeight = FontWeight.Bold)
                Text(item, style = MaterialTheme.typography.bodyLarge, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            Spacer(Modifier.width(10.dp))
            RepairStatusPill(ticket.status)
        }
        if (modelSerial.isNotBlank()) RepairMetaText(modelSerial)
        Text(ticket.problem?.takeIf { it.isNotBlank() } ?: "No problem description was provided.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
        HorizontalDivider(color = RepairUi.Divider)
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            RepairPriorityPill(ticket.priority)
            ticket.linkedCallTicketCode?.takeIf { it.isNotBlank() }?.let {
                Text("IT CALL · $it", style = MaterialTheme.typography.labelSmall, color = RepairUi.Brand, fontWeight = FontWeight.SemiBold)
            } ?: Text("Open ticket", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
        }
    }
}

@Composable
private fun RepairLoading(message: String) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
            CircularProgressIndicator(color = RepairUi.Brand)
            Text(message, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
        }
    }
}

@Composable
private fun RepairEmptyQueue(workspace: RepairWorkspace, filtered: Boolean, onCreate: () -> Unit) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        RepairSurfaceCard(modifier = Modifier.fillMaxWidth(), tonalColor = RepairUi.Surface) {
            Icon(Icons.Filled.Inventory2, contentDescription = null, modifier = Modifier.size(32.dp), tint = RepairUi.Brand)
            Text(if (filtered) "No matching repair tickets" else "Nothing needs attention here", style = MaterialTheme.typography.titleMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
            Text(
                if (filtered) "Try clearing the search or priority filter." else if (workspace == RepairWorkspace.Requests) "Create a repair request when equipment needs help." else "The technician queue is currently clear.",
                style = MaterialTheme.typography.bodySmall,
                color = RepairUi.Muted
            )
            if (workspace == RepairWorkspace.Requests && !filtered) {
                RepairPrimaryButton(onClick = onCreate) { Text("Create repair request") }            }
        }
    }
}

@Composable
private fun RepairErrorPanel(message: String, onRetry: () -> Unit, onBackToRequests: () -> Unit) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        RepairSurfaceCard(modifier = Modifier.fillMaxWidth(), tonalColor = RepairUi.CriticalSoft) {
            Text("We could not load this workspace", style = MaterialTheme.typography.titleMedium, color = RepairUi.Critical, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RepairSecondaryButton(onClick = onBackToRequests) { Text("My requests") }
                RepairPrimaryButton(onClick = onRetry) { Text("Try again") }            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairTicketCreateScreen(
    navController: NavController,
    viewModel: RepairPortalViewModel = hiltViewModel()
) {
    RepairSystemBars()
    val items by viewModel.itemLookup.collectAsStateWithLifecycle()
    val lookupLoading by viewModel.lookupLoading.collectAsStateWithLifecycle()
    val createState by viewModel.createState.collectAsStateWithLifecycle()
    var search by rememberSaveable { mutableStateOf("") }
    var selectedItem by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var problem by rememberSaveable { mutableStateOf("") }
    var priority by rememberSaveable { mutableStateOf("Medium") }
    var departmentLevel by rememberSaveable { mutableStateOf(false) }
    var requestedCompany by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var requestedBranch by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var requestedDepartment by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var requestedEmployee by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var requestedPicker by remember { mutableStateOf<RepairCreateLookupRequest?>(null) }
    val snackbar = remember { SnackbarHostState() }

    fun openRequestedByPicker(
        title: String,
        kind: String,
        companyId: Int? = null,
        departmentId: Int? = null,
        branchId: Int? = null
    ) {
        requestedPicker = RepairCreateLookupRequest(title, kind, companyId, departmentId, branchId)
    }

    val requestedByValid = !departmentLevel || (requestedCompany != null && requestedDepartment != null)
    val requestedByType = when {
        departmentLevel -> "Department"
        requestedEmployee != null -> "Employee"
        else -> null
    }

    LaunchedEffect(Unit) { viewModel.searchItems("") }
    LaunchedEffect(createState) {
        when (val result = createState) {
            is RepairCreateUiState.Success -> {
                viewModel.resetCreateState()
                navController.navigate("repair_ticket_detail/${result.ticketId}") {
                    popUpTo("repair_ticket_create") { inclusive = true }
                }
            }
            is RepairCreateUiState.Error -> {
                snackbar.showSnackbar(result.message)
                viewModel.resetCreateState()
            }
            else -> Unit
        }
    }

    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("New repair request", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Tell the repair team what needs attention", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                    }
                },
                navigationIcon = { IconButton(onClick = { navController.popBackStack() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = RepairUi.Ink) } },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = RepairUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbar) },
        bottomBar = {
            Column(modifier = Modifier.fillMaxWidth().background(RepairUi.Surface).padding(horizontal = 16.dp, vertical = 10.dp)) {
                RepairPrimaryButton(
                    onClick = {
                        selectedItem?.let { item ->
                            viewModel.createTicket(
                                CreateRepairTicketRequest(
                                    itemId = item.id,
                                    problem = problem.trim(),
                                    priority = priority,
                                    requestedByType = requestedByType,
                                    requestedByEmployeeId = if (departmentLevel) null else requestedEmployee?.id,
                                    requestedByDepartmentId = requestedDepartment?.id,
                                    requestedByCompanyId = requestedCompany?.id,
                                    requestedByBranchId = requestedBranch?.id
                                )
                            )
                        }
                    },
                    enabled = selectedItem != null && problem.isNotBlank() && requestedByValid && createState !is RepairCreateUiState.Loading,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    if (createState is RepairCreateUiState.Loading) CircularProgressIndicator(Modifier.size(20.dp), color = RepairUi.Surface, strokeWidth = 2.dp)
                    else Text("Submit repair request", fontWeight = FontWeight.Bold)
                }
            }
        }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().background(RepairUi.Canvas).padding(padding).padding(horizontal = 16.dp).verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Spacer(Modifier.height(2.dp))
            RepairSurfaceCard(tonalColor = RepairUi.BrandSoft) {
                Text("A clear request gets repaired faster", style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                Text("1. Select equipment  ·  2. Describe the problem  ·  3. Set urgency", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            }
            RepairSurfaceCard {
                RepairSectionHeading("1 · Select equipment", "Search by item, model, or serial number.")
                OutlinedTextField(
                    value = search,
                    onValueChange = { search = it; viewModel.searchItems(it) },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Search equipment") },
                    singleLine = true,
                    leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null, tint = RepairUi.Muted) },
                    shape = RoundedCornerShape(10.dp),
                colors = repairTextFieldColors()
                )
                if (lookupLoading) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        CircularProgressIndicator(Modifier.size(18.dp), color = RepairUi.Brand, strokeWidth = 2.dp)
                        Text("Searching equipment…", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                    }
                }
                selectedItem?.let { SelectedItemCard(it) }
                if (items.isNotEmpty()) {
                    Text("Matches", style = MaterialTheme.typography.labelLarge, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                    val equipmentResults = items
                    val equipmentListState = rememberLazyListState()
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(180.dp)
                            .clip(RoundedCornerShape(10.dp))
                            .background(RepairUi.SurfaceSubtle.copy(alpha = 0.35f))
                            .border(BorderStroke(1.dp, RepairUi.Divider), RoundedCornerShape(10.dp))
                    ) {
                        LazyColumn(
                            state = equipmentListState,
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(end = 12.dp),
                            contentPadding = PaddingValues(horizontal = 10.dp, vertical = 2.dp)
                        ) {
                            items(equipmentResults) { item ->
                                RepairLookupRow(
                                    item = item,
                                    selected = item.id == selectedItem?.id,
                                    onClick = { selectedItem = item }
                                )
                                HorizontalDivider(color = RepairUi.Divider)
                            }
                        }
                        RepairVerticalScrollbar(
                            state = equipmentListState,
                            modifier = Modifier
                                .align(Alignment.CenterEnd)
                                .fillMaxHeight()
                                .padding(vertical = 8.dp, horizontal = 4.dp)
                        )
                    }
                }
            }
            RepairSurfaceCard {
                RepairSectionHeading("2 · Describe the problem", "Include symptoms, damage, or when the issue began.")
                OutlinedTextField(
                    value = problem,
                    onValueChange = { problem = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("What needs repair? *") },
                    minLines = 4,
                    maxLines = 6,
                    shape = RoundedCornerShape(10.dp),
                colors = repairTextFieldColors()
                )
            }
            RepairSurfaceCard {
                RepairSectionHeading("3 · Set urgency", "Choose the impact on daily work.")
                LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(listOf("Low", "Medium", "High", "Critical")) { value ->
                        FilterChip(selected = priority == value, onClick = { priority = value }, label = { Text(value) })
                    }
                }
            }
            RepairSurfaceCard {
                RepairSectionHeading("4 · Requested By", "Use the same Company, Branch, Department, and Employee context as desktop intake.")
                RepairCreatePickButton("Company", requestedCompany?.name, onClick = {
                    openRequestedByPicker("Select company", "companies")
                })
                RepairCreatePickButton("Branch", requestedBranch?.name, enabled = requestedCompany != null, onClick = {
                    openRequestedByPicker("Select branch", "branches", companyId = requestedCompany?.id)
                })
                RepairCreatePickButton("Department", requestedDepartment?.name, enabled = requestedCompany != null, onClick = {
                    openRequestedByPicker(
                        "Select department",
                        "departments",
                        companyId = requestedCompany?.id,
                        branchId = requestedBranch?.id
                    )
                })
                RepairCreatePickButton(
                    "Employee",
                    if (departmentLevel) "Not applicable for department-level request" else requestedEmployee?.name,
                    enabled = !departmentLevel && requestedCompany != null && requestedDepartment != null,
                    onClick = {
                        openRequestedByPicker(
                            "Select employee",
                            "employees",
                            companyId = requestedCompany?.id,
                            departmentId = requestedDepartment?.id,
                            branchId = requestedBranch?.id
                        )
                    }
                )
                Row(
                    modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(RepairUi.SurfaceSubtle).padding(horizontal = 8.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Checkbox(
                        checked = departmentLevel,
                        onCheckedChange = { checked ->
                            departmentLevel = checked
                            if (checked) requestedEmployee = null
                        }
                    )
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Department-level request", style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
                        Text("No specific employee", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                    }
                }
                if (departmentLevel && (requestedCompany == null || requestedDepartment == null)) {
                    Text("Choose a company and department before submitting a department-level request.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Warning)
                }
            }
            Spacer(Modifier.height(88.dp))
        }
    }

    requestedPicker?.let { request ->
        RepairCreateLookupDialog(
            request = request,
            viewModel = viewModel,
            onDismiss = { requestedPicker = null },
            onSelected = { selected ->
                when (request.kind) {
                    "companies" -> {
                        requestedCompany = selected
                        requestedBranch = null
                        requestedDepartment = null
                        requestedEmployee = null
                    }
                    "branches" -> {
                        requestedBranch = selected
                        requestedDepartment = null
                        requestedEmployee = null
                    }
                    "departments" -> {
                        requestedDepartment = selected
                        requestedEmployee = null
                    }
                    "employees" -> requestedEmployee = selected
                }
                requestedPicker = null
            }
        )
    }
}

private data class RepairCreateLookupRequest(
    val title: String,
    val kind: String,
    val companyId: Int? = null,
    val departmentId: Int? = null,
    val branchId: Int? = null
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun RepairCreateLookupDialog(
    request: RepairCreateLookupRequest,
    viewModel: RepairPortalViewModel,
    onDismiss: () -> Unit,
    onSelected: (RepairLookupItemDto) -> Unit
) {
    val items by viewModel.technicianLookup.collectAsStateWithLifecycle()
    val loading by viewModel.lookupLoading.collectAsStateWithLifecycle()
    var search by remember(request.kind, request.companyId, request.departmentId, request.branchId) { mutableStateOf("") }

    LaunchedEffect(request.kind, request.companyId, request.departmentId, request.branchId) {
        viewModel.loadTechnicianLookup(request.kind, null, request.companyId, request.departmentId, request.branchId)
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text(request.title, color = RepairUi.Ink, fontWeight = FontWeight.Bold) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = search,
                    onValueChange = {
                        search = it
                        viewModel.loadTechnicianLookup(request.kind, it.ifBlank { null }, request.companyId, request.departmentId, request.branchId)
                    },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Search") },
                    singleLine = true,
                    colors = repairTextFieldColors()
                )
                if (loading) CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally), color = RepairUi.Brand)
                Column(Modifier.heightIn(max = 320.dp).verticalScroll(rememberScrollState())) {
                    if (!loading && items.isEmpty()) Text("No matching choices.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                    items.forEach { item ->
                        Row(
                            Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).clickable { onSelected(item) }.padding(horizontal = 8.dp, vertical = 10.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                Text(item.name.ifBlank { "Item #${item.id}" }, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
                                item.displayText.takeIf { it != item.name }?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted) }
                            }
                        }
                        HorizontalDivider(color = RepairUi.Divider)
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close", color = RepairUi.Brand) } }
    )
}

@Composable
private fun RepairCreatePickButton(label: String, selected: String?, enabled: Boolean = true, onClick: () -> Unit) {
    RepairSecondaryButton(onClick = onClick, enabled = enabled, modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(1.dp)) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
            Text(
                selected?.takeIf { it.isNotBlank() } ?: if (enabled) "Select" else "Select company first",
                style = MaterialTheme.typography.bodySmall,
                color = if (enabled) RepairUi.Ink else RepairUi.Muted,
                fontWeight = FontWeight.SemiBold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun SelectedItemCard(item: RepairLookupItemDto) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).background(RepairUi.BrandSoft).padding(12.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(Icons.Filled.CheckCircle, contentDescription = null, tint = RepairUi.Brand)
        Column(modifier = Modifier.weight(1f)) {
            Text("Selected equipment", style = MaterialTheme.typography.labelSmall, color = RepairUi.Brand, fontWeight = FontWeight.Bold)
            Text(item.displayText, style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
        }
    }
}

@Composable
private fun RepairVerticalScrollbar(
    state: LazyListState,
    modifier: Modifier = Modifier
) {
    val layoutInfo = state.layoutInfo
    val totalItems = layoutInfo.totalItemsCount
    val visibleItems = layoutInfo.visibleItemsInfo
    val lastVisibleItem = visibleItems.lastOrNull()
    val viewportStart = layoutInfo.viewportStartOffset
    val viewportEnd = layoutInfo.viewportEndOffset
    val canScrollBackward = state.firstVisibleItemIndex > 0 || state.firstVisibleItemScrollOffset > 0
    val canScrollForward = lastVisibleItem != null && (
        lastVisibleItem.index < totalItems - 1 ||
            lastVisibleItem.offset + lastVisibleItem.size > viewportEnd
        )

    if (totalItems == 0 || (!canScrollBackward && !canScrollForward)) return

    val viewportSize = (viewportEnd - viewportStart).coerceAtLeast(1)
    val averageItemSize = visibleItems
        .map { it.size }
        .average()
        .toFloat()
        .coerceAtLeast(1f)
    val estimatedContentSize = (averageItemSize * totalItems).coerceAtLeast(viewportSize.toFloat())
    val scrollPosition = state.firstVisibleItemIndex * averageItemSize + state.firstVisibleItemScrollOffset
    val maxScrollPosition = (estimatedContentSize - viewportSize).coerceAtLeast(1f)
    val scrollProgress = (scrollPosition / maxScrollPosition).coerceIn(0f, 1f)
    val thumbFraction = (viewportSize / estimatedContentSize).coerceIn(0.15f, 1f)

    Canvas(modifier = modifier.width(6.dp)) {
        val trackCorner = CornerRadius(size.width / 2f, size.width / 2f)
        drawRoundRect(
            color = RepairUi.Divider,
            topLeft = Offset.Zero,
            size = Size(size.width, size.height),
            cornerRadius = trackCorner
        )

        val minimumThumbHeight = 28.dp.toPx().coerceAtMost(size.height)
        val thumbHeight = (size.height * thumbFraction)
            .coerceAtLeast(minimumThumbHeight)
            .coerceAtMost(size.height)
        val thumbTop = (size.height - thumbHeight) * scrollProgress
        val thumbCorner = CornerRadius(size.width / 2f, size.width / 2f)
        drawRoundRect(
            color = RepairUi.Brand,
            topLeft = Offset(0f, thumbTop),
            size = Size(size.width, thumbHeight),
            cornerRadius = thumbCorner
        )
    }
}

@Composable
private fun RepairLookupRow(item: RepairLookupItemDto, selected: Boolean, onClick: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).clickable(onClick = onClick).padding(vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Box(
            modifier = Modifier.size(30.dp).clip(RoundedCornerShape(10.dp)).background(if (selected) RepairUi.BrandSoft else RepairUi.SurfaceSubtle),
            contentAlignment = Alignment.Center
        ) {
            Icon(if (selected) Icons.Filled.CheckCircle else Icons.Filled.Inventory2, contentDescription = null, modifier = Modifier.size(17.dp), tint = if (selected) RepairUi.Brand else RepairUi.Muted)
        }
        Column(modifier = Modifier.weight(1f)) {
            Text(item.displayText, style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium)
            item.category?.takeIf { it.isNotBlank() }?.let { RepairMetaText(it) }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairTicketDetailScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: RepairPortalViewModel = hiltViewModel()
) {
    RepairSystemBars()
    val state by viewModel.detailState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    var noteText by rememberSaveable { mutableStateOf("") }
    var pendingNotes by remember { mutableStateOf<List<String>>(emptyList()) }
    var showPartDialog by remember { mutableStateOf(false) }
    var showConclusionDialog by remember { mutableStateOf(false) }
    var statusExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(ticketId) { viewModel.loadDetail(ticketId) }
    LaunchedEffect(actionState) {
        when (val current = actionState) {
            is RepairActionUiState.Success -> {
                snackbar.showSnackbar(current.message)
                viewModel.resetActionState()
            }
            is RepairActionUiState.Error -> {
                snackbar.showSnackbar(current.message)
                viewModel.resetActionState()
            }
            else -> Unit
        }
    }

    val headerTicket = (state as? RepairDetailUiState.Success)?.response?.ticket
    LaunchedEffect(state) {
        if (state is RepairDetailUiState.Success) pendingNotes = emptyList()
    }
    val headerTitle = when {
        headerTicket == null -> "Repair details"
        !headerTicket.itemName.isNullOrBlank() -> "${headerTicket.ticketCode ?: "Repair"} · ${headerTicket.itemName}"
        else -> headerTicket.ticketCode ?: "Repair #${headerTicket.repairTicketId}"
    }
    val headerAsset = headerTicket?.let { t ->
        listOfNotNull(
            t.modelNumber?.takeIf { it.isNotBlank() },
            t.serialNumber?.takeIf { it.isNotBlank() }?.let { "S/N $it" }
        ).joinToString(" · ").takeIf { it.isNotBlank() }?.let { "Asset: $it" }
    }
    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            Surface(color = RepairUi.Surface, shadowElevation = 3.dp) {
                Column(
                    modifier = Modifier.fillMaxWidth().statusBarsPadding().padding(horizontal = 6.dp, vertical = 6.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        IconButton(onClick = { navController.popBackStack() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = RepairUi.Ink) }
                        Column(modifier = Modifier.weight(1f)) {
                            Text(headerTitle, style = MaterialTheme.typography.titleMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            headerAsset?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                        }
                        headerTicket?.let {
                            Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                                RepairStatusPill(it.status)
                                RepairPriorityPill(it.priority, compact = true)
                            }
                        }
                        IconButton(onClick = { viewModel.loadDetail(ticketId) }) { Icon(Icons.Filled.Refresh, "Refresh repair ticket", tint = RepairUi.Muted) }
                    }
                }
            }
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when (val current = state) {
            RepairDetailUiState.Idle, RepairDetailUiState.Loading -> Box(Modifier.fillMaxSize().background(RepairUi.Canvas).padding(padding), contentAlignment = Alignment.Center) { RepairLoading("Loading repair details…") }
            is RepairDetailUiState.Error -> Box(Modifier.fillMaxSize().background(RepairUi.Canvas).padding(padding)) {
                RepairErrorPanel(current.message, { viewModel.loadDetail(ticketId) }, { navController.popBackStack() })
            }
            is RepairDetailUiState.Success -> RepairTicketDetailContent(
                modifier = Modifier.fillMaxSize().background(RepairUi.Canvas).padding(padding),
                response = current.response,
                noteText = noteText,
                onNoteTextChange = { noteText = it },
                onAddNote = {
                    if (noteText.isNotBlank()) {
                        val text = noteText.trim()
                        pendingNotes = pendingNotes + text
                        viewModel.submitAction(RepairTicketActionRequest(action = "addNote", ticketId = ticketId, noteText = text))
                        noteText = ""
                    }
                },
                optimisticNotes = pendingNotes,
                onStatusClick = { statusExpanded = true },
                onStatusSelected = { status -> viewModel.submitAction(RepairTicketActionRequest(action = "setStatus", ticketId = ticketId, status = status)) },
                statusExpanded = statusExpanded,
                onStatusDismiss = { statusExpanded = false },
                onCreatePart = { showPartDialog = true },
                onSaveConclusion = { showConclusionDialog = true },
                onPhotoUpload = { current.response.ticket?.ticketCode?.let { navController.navigate("repair_photo_upload/${Uri.encode(it)}") } },
                onTechnicianTools = { navController.navigate("repair_ticket_tools/$ticketId") }
            )
        }
    }
    if (showPartDialog) RepairPartDialog(onDismiss = { showPartDialog = false }) { label, problem, severity ->
        viewModel.submitAction(RepairTicketActionRequest(action = "createPart", ticketId = ticketId, label = label, problemDescription = problem, severity = severity))
        showPartDialog = false
    }
    if (showConclusionDialog) RepairConclusionDialog(onDismiss = { showConclusionDialog = false }) { rootCause, work, outcome, recommendation ->
        viewModel.submitAction(RepairTicketActionRequest(action = "saveConclusion", ticketId = ticketId, rootCause = rootCause, workPerformed = work, finalOutcome = outcome, recommendations = recommendation))
        showConclusionDialog = false
    }
}

@Composable
private fun RepairTicketDetailContent(
    modifier: Modifier,
    response: RepairTicketDetailResponse,
    noteText: String,
    onNoteTextChange: (String) -> Unit,
    onAddNote: () -> Unit,
    optimisticNotes: List<String>,
    onStatusClick: () -> Unit,
    onStatusSelected: (String) -> Unit,
    statusExpanded: Boolean,
    onStatusDismiss: () -> Unit,
    onCreatePart: () -> Unit,
    onSaveConclusion: () -> Unit,
    onPhotoUpload: () -> Unit,
    onTechnicianTools: () -> Unit
) {
    val ticket = response.ticket ?: return
    LazyColumn(
        modifier = modifier,
        contentPadding = PaddingValues(start = 16.dp, top = 14.dp, end = 16.dp, bottom = 96.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        response.linkedCall?.let { call ->
            item {
                RepairSurfaceCard(tonalColor = RepairUi.Surface) {
                    RepairSectionHeading("Linked Call Details", "This repair came from the IT Call workflow.")
                    Text(
                        "${call.ticketCode ?: "IT Call #${call.ticketId}"} · ${call.status ?: "Open"}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = RepairUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                    call.issue?.takeIf { it.isNotBlank() }?.let {
                        Text(it, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
                    }
                }
            }
        }
        response.activeSpare?.let { spare ->
            item {
                RepairSurfaceCard(tonalColor = RepairUi.WarningSoft) {
                    RepairSectionHeading("Temporary spare in use", "Return or unlink it from the technician workspace when this repair is complete.")
                    Text("${spare.itemName ?: "Spare equipment"} · ${spare.serialNumber ?: "No serial"}", style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
                }
            }
        }
        if (response.isTechnician) {
            item {
                RepairSurfaceCard {
                    RepairSectionHeading("Technician Controls")
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Box(modifier = Modifier.weight(1f)) {
                            RepairControlTile(Icons.Filled.Refresh, "Update Status", onStatusClick, modifier = Modifier.fillMaxWidth())
                            DropdownMenu(expanded = statusExpanded, onDismissRequest = onStatusDismiss) {
                                listOf("Waiting", "Diagnosing", "Repairing", "AwaitingParts", "Testing", "Completed", "Unrepairable").forEach { status ->
                                    DropdownMenuItem(text = { Text(formatDetailStatus(status)) }, onClick = { onStatusSelected(status); onStatusDismiss() })
                                }
                            }
                        }
                        RepairControlTile(Icons.Filled.Add, "Add Parts", onCreatePart, modifier = Modifier.weight(1f))
                        RepairControlTile(Icons.Filled.CameraAlt, "Capture Evidence", onPhotoUpload, modifier = Modifier.weight(1f))
                        RepairControlTile(Icons.Filled.Description, "Log Conclusion", onSaveConclusion, modifier = Modifier.weight(1f))
                    }
                    RepairSecondaryButton(onClick = onTechnicianTools, modifier = Modifier.fillMaxWidth()) {
                        Icon(Icons.Filled.Build, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(8.dp))
                        Text("Open full technician workspace")
                    }
                }
            }
        } else {
            item {
                RepairSurfaceCard {
                    RepairActionRow("Add evidence", "Share a photo or document that helps the repair team.", Icons.Filled.CameraAlt, onPhotoUpload)
                }
            }
        }
        item {
            RepairSurfaceCard {
                RepairSectionHeading("Activity & Observations Feed")
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        value = noteText,
                        onValueChange = onNoteTextChange,
                        modifier = Modifier.weight(1f),
                        placeholder = { Text("Add a new note or observation…", color = RepairUi.Muted) },
                        maxLines = 3,
                        shape = RoundedCornerShape(12.dp),
                        colors = repairTextFieldColors()
                    )
                    RepairPrimaryButton(onClick = onAddNote, enabled = noteText.isNotBlank()) { Text("Post") }
                }
                val feed = buildRepairFeed(response, optimisticNotes)
                if (feed.isEmpty()) {
                    Text("No activity yet. Post the first note or observation.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                } else {
                    Spacer(Modifier.height(2.dp))
                    feed.forEachIndexed { index, entry ->
                        RepairFeedRow(time = entry.time, author = entry.author, text = entry.text, pending = entry.pending, isLast = index == feed.lastIndex)
                    }
                }
            }
        }
        response.conclusion?.let { conclusion ->
            item {
                RepairSurfaceCard(tonalColor = RepairUi.SurfaceSubtle) {
                    RepairSectionHeading("Repair conclusion")
                    RepairInfoRow("Root cause", conclusion.rootCause)
                    RepairInfoRow("Work completed", conclusion.workPerformed)
                    RepairInfoRow("Outcome", conclusion.finalOutcome)
                    RepairInfoRow("Recommendation", conclusion.recommendations)
                    conclusion.disposition?.takeIf { it.isNotBlank() }?.let { Text("Disposition · $it", style = MaterialTheme.typography.labelMedium, color = RepairUi.Critical, fontWeight = FontWeight.Bold) }
                }
            }
        }
        item {
            RepairAccordionCard(title = "Parts List (${response.parts.size})") {
                if (response.parts.isEmpty()) {
                    Text("No parts have been added.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                } else {
                    response.parts.forEachIndexed { index, part ->
                        if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Text(part.displayName ?: "Part ${part.partNumber}", style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                                RepairStatusPill(part.status)
                                Text("${part.severity ?: "Medium"} severity · ${part.attachmentCount} evidence", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                            }
                            part.problemDescription?.takeIf { it.isNotBlank() }?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted) }
                        }
                    }
                }
            }
        }
        if (response.attachments.isNotEmpty() || response.partAttachments.isNotEmpty()) {
            item {
                RepairAccordionCard(title = "Evidence (${response.attachments.size + response.partAttachments.size})") {
                    (response.attachments.map { it.fileName ?: "Ticket evidence" } + response.partAttachments.map { it.fileName ?: "Part evidence" }).forEachIndexed { index, label ->
                        if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                        Text(label, style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink)
                    }
                }
            }
        }
        item {
            RepairAccordionCard(title = "Complete Repair History") {
                if (response.history.isEmpty()) {
                    Text("No workflow history is available yet.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                } else {
                    response.history.take(30).forEachIndexed { index, entry ->
                        if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            val heading = listOfNotNull(relativeRepairTime(entry.changedAt).takeIf { it.isNotBlank() }, entry.changedByName?.takeIf { it.isNotBlank() }).joinToString(" · ")
                            if (heading.isNotBlank()) Text(heading, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                            Text("${entry.fieldName ?: "Updated"}: ${entry.newValue ?: entry.note ?: ""}", style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink)
                        }
                    }
                }
            }
        }
        item { Spacer(Modifier.height(18.dp)) }
    }
}

private data class RepairFeedEntry(val sortKey: Long, val time: String, val author: String, val text: String, val pending: Boolean = false)

private fun buildRepairFeed(response: RepairTicketDetailResponse, optimisticNotes: List<String> = emptyList()): List<RepairFeedEntry> {
    val entries = mutableListOf<RepairFeedEntry>()
    optimisticNotes.forEach { text ->
        entries += RepairFeedEntry(sortKey = Long.MAX_VALUE, time = "just now", author = "You", text = text, pending = true)
    }
    response.ticket?.problem?.takeIf { it.isNotBlank() }?.let { problem ->
        val raw = response.ticket.dateReceived ?: response.ticket.createdAt
        entries += RepairFeedEntry(parseRepairInstant(raw) ?: 0L, relativeRepairTime(raw), "Request", problem)
    }
    response.notes.forEach { note ->
        entries += RepairFeedEntry(
            sortKey = parseRepairInstant(note.createdAt) ?: 0L,
            time = relativeRepairTime(note.createdAt),
            author = note.createdByName?.takeIf { it.isNotBlank() } ?: note.noteType?.takeIf { it.isNotBlank() } ?: "Update",
            text = note.noteText.orEmpty()
        )
    }
    response.observations.forEach { obs ->
        entries += RepairFeedEntry(
            sortKey = parseRepairInstant(obs.createdAt) ?: 0L,
            time = relativeRepairTime(obs.createdAt),
            author = obs.createdByName?.takeIf { it.isNotBlank() } ?: "Observation",
            text = obs.text.orEmpty()
        )
    }
    return entries.filter { it.text.isNotBlank() }.sortedByDescending { it.sortKey }
}

private fun parseRepairInstant(raw: String?): Long? {
    if (raw.isNullOrBlank()) return null
    val patterns = listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSSXXX",
        "yyyy-MM-dd'T'HH:mm:ssXXX",
        "yyyy-MM-dd'T'HH:mm:ss.SSS",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss"
    )
    for (pattern in patterns) {
        val parsed = runCatching { SimpleDateFormat(pattern, Locale.US).parse(raw)?.time }.getOrNull()
        if (parsed != null) return parsed
    }
    return null
}

private fun relativeRepairTime(raw: String?): String {
    val time = parseRepairInstant(raw) ?: return ""
    val diff = System.currentTimeMillis() - time
    if (diff < 0) return "just now"
    val minutes = diff / 60_000
    val hours = minutes / 60
    val days = hours / 24
    return when {
        minutes < 1 -> "just now"
        minutes < 60 -> "${minutes}m ago"
        hours < 24 -> "${hours}h ago"
        days < 7 -> "${days}d ago"
        else -> SimpleDateFormat("MMM d", Locale.US).format(Date(time))
    }
}

@Composable
private fun RepairControlTile(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, onClick: () -> Unit, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(14.dp))
            .background(RepairUi.SurfaceSubtle)
            .clickable(onClick = onClick)
            .padding(vertical = 12.dp, horizontal = 6.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Box(
            modifier = Modifier.size(38.dp).clip(RoundedCornerShape(12.dp)).background(RepairUi.Surface),
            contentAlignment = Alignment.Center
        ) {
            Icon(icon, contentDescription = null, tint = RepairUi.Brand, modifier = Modifier.size(20.dp))
        }
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = RepairUi.Ink,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
            maxLines = 2,
            minLines = 2
        )
    }
}

@Composable
private fun RepairFeedRow(time: String, author: String, text: String, pending: Boolean, isLast: Boolean) {
    Row(modifier = Modifier.height(IntrinsicSize.Min), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Column(modifier = Modifier.width(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.padding(top = 4.dp).size(9.dp).clip(CircleShape).background(if (pending) RepairUi.Muted else RepairUi.Brand))
            if (!isLast) Box(modifier = Modifier.width(2.dp).weight(1f).background(RepairUi.Divider))
        }
        Column(
            modifier = Modifier.weight(1f).padding(bottom = if (isLast) 0.dp else 14.dp),
            verticalArrangement = Arrangement.spacedBy(2.dp)
        ) {
            val label = if (pending) "sending…" else time
            val meta = listOf(label.takeIf { it.isNotBlank() }, "[$author]").filterNotNull().joinToString(" · ")
            Text(meta, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, fontWeight = FontWeight.SemiBold)
            Text(text, style = MaterialTheme.typography.bodySmall, color = if (pending) RepairUi.Muted else RepairUi.Ink)
        }
    }
}

@Composable
private fun RepairAccordionCard(title: String, initiallyExpanded: Boolean = false, content: @Composable ColumnScope.() -> Unit) {
    var expanded by remember { mutableStateOf(initiallyExpanded) }
    val chevronRotation by animateFloatAsState(targetValue = if (expanded) 180f else 0f, label = "accordionChevron")
    RepairSurfaceCard {
        Row(
            modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(8.dp)).clickable { expanded = !expanded },
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(title, style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
            Icon(
                Icons.Filled.ExpandMore,
                contentDescription = if (expanded) "Collapse" else "Expand",
                tint = RepairUi.Muted,
                modifier = Modifier.rotate(chevronRotation)
            )
        }
        AnimatedVisibility(
            visible = expanded,
            enter = expandVertically() + fadeIn(),
            exit = shrinkVertically() + fadeOut()
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                HorizontalDivider(color = RepairUi.Divider)
                content()
            }
        }
    }
}

private fun formatDetailStatus(status: String): String = when (status) {
    "AwaitingParts" -> "Awaiting parts"
    else -> status
}

@Composable
private fun RepairPartDialog(onDismiss: () -> Unit, onSave: (String, String, String) -> Unit) {
    var label by remember { mutableStateOf("") }
    var problem by remember { mutableStateOf("") }
    var severity by remember { mutableStateOf("Medium") }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Add repair part", color = RepairUi.Ink, fontWeight = FontWeight.Bold) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(label, { label = it }, label = { Text("Part label") }, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                OutlinedTextField(problem, { problem = it }, label = { Text("Problem description") }, minLines = 2, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                OutlinedTextField(severity, { severity = it }, label = { Text("Severity: Low, Medium, High, Critical") }, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
            }
        },
        confirmButton = { Button(onClick = { onSave(label, problem, severity) }, colors = ButtonDefaults.buttonColors(containerColor = RepairUi.Brand)) { Text("Add part") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairConclusionDialog(onDismiss: () -> Unit, onSave: (String, String, String, String) -> Unit) {
    var rootCause by remember { mutableStateOf("") }
    var work by remember { mutableStateOf("") }
    var outcome by remember { mutableStateOf("") }
    var recommendation by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Save repair conclusion", color = RepairUi.Ink, fontWeight = FontWeight.Bold) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(rootCause, { rootCause = it }, label = { Text("Root cause") }, minLines = 2, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                OutlinedTextField(work, { work = it }, label = { Text("Work performed") }, minLines = 2, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                OutlinedTextField(outcome, { outcome = it }, label = { Text("Final outcome") }, minLines = 2, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                OutlinedTextField(recommendation, { recommendation = it }, label = { Text("Recommendations") }, minLines = 2, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
            }
        },
        confirmButton = { Button(onClick = { onSave(rootCause, work, outcome, recommendation) }, colors = ButtonDefaults.buttonColors(containerColor = RepairUi.Brand)) { Text("Save conclusion") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}


