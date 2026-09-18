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
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import com.example.yakultscanner.utils.SignOffParity
import com.example.yakultscanner.utils.TicketSignOffBundle
import com.example.yakultscanner.utils.generateTicketPdf
import androidx.compose.material3.TextButton
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallTicketDetailResponse
import com.example.yakultscanner.api.CallTicketHistoryDto
import com.example.yakultscanner.api.CallTicketNoteDto
import com.example.yakultscanner.viewmodels.ActionUiState
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.TicketDetailUiState
import kotlinx.coroutines.launch


@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TicketDetailScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val detailState by viewModel.detailState.collectAsState()
    val actionState by viewModel.actionState.collectAsState()

    var showAddNote by remember { mutableStateOf(false) }
    var newNoteText by remember { mutableStateOf("") }
    var showStatusDialog by remember { mutableStateOf(false) }
    var selectedStatus by remember { mutableStateOf("") }
    var showReopenDialog by remember { mutableStateOf(false) }
    var reopenNote by remember { mutableStateOf("") }
    var showPriorityDialog by remember { mutableStateOf(false) }
    var selectedPriority by remember { mutableStateOf("") }
    var legacyExportLoading by remember { mutableStateOf(false) }
    var legacyPhotoPrompt by remember { mutableStateOf<TicketSignOffBundle?>(null) }
    val context = LocalContext.current

    LaunchedEffect(ticketId) {
        viewModel.loadTicketDetail(ticketId)
    }

    // Observe action state for snackbar
    LaunchedEffect(actionState) {
        when (val state = actionState) {
            is ActionUiState.Success -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetActionState()
                // Refresh detail to show new note/status
                viewModel.loadTicketDetail(ticketId)
            }
            is ActionUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetActionState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    val headerTicket = (detailState as? TicketDetailUiState.Success)?.response?.ticket
                    Text(headerTicket?.ticketCode ?: "Ticket Details")
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    val ticketData = (detailState as? TicketDetailUiState.Success)?.response
                    IconButton(
                        onClick = {
                            ticketData?.ticket?.let {
                                legacyExportLoading = true
                                scope.launch {
                                    val bundle = viewModel.loadSignOffBundleNow(ticketId)
                                    legacyExportLoading = false
                                    val data = (viewModel.detailState.value as? TicketDetailUiState.Success)?.response
                                    val ct = data?.ticket
                                    if (data != null && ct != null) {
                                        val eligible = bundle != null &&
                                            SignOffParity.isSignOffEligible(ct.status, bundle.visits)
                                        if (bundle != null && eligible && bundle.totalPhotoCount == 0) {
                                            legacyPhotoPrompt = bundle
                                        } else {
                                            generateTicketPdf(
                                                context, ct, data.notes, data.history,
                                                signOff = if (eligible) bundle else null
                                            )
                                        }
                                    }
                                }
                            }
                        },
                        enabled = ticketData != null && !legacyExportLoading
                    ) {
                        Icon(Icons.Filled.PictureAsPdf, contentDescription = "Export PDF")
                    }
                    IconButton(onClick = { viewModel.loadTicketDetail(ticketId) }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    titleContentColor = MaterialTheme.colorScheme.onPrimaryContainer
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier.padding(bottom = 80.dp)) }
    ) { padding ->
        when (val state = detailState) {
            is TicketDetailUiState.Loading -> {
                Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                    androidx.compose.material3.CircularProgressIndicator()
                }
            }
            is TicketDetailUiState.Error -> {
                Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Text("Error loading ticket", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.error)
                        Text(state.message, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        OutlinedButton(onClick = { viewModel.loadTicketDetail(ticketId) }) {
                            Text("Retry")
                        }
                    }
                }
            }
            is TicketDetailUiState.Success -> {
                val detail = state.response
                val ticket = detail.ticket
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(horizontal = 16.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    Spacer(Modifier.height(12.dp))

                    // Header Card
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(16.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f))
                    ) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                Text(
                                    text = ticket?.ticketCode ?: "",
                                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                    color = MaterialTheme.colorScheme.primary
                                )
                                StatusBadge(
                                    text = ticket?.status ?: "",
                                    color = when (ticket?.status) {
                                        "Pending" -> Color(0xFFE28A22)
                                        "In Progress", "Reopened" -> Color(0xFF1565C0)
                                        "Escalated" -> Color(0xFF7B1FA2)
                                        "Solved" -> Color(0xFF2E7D32)
                                        "Resolved (Temporary)" -> Color(0xFF00838F)
                                        "Closed" -> Color(0xFF455A64)
                                        else -> MaterialTheme.colorScheme.onSurfaceVariant
                                    }
                                )
                            }

                            InfoRow(label = "Company", value = ticket?.company)
                            InfoRow(label = "Caller", value = ticket?.callerName)
                            InfoRow(label = "Department", value = ticket?.department)
                            InfoRow(label = "Branch", value = ticket?.branch)
                            InfoRow(label = "Assigned To", value = ticket?.assignedTo)
                            InfoRow(label = "Priority", value = ticket?.priority)
                            InfoRow(label = "Issue Type", value = ticket?.issueType)
                            InfoRow(label = "Created", value = ticket?.createdAt)
                            if (!ticket?.solvedAt.isNullOrBlank()) InfoRow(label = "Solved", value = ticket?.solvedAt)
                        }
                    }

                    // Issue Card
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("ISSUE DESCRIPTION", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f))
                            Text(ticket?.issue ?: "", style = MaterialTheme.typography.bodyMedium)
                            if (!ticket?.providedSolution.isNullOrBlank()) {
                                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))
                                Text("PROVIDED SOLUTION", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f))
                                Text(ticket?.providedSolution ?: "", style = MaterialTheme.typography.bodyMedium, color = Color(0xFF2E7D32))
                            }
                        }
                    }

                    // Context-aware actions: the primary button always reflects the next valid field action.
                    val currentStatus = ticket?.status?.trim().orEmpty()
                    val isActionLoading = actionState is ActionUiState.Loading
                    val isFinal = currentStatus in listOf("Solved", "Resolved (Temporary)", "Closed")
                    val isPending = currentStatus == "Pending"
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        OutlinedButton(
                            onClick = { showAddNote = !showAddNote },
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(12.dp),
                            enabled = !isActionLoading
                        ) {
                            Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(Modifier.width(6.dp))
                            Text("Add Note")
                        }
                        if (isFinal) {
                            OutlinedButton(
                                onClick = {
                                    reopenNote = ""
                                    showReopenDialog = true
                                },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp),
                                enabled = !isActionLoading
                            ) { Text("Reopen") }
                        } else if (isPending) {
                            androidx.compose.material3.Button(
                                onClick = {
                                    viewModel.updateStatus(
                                        ticketId = ticketId,
                                        newStatus = "In Progress",
                                        userId = UserSession.currentUser?.userId
                                    )
                                },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp),
                                enabled = !isActionLoading
                            ) { Text("Start Work") }
                        } else {
                            OutlinedButton(
                                onClick = {
                                    selectedStatus = currentStatus
                                    showStatusDialog = true
                                },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp),
                                enabled = !isActionLoading
                            ) { Text("Update Status") }
                        }
                    }

                    if (showReopenDialog) {
                        AlertDialog(
                            onDismissRequest = { showReopenDialog = false },
                            title = { Text("Reopen Ticket") },
                            text = {
                                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                    Text(
                                        "This ticket is in a final state. Add a short reason before reopening it for active work.",
                                        style = MaterialTheme.typography.bodyMedium
                                    )
                                    OutlinedTextField(
                                        value = reopenNote,
                                        onValueChange = { reopenNote = it },
                                        label = { Text("Reason for reopening") },
                                        modifier = Modifier.fillMaxWidth(),
                                        minLines = 3,
                                        maxLines = 5,
                                        shape = RoundedCornerShape(10.dp)
                                    )
                                }
                            },
                            confirmButton = {
                                androidx.compose.material3.Button(
                                    onClick = {
                                        showReopenDialog = false
                                        viewModel.updateStatus(
                                            ticketId = ticketId,
                                            newStatus = "Reopened",
                                            note = reopenNote.trim(),
                                            userId = UserSession.currentUser?.userId
                                        )
                                    },
                                    enabled = reopenNote.isNotBlank()
                                ) { Text("Reopen Ticket") }
                            },
                            dismissButton = {
                                OutlinedButton(onClick = { showReopenDialog = false }) { Text("Cancel") }
                            }
                        )
                    }

                    if (!isFinal) {
                        OutlinedButton(
                            onClick = {
                                selectedPriority = ticket?.priority ?: "Medium"
                                showPriorityDialog = true
                            },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp),
                            enabled = !isActionLoading
                        ) {
                            Text("Update Priority")
                        }
                    }

                    if (showPriorityDialog) {
                        val priorityOptions = listOf("Low", "Medium", "High", "Critical")
                        AlertDialog(
                            onDismissRequest = { showPriorityDialog = false },
                            title = { Text("Update Priority") },
                            text = {
                                Column {
                                    priorityOptions.forEach { priority ->
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clickable { selectedPriority = priority }
                                                .padding(vertical = 4.dp),
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            RadioButton(
                                                selected = selectedPriority == priority,
                                                onClick = { selectedPriority = priority }
                                            )
                                            Spacer(Modifier.width(8.dp))
                                            Text(priority, style = MaterialTheme.typography.bodyMedium)
                                        }
                                    }
                                }
                            },
                            confirmButton = {
                                OutlinedButton(
                                    onClick = {
                                        showPriorityDialog = false
                                        viewModel.updatePriority(
                                            ticketId = ticketId,
                                            newPriority = selectedPriority,
                                            userId = UserSession.currentUser?.userId
                                        )
                                    },
                                    enabled = selectedPriority.isNotBlank()
                                ) { Text("Confirm") }
                            },
                            dismissButton = {
                                OutlinedButton(onClick = { showPriorityDialog = false }) { Text("Cancel") }
                            }
                        )
                    }

                    if (!isFinal) {
                        OutlinedButton(
                            onClick = {
                                navController.navigate("call_ticket_resolve/$ticketId")
                            },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp),
                            enabled = !isActionLoading
                        ) {
                            Text("Mark As Resolved")
                        }
                    }

                    if (showStatusDialog) {
                        val statusOptions = allowedTicketStatuses(ticket?.status)
                        AlertDialog(
                            onDismissRequest = { showStatusDialog = false },
                            title = { Text("Update Status") },
                            text = {
                                Column {
                                    statusOptions.forEach { status ->
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clickable { selectedStatus = status }
                                                .padding(vertical = 4.dp),
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            RadioButton(
                                                selected = selectedStatus == status,
                                                onClick = { selectedStatus = status }
                                            )
                                            Spacer(Modifier.width(8.dp))
                                            Text(status, style = MaterialTheme.typography.bodyMedium)
                                        }
                                    }
                                }
                            },
                            confirmButton = {
                                OutlinedButton(
                                    onClick = {
                                        showStatusDialog = false
                                        viewModel.updateStatus(
                                            ticketId = ticketId,
                                            newStatus = selectedStatus,
                                            userId = UserSession.currentUser?.userId
                                        )
                                    },
                                    enabled = selectedStatus.isNotBlank()
                                ) { Text("Confirm") }
                            },
                            dismissButton = {
                                OutlinedButton(onClick = { showStatusDialog = false }) { Text("Cancel") }
                            }
                        )
                    }

                    AnimatedVisibility(
                        visible = showAddNote,
                        enter = expandVertically() + fadeIn(),
                        exit = shrinkVertically() + fadeOut()
                    ) {
                        Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(12.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f))) {
                            Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                OutlinedTextField(
                                    value = newNoteText,
                                    onValueChange = { newNoteText = it },
                                    placeholder = { Text("Write a note...") },
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(10.dp),
                                    maxLines = 4
                                )
                                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                                    OutlinedButton(
                                        onClick = {
                                            if (newNoteText.isNotBlank()) {
                                                viewModel.addNote(
                                                    ticketId = ticketId,
                                                    noteText = newNoteText,
                                                    noteType = "Internal",
                                                    userId = UserSession.currentUser?.userId
                                                )
                                                newNoteText = ""
                                                showAddNote = false
                                            }
                                        },
                                        shape = RoundedCornerShape(10.dp),
                                        enabled = !isActionLoading && newNoteText.isNotBlank()
                                    ) {
                                        Text("Save Note")
                                    }
                                }
                            }
                        }
                    }

                    // Notes Section
                    if (detail.notes.isNotEmpty()) {
                        NotesTimelineSection(notes = detail.notes)
                    }

                    // History Section
                    if (detail.history.isNotEmpty()) {
                        HistoryTimelineSection(history = detail.history)
                    }

                    Spacer(Modifier.height(88.dp))
                }
            }
        }
    }

    legacyPhotoPrompt?.let { bundle ->
        AlertDialog(
            onDismissRequest = { legacyPhotoPrompt = null },
            title = { Text("No field photos") },
            text = { Text("No field-visit photos are on file for this ticket. Continue anyway?") },
            confirmButton = {
                TextButton(onClick = {
                    legacyPhotoPrompt = null
                    val data = (viewModel.detailState.value as? TicketDetailUiState.Success)?.response
                    if (data?.ticket != null) {
                        generateTicketPdf(context, data.ticket, data.notes, data.history, signOff = bundle)
                    }
                }) { Text("Continue") }
            },
            dismissButton = {
                TextButton(onClick = { legacyPhotoPrompt = null }) { Text("Cancel") }
            }
        )
    }
}

@Composable
private fun InfoRow(label: String, value: String?) {
    if (value.isNullOrBlank()) return
    Row(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = "$label: ",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.width(100.dp)
        )
        Text(text = value, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun NotesTimelineSection(notes: List<CallTicketNoteDto>) {
    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(0.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Filled.Notes, contentDescription = null, modifier = Modifier.size(18.dp), tint = MaterialTheme.colorScheme.primary)
                Text("NOTES & ACTIVITY", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f))
            }
            Spacer(Modifier.height(10.dp))
            notes.forEachIndexed { i, note ->
                NoteTimelineItem(note = note, isLast = i == notes.lastIndex)
            }
        }
    }
}

@Composable
private fun NoteTimelineItem(note: CallTicketNoteDto, isLast: Boolean) {
    val noteColor = when (note.noteType) {
        "Solution" -> Color(0xFF2E7D32)
        "Escalation" -> Color(0xFF7B1FA2)
        "Troubleshooting" -> Color(0xFF1565C0)
        else -> MaterialTheme.colorScheme.primary
    }
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.size(10.dp).clip(CircleShape).background(noteColor))
            if (!isLast) Box(modifier = Modifier.width(2.dp).height(80.dp).background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)))
        }
        Column(
            modifier = Modifier.weight(1f).padding(bottom = if (isLast) 0.dp else 10.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text(text = note.noteType ?: "Note", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = noteColor)
                Text(text = note.createdAt ?: "", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(text = note.createdBy ?: "", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurface, fontWeight = FontWeight.SemiBold)
            Text(text = note.noteText ?: "", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun HistoryTimelineSection(history: List<CallTicketHistoryDto>) {
    var isExpanded by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(true) }
    val toggleLabel = if (isExpanded) "Collapse status history" else "Expand status history"

    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(0.dp)) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable(onClickLabel = toggleLabel) { isExpanded = !isExpanded },
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Icon(Icons.Filled.History, contentDescription = null, modifier = Modifier.size(18.dp), tint = MaterialTheme.colorScheme.primary)
                Text(
                    "STATUS HISTORY",
                    modifier = Modifier.weight(1f),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f)
                )
                Text(
                    text = if (isExpanded) "⌃" else "⌄",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.titleMedium
                )
            }
            AnimatedVisibility(
                visible = isExpanded,
                enter = expandVertically() + fadeIn(),
                exit = shrinkVertically() + fadeOut()
            ) {
                Column {
                    Spacer(Modifier.height(10.dp))
                    history.forEachIndexed { i, entry ->
                        HistoryTimelineItem(entry = entry, isLast = i == history.lastIndex)
                    }
                }
            }
        }
    }
}

@Composable
private fun HistoryTimelineItem(entry: CallTicketHistoryDto, isLast: Boolean) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.size(8.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.6f)))
            if (!isLast) Box(modifier = Modifier.width(2.dp).height(48.dp).background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f)))
        }
        Column(
            modifier = Modifier.weight(1f).padding(bottom = if (isLast) 0.dp else 8.dp),
            verticalArrangement = Arrangement.spacedBy(2.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "${entry.fieldName}: ${entry.oldValue?.takeIf { it.isNotBlank() } ?: "—"} → ${entry.newValue}",
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = FontWeight.SemiBold
                )
                Text(text = entry.changedAt ?: "", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(text = "by ${entry.changedBy}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f))
        }
    }
}


private fun allowedTicketStatuses(currentStatus: String?): List<String> {
    val current = currentStatus?.trim().orEmpty()
    val known = listOf("Pending", "In Progress", "Escalated", "Resolved (Temporary)", "Solved", "Closed")

    if (current.equals("Solved", ignoreCase = true) ||
        current.equals("Resolved (Temporary)", ignoreCase = true) ||
        current.equals("Closed", ignoreCase = true)) {
        return listOf(current, "Reopened")
    }

    fun rank(status: String): Int = when {
        status.equals("Pending", ignoreCase = true) -> 0
        status.equals("In Progress", ignoreCase = true) -> 1
        status.equals("Escalated", ignoreCase = true) -> 2
        status.equals("Resolved (Temporary)", ignoreCase = true) -> 3
        status.equals("Solved", ignoreCase = true) || status.equals("Closed", ignoreCase = true) -> 4
        else -> -1
    }

    val currentRank = rank(current)
    return if (currentRank < 0) known else known.filter { rank(it) >= currentRank }
}
