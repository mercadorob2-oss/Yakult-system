package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.api.CallTicketListItem
import com.example.yakultscanner.ui.components.ItcmMetaText
import com.example.yakultscanner.ui.components.ItcmPriorityChip
import com.example.yakultscanner.ui.components.ItcmSectionHeading
import com.example.yakultscanner.ui.components.ItcmStatusChip
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.ui.components.itcmStatusColor
import com.example.yakultscanner.utils.generateCallMonitoringPdf
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.CallTicketsUiState
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private enum class TicketWorkspace(val label: String, val apiScope: String) {
    Inbox("Inbox", "inbox"),
    MyWork("My Work", "mine"),
    Resolved("Resolved", "resolved")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ItcmQueueScreen(
    navController: NavController,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val uiState by viewModel.ticketsState.collectAsState()
    val context = androidx.compose.ui.platform.LocalContext.current

    var workspaceName by rememberSaveable { mutableStateOf(TicketWorkspace.Inbox.name) }
    val workspace = TicketWorkspace.entries.firstOrNull { it.name == workspaceName } ?: TicketWorkspace.Inbox
    var searchQuery by rememberSaveable { mutableStateOf("") }
    var selectedPriority by rememberSaveable { mutableStateOf<String?>(null) }
    var selectedIssueType by rememberSaveable { mutableStateOf<String?>(null) }
    var showFilters by rememberSaveable { mutableStateOf(false) }
    var currentPage by rememberSaveable { mutableIntStateOf(1) }

    fun loadPage(page: Int) {
        currentPage = page.coerceAtLeast(1)
        viewModel.loadTickets(
            scope = workspace.apiScope,
            search = searchQuery.takeIf { it.isNotBlank() },
            priority = selectedPriority,
            issueType = selectedIssueType,
            page = currentPage,
            refresh = true
        )
    }

    LaunchedEffect(workspace, searchQuery, selectedPriority, selectedIssueType) {
        if (searchQuery.isNotBlank()) delay(350)
        loadPage(1)
    }

    Scaffold(
        containerColor = ItcmUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("IT Call Monitoring", fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                        Text("Work queue", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ItcmUi.Ink)
                    }
                },
                actions = {
                    val success = uiState as? CallTicketsUiState.Success
                    IconButton(
                        onClick = {
                            success?.let {
                                generateCallMonitoringPdf(
                                    context = context,
                                    tickets = it.response.tickets,
                                    tabTitle = workspace.label,
                                    searchQuery = searchQuery,
                                    totalCount = it.response.totalCount,
                                    page = it.response.page,
                                    pageSize = it.response.pageSize
                                )
                            }
                        },
                        enabled = success != null
                    ) {
                        Icon(Icons.Filled.PictureAsPdf, contentDescription = "Export queue as PDF", tint = ItcmUi.Ink)
                    }
                    IconButton(onClick = {
                        loadPage(1)
                        scope.launch { snackbarHostState.showSnackbar("Ticket queue refreshed") }
                    }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh tickets", tint = ItcmUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ItcmUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) }
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(ItcmUi.Canvas)
                .padding(padding)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 16.dp)
            ) {
                Spacer(Modifier.height(12.dp))
                ItcmWorkspaceTabs(
                    selected = workspace,
                    onSelected = { workspaceName = it.name }
                )
                Spacer(Modifier.height(14.dp))
                OutlinedTextField(
                    value = searchQuery,
                    onValueChange = { searchQuery = it },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    placeholder = { Text("Search ticket code, issue, caller…", color = ItcmUi.Muted) },
                    leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null, tint = ItcmUi.Muted) },
                    trailingIcon = {
                        IconButton(onClick = { showFilters = true }) {
                            Icon(Icons.Filled.FilterList, contentDescription = "Open ticket filters", tint = ItcmUi.Ink)
                        }
                    },
                    shape = RoundedCornerShape(16.dp)
                )
                if (selectedPriority != null || selectedIssueType != null) {
                    Spacer(Modifier.height(8.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        selectedPriority?.let { ItcmPriorityChip(it) }
                        selectedIssueType?.let {
                            Text(
                                text = it,
                                modifier = Modifier
                                    .clip(RoundedCornerShape(999.dp))
                                    .background(ItcmUi.SurfaceSubtle)
                                    .padding(horizontal = 10.dp, vertical = 6.dp),
                                style = MaterialTheme.typography.labelSmall,
                                color = ItcmUi.Muted,
                                fontWeight = FontWeight.SemiBold
                            )
                        }
                    }
                }
                Spacer(Modifier.height(14.dp))

                when (val state = uiState) {
                    is CallTicketsUiState.Loading -> ItcmQueueLoading()
                    is CallTicketsUiState.Error -> ItcmQueueError(
                        message = state.message,
                        onRetry = { loadPage(1) }
                    )
                    is CallTicketsUiState.Success -> {
                        val tickets = state.response.tickets
                        if (tickets.isEmpty()) {
                            ItcmQueueEmpty(workspace = workspace, hasSearchOrFilters = searchQuery.isNotBlank() || selectedPriority != null || selectedIssueType != null)
                        } else {
                            LazyColumn(
                                modifier = Modifier.fillMaxSize(),
                                verticalArrangement = Arrangement.spacedBy(10.dp),
                                contentPadding = PaddingValues(bottom = 24.dp)
                            ) {
                                item {
                                    ItcmQueueSummary(
                                        workspace = workspace,
                                        totalCount = state.response.totalCount,
                                        currentPage = currentPage
                                    )
                                }
                                items(tickets, key = { it.ticketId }) { ticket ->
                                    ItcmTicketQueueCard(
                                        ticket = ticket,
                                        onClick = { navController.navigate("call_ticket_detail/${ticket.ticketId}") }
                                    )
                                }
                                item {
                                    ItcmCompactPager(
                                        currentPage = currentPage,
                                        totalCount = state.response.totalCount,
                                        pageSize = state.response.pageSize.takeIf { it > 0 } ?: 25,
                                        onPrevious = { loadPage(currentPage - 1) },
                                        onNext = { loadPage(currentPage + 1) }
                                    )
                                }
                            }
                        }
                    }
                }
            }

            ExtendedFloatingActionButton(
                onClick = { navController.navigate("call_ticket_create") },
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(end = 16.dp, bottom = 16.dp),
                containerColor = ItcmUi.Brand,
                contentColor = ItcmUi.Surface,
                icon = { Icon(Icons.Filled.Add, contentDescription = null) },
                text = { Text("New ticket", fontWeight = FontWeight.Bold) }
            )
        }
    }

    if (showFilters) {
        ItcmQueueFilterSheet(
            selectedPriority = selectedPriority,
            selectedIssueType = selectedIssueType,
            onPrioritySelected = { selectedPriority = if (selectedPriority == it) null else it },
            onIssueTypeSelected = { selectedIssueType = if (selectedIssueType == it) null else it },
            onClear = {
                selectedPriority = null
                selectedIssueType = null
            },
            onDismiss = { showFilters = false }
        )
    }
}

@Composable
private fun ItcmWorkspaceTabs(
    selected: TicketWorkspace,
    onSelected: (TicketWorkspace) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(ItcmUi.SurfaceSubtle)
            .padding(4.dp),
        horizontalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        TicketWorkspace.entries.forEach { workspace ->
            val isSelected = workspace == selected
            Box(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(12.dp))
                    .background(if (isSelected) ItcmUi.Surface else ItcmUi.SurfaceSubtle)
                    .clickable { onSelected(workspace) }
                    .padding(vertical = 10.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = workspace.label,
                    style = MaterialTheme.typography.labelMedium,
                    color = if (isSelected) ItcmUi.Ink else ItcmUi.Muted,
                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Medium
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmQueueFilterSheet(
    selectedPriority: String?,
    selectedIssueType: String?,
    onPrioritySelected: (String) -> Unit,
    onIssueTypeSelected: (String) -> Unit,
    onClear: () -> Unit,
    onDismiss: () -> Unit
) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = ItcmUi.Surface,
        contentColor = ItcmUi.Ink
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                ItcmSectionHeading("Filter tickets", "Narrow the active queue without losing your workspace.")
                androidx.compose.material3.TextButton(onClick = onClear) { Text("Clear", color = ItcmUi.Brand) }
            }
            FilterChoiceRow(
                label = "Priority",
                values = listOf("Low", "Medium", "High", "Critical"),
                selected = selectedPriority,
                onClick = onPrioritySelected
            )
            FilterChoiceRow(
                label = "Issue type",
                values = listOf("Hardware", "Software", "Network", "Account", "Peripheral", "Setup", "Other"),
                selected = selectedIssueType,
                onClick = onIssueTypeSelected
            )
            Button(
                onClick = onDismiss,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) { Text("Show tickets", fontWeight = FontWeight.Bold) }
            Spacer(Modifier.height(18.dp))
        }
    }
}

@Composable
private fun FilterChoiceRow(
    label: String,
    values: List<String>,
    selected: String?,
    onClick: (String) -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(label, style = MaterialTheme.typography.labelLarge, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(values) { value ->
                FilterChip(
                    selected = selected == value,
                    onClick = { onClick(value) },
                    label = { Text(value) }
                )
            }
        }
    }
}

@Composable
private fun ItcmQueueSummary(workspace: TicketWorkspace, totalCount: Int, currentPage: Int) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(
            text = workspace.label,
            style = MaterialTheme.typography.titleMedium,
            color = ItcmUi.Ink,
            fontWeight = FontWeight.Bold
        )
        Text(
            text = "$totalCount ticket${if (totalCount == 1) "" else "s"} · Page $currentPage",
            style = MaterialTheme.typography.labelSmall,
            color = ItcmUi.Muted
        )
    }
}

@Composable
private fun ItcmTicketQueueCard(ticket: CallTicketListItem, onClick: () -> Unit) {
    val assignee = ticket.responsiblePerson?.takeIf { it.isNotBlank() } ?: "Unassigned"
    val location = listOf(ticket.company, ticket.department, ticket.branch)
        .filterNotNull()
        .filter { it.isNotBlank() }
        .joinToString(" · ")
        .ifBlank { "Location unavailable" }
    val needsAttention = ticket.ticketAgeDays > 3 && ticket.status !in listOf("Solved", "Resolved (Temporary)", "Closed")

    ItcmSurfaceCard(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(ticket.ticketCode.orEmpty(), style = MaterialTheme.typography.labelMedium, color = ItcmUi.Brand, fontWeight = FontWeight.Bold)
                Text(
                    text = ticket.issue.orEmpty().ifBlank { "No issue summary" },
                    style = MaterialTheme.typography.bodyLarge,
                    color = ItcmUi.Ink,
                    fontWeight = FontWeight.SemiBold,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Spacer(Modifier.width(10.dp))
            ItcmStatusChip(ticket.status)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
            ItcmPriorityChip(ticket.priority)
            ticket.issueType?.takeIf { it.isNotBlank() }?.let {
                Text(it, style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
            }
        }
        HorizontalDivider(color = ItcmUi.Divider)
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Bottom) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(5.dp)) {
                    Icon(Icons.Filled.Person, contentDescription = null, modifier = Modifier.size(14.dp), tint = ItcmUi.Muted)
                    ItcmMetaText(ticket.callerName.orEmpty().ifBlank { "Requester unavailable" })
                }
                ItcmMetaText(location)
                ItcmMetaText("Assigned to $assignee")
            }
            Text(
                text = "${ticket.ticketAgeDays}d",
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Bold,
                color = if (needsAttention) ItcmUi.Critical else ItcmUi.Muted
            )
        }
    }
}

@Composable
private fun ItcmCompactPager(
    currentPage: Int,
    totalCount: Int,
    pageSize: Int,
    onPrevious: () -> Unit,
    onNext: () -> Unit
) {
    val totalPages = maxOf(1, (totalCount + pageSize - 1) / pageSize)
    val hasPrevious = currentPage > 1
    val hasNext = currentPage < totalPages
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 4.dp, bottom = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        OutlinedButton(onClick = onPrevious, enabled = hasPrevious, modifier = Modifier.weight(1f)) { Text("Previous") }
        Text("$currentPage / $totalPages", style = MaterialTheme.typography.labelMedium, color = ItcmUi.Muted)
        Button(onClick = onNext, enabled = hasNext, modifier = Modifier.weight(1f)) { Text("Next") }
    }
}

@Composable
private fun ItcmQueueLoading() {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(10.dp),
        contentPadding = PaddingValues(bottom = 24.dp)
    ) {
        items(4) {
            ItcmSurfaceCard {
                Box(Modifier.fillMaxWidth().height(14.dp).clip(RoundedCornerShape(8.dp)).background(ItcmUi.SurfaceSubtle))
                Box(Modifier.fillMaxWidth(0.72f).height(18.dp).clip(RoundedCornerShape(8.dp)).background(ItcmUi.SurfaceSubtle))
                Box(Modifier.fillMaxWidth(0.45f).height(12.dp).clip(RoundedCornerShape(8.dp)).background(ItcmUi.SurfaceSubtle))
            }
        }
    }
}

@Composable
private fun ItcmQueueError(message: String, onRetry: () -> Unit) {
    Box(modifier = Modifier.fillMaxSize().padding(28.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Couldn’t load tickets", style = MaterialTheme.typography.titleMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
            Button(onClick = onRetry) { Text("Try again") }
        }
    }
}

@Composable
private fun ItcmQueueEmpty(workspace: TicketWorkspace, hasSearchOrFilters: Boolean) {
    val message = when {
        hasSearchOrFilters -> "Try clearing or changing your search and filters."
        workspace == TicketWorkspace.Inbox -> "No unassigned active tickets need triage right now."
        workspace == TicketWorkspace.MyWork -> "You have no active assigned tickets right now."
        else -> "No completed tickets were found."
    }
    Box(modifier = Modifier.fillMaxSize().padding(28.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Your queue is clear", style = MaterialTheme.typography.titleMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
        }
    }
}