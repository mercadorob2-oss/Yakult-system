package com.example.yakultscanner.ui.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.FiberManualRecord
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Button
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SecondaryTabRow
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Tab
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
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.api.CallTicketListItem
import androidx.compose.ui.platform.LocalContext
import com.example.yakultscanner.utils.generateCallMonitoringPdf
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.CallTicketsUiState
import kotlinx.coroutines.launch

// ─── API-backed Call Monitoring Screen ──────────────────────────────────────

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CallMonitoringScreen(
    navController: NavController,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val uiState by viewModel.ticketsState.collectAsState()
    val context = LocalContext.current

    var searchQuery by remember { mutableStateOf("") }
    var selectedTab by remember { mutableIntStateOf(0) }
    var showFilters by remember { mutableStateOf(false) }
    var selectedPriority by remember { mutableStateOf<String?>(null) }
    var selectedIssueType by remember { mutableStateOf<String?>(null) }
    var currentPage by remember { mutableIntStateOf(1) }

    val tabLabels = listOf("Pending", "Solved", "All")
    val statusFilter = when (selectedTab) {
        0 -> "pending"
        1 -> "solved"
        else -> null
    }

    // Load tickets on init and when filters change
    LaunchedEffect(statusFilter, selectedPriority, selectedIssueType) {
        currentPage = 1
        viewModel.loadTickets(
            status = statusFilter,
            priority = selectedPriority,
            issueType = selectedIssueType,
            page = 1,
            refresh = true
        )
    }

    // Debounced search
    LaunchedEffect(searchQuery) {
        currentPage = 1
        if (searchQuery.isBlank()) {
            viewModel.loadTickets(
                status = statusFilter,
                priority = selectedPriority,
                issueType = selectedIssueType,
                page = 1,
                refresh = true
            )
        } else {
            // debounce 500ms
            kotlinx.coroutines.delay(500)
            viewModel.loadTickets(
                status = statusFilter,
                search = searchQuery,
                priority = selectedPriority,
                issueType = selectedIssueType,
                page = 1,
                refresh = true
            )
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("IT Call Monitoring") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    titleContentColor = MaterialTheme.colorScheme.onPrimaryContainer
                ),
                actions = {
                    val successState = uiState as? CallTicketsUiState.Success
                    IconButton(
                        onClick = {
                            successState?.let { s ->
                                generateCallMonitoringPdf(
                                    context, s.response.tickets,
                                    tabLabels[selectedTab], searchQuery,
                                    s.response.totalCount, s.response.page, s.response.pageSize
                                )
                            }
                        },
                        enabled = successState != null
                    ) {
                        Icon(Icons.Filled.PictureAsPdf, contentDescription = "Export PDF")
                    }
                    IconButton(onClick = {
                        currentPage = 1
                        scope.launch {
                            viewModel.loadTickets(
                                status = statusFilter,
                                search = searchQuery.takeIf { it.isNotBlank() },
                                priority = selectedPriority,
                                issueType = selectedIssueType,
                                page = 1,
                                refresh = true
                            )
                            snackbarHostState.showSnackbar("Tickets refreshed")
                        }
                    }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh")
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier.padding(bottom = 80.dp)) }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 16.dp)
        ) {
            Spacer(Modifier.height(12.dp))
            OutlinedTextField(
                value = searchQuery,
                onValueChange = { searchQuery = it },
                placeholder = { Text("Search tickets...") },
                leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null) },
                trailingIcon = {
                    IconButton(onClick = { showFilters = !showFilters }) {
                        Icon(Icons.Filled.FilterList, contentDescription = "Filter")
                    }
                },
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                singleLine = true,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                keyboardActions = KeyboardActions(onSearch = { })
            )

            // Static filter options — covers all possible values regardless of which page is loaded
            val priorities = listOf("Low", "Medium", "High", "Critical")
            val issueTypes = listOf("Hardware", "Software", "Network", "Account", "Peripheral", "Setup", "Other")

            AnimatedVisibility(
                visible = showFilters,
                enter = expandVertically() + fadeIn(),
                exit = shrinkVertically() + fadeOut()
            ) {
                Column(modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)) {
                    Text("Priority", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                    FilterChipRow(
                        items = priorities,
                        selected = selectedPriority,
                        onSelect = { selectedPriority = if (selectedPriority == it) null else it }
                    )
                    Spacer(Modifier.height(6.dp))
                    Text("Issue Type", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                    FilterChipRow(
                        items = issueTypes,
                        selected = selectedIssueType,
                        onSelect = { selectedIssueType = if (selectedIssueType == it) null else it }
                    )
                }
            }

            SecondaryTabRow(selectedTabIndex = selectedTab) {
                tabLabels.forEachIndexed { index, label ->
                    Tab(
                        selected = selectedTab == index,
                        onClick = { selectedTab = index },
                        text = { Text(label) }
                    )
                }
            }

            Spacer(Modifier.height(8.dp))

            Box(modifier = Modifier.weight(1f)) {
                when (val state = uiState) {
                    is CallTicketsUiState.Loading -> {
                        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                androidx.compose.material3.CircularProgressIndicator()
                                Text("Loading tickets...", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                    is CallTicketsUiState.Error -> {
                        Box(Modifier.fillMaxSize().padding(32.dp), contentAlignment = Alignment.Center) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                Icon(
                                    imageVector = Icons.Filled.Search,
                                    contentDescription = null,
                                    modifier = Modifier.size(48.dp),
                                    tint = MaterialTheme.colorScheme.error.copy(alpha = 0.5f)
                                )
                                Text("Error loading tickets", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.error)
                                Text(state.message, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                    is CallTicketsUiState.Success -> {
                        val ticketList = state.response.tickets
                        if (ticketList.isEmpty()) {
                            Box(Modifier.fillMaxSize().padding(32.dp), contentAlignment = Alignment.Center) {
                                Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                    Icon(
                                        imageVector = Icons.Filled.Search,
                                        contentDescription = null,
                                        modifier = Modifier.size(48.dp),
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f)
                                    )
                                    Text("No tickets found", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    Text("Try adjusting your search or filters", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f))
                                }
                            }
                        } else {
                            LazyColumn(
                                modifier = Modifier.fillMaxSize(),
                                verticalArrangement = Arrangement.spacedBy(10.dp),
                                contentPadding = PaddingValues(bottom = 96.dp)
                            ) {
                                items(ticketList, key = { it.ticketId }) { ticket ->
                                    TicketListCard(ticket = ticket, onClick = {
                                        navController.navigate("call_ticket_detail/${ticket.ticketId}")
                                    })
                                }
                                item {
                                    val ps = state.response.pageSize.takeIf { it > 0 } ?: 25
                                    TicketPaginationCard(
                                        currentPage = currentPage,
                                        totalCount = state.response.totalCount,
                                        pageSize = ps,
                                        onPrevious = {
                                            if (currentPage > 1) {
                                                currentPage--
                                                scope.launch {
                                                    viewModel.loadTickets(
                                                        status = statusFilter,
                                                        search = searchQuery.takeIf { it.isNotBlank() },
                                                        priority = selectedPriority,
                                                        issueType = selectedIssueType,
                                                        page = currentPage,
                                                        refresh = true
                                                    )
                                                }
                                            }
                                        },
                                        onNext = {
                                            currentPage++
                                            scope.launch {
                                                viewModel.loadTickets(
                                                    status = statusFilter,
                                                    search = searchQuery.takeIf { it.isNotBlank() },
                                                    priority = selectedPriority,
                                                    issueType = selectedIssueType,
                                                    page = currentPage,
                                                    refresh = true
                                                )
                                            }
                                        }
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }

        FloatingActionButton(
            onClick = { navController.navigate("call_ticket_create") },
            containerColor = MaterialTheme.colorScheme.primary,
            shape = RoundedCornerShape(16.dp),
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(end = 16.dp, bottom = 88.dp)
        ) {
            Icon(Icons.Filled.Add, contentDescription = "New Ticket")
        }
        } // end Box
    }
}

@Composable
private fun FilterChipRow(
    items: List<String>,
    selected: String?,
    onSelect: (String) -> Unit
) {
    Row(
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        modifier = Modifier.fillMaxWidth().padding(top = 4.dp)
    ) {
        items.forEach { item ->
            val isSelected = selected == item
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(20.dp))
                    .background(
                        if (isSelected) MaterialTheme.colorScheme.primaryContainer
                        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                    )
                    .clickable { onSelect(item) }
                    .padding(horizontal = 14.dp, vertical = 6.dp)
            ) {
                Text(
                    text = item,
                    style = MaterialTheme.typography.labelSmall,
                    color = if (isSelected) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurfaceVariant,
                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal
                )
            }
        }
    }
}

@Composable
private fun TicketListCard(ticket: CallTicketListItem, onClick: () -> Unit) {
    val priorityColor = when (ticket.priority) {
        "Critical" -> Color(0xFFC62828)
        "High" -> Color(0xFFE28A22)
        "Medium" -> Color(0xFF1565C0)
        else -> Color(0xFF2E7D32)
    }
    val statusColor = when (ticket.status) {
        "Pending" -> Color(0xFFE28A22)
        "In Progress", "Reopened" -> Color(0xFF1565C0)
        "Escalated" -> Color(0xFF7B1FA2)
        "Solved" -> Color(0xFF2E7D32)
        "Resolved (Temporary)" -> Color(0xFF00838F)
        "Closed" -> Color(0xFF455A64)
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }

    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = ticket.ticketCode ?: "",
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary
                )
                StatusBadge(text = ticket.status ?: "", color = statusColor)
            }

            Text(
                text = ticket.issue ?: "",
                style = MaterialTheme.typography.bodyMedium,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                color = MaterialTheme.colorScheme.onSurface
            )

            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Box(
                    modifier = Modifier.size(8.dp).clip(CircleShape).background(priorityColor)
                )
                Text(
                    text = ticket.priority ?: "",
                    style = MaterialTheme.typography.labelSmall,
                    color = priorityColor
                )
                Text("·", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(
                    text = ticket.issueType ?: "",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))

            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text("Caller: ${ticket.callerName}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text("Assigned: ${ticket.responsiblePerson?.takeIf { it.isNotBlank() } ?: "Unassigned"}", style = MaterialTheme.typography.labelSmall, color = if (ticket.responsiblePerson.isNullOrBlank()) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant)
                    Text("${ticket.department} · ${ticket.branch}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f))
                }
                Text(
                    text = "${ticket.ticketAgeDays}d",
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = if (ticket.ticketAgeDays > 3 && ticket.status != "Solved") Color(0xFFC62828) else MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun TicketPaginationCard(
    currentPage: Int,
    totalCount: Int,
    pageSize: Int,
    onPrevious: () -> Unit,
    onNext: () -> Unit
) {
    val hasPrevious = currentPage > 1
    val hasNext = (currentPage * pageSize) < totalCount
    val totalPages = if (totalCount <= 0) 1 else ((totalCount - 1) / pageSize) + 1
    val fromItem = if (totalCount == 0) 0 else ((currentPage - 1) * pageSize) + 1
    val toItem = minOf(totalCount, currentPage * pageSize)

    Card(
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = "Page $currentPage of $totalPages",
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = if (totalCount == 0) "No tickets" else "Showing $fromItem\u2013$toItem of $totalCount tickets",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(
                    onClick = onPrevious,
                    enabled = hasPrevious,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(10.dp)
                ) { Text("Prev Page") }
                Button(
                    onClick = onNext,
                    enabled = hasNext,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(10.dp)
                ) { Text("Next Page") }
            }
        }
    }
}

@Composable
internal fun StatusBadge(text: String, color: Color) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(10.dp))
            .background(color.copy(alpha = 0.12f))
            .padding(horizontal = 10.dp, vertical = 4.dp)
    ) {
        Text(text = text, style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = color)
    }
}
