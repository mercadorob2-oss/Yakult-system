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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.MoreHoriz
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
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
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallTicketDetail
import com.example.yakultscanner.api.CallTicketHistoryDto
import com.example.yakultscanner.api.CallTicketNoteDto
import com.example.yakultscanner.ui.components.ItcmMetaText
import com.example.yakultscanner.ui.components.ItcmPriorityChip
import com.example.yakultscanner.ui.components.ItcmSectionHeading
import com.example.yakultscanner.ui.components.ItcmStatusChip
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.ui.components.itcmStatusColor
import com.example.yakultscanner.utils.SignOffParity
import com.example.yakultscanner.utils.TicketSignOffBundle
import com.example.yakultscanner.utils.generateTicketPdf
import com.example.yakultscanner.viewmodels.ActionUiState
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.SignOffExportUiState
import com.example.yakultscanner.viewmodels.TicketDetailUiState

private enum class ItcmDetailSheet {
    More,
    AddNote,
    UpdateStatus,
    UpdatePriority,
    Reopen
}

private data class ItcmPrimaryAction(
    val label: String,
    val onClick: () -> Unit
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ItcmTicketDetailScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val snackbarHostState = remember { SnackbarHostState() }
    val detailState by viewModel.detailState.collectAsState()
    val actionState by viewModel.actionState.collectAsState()
    val context = LocalContext.current

    var activeSheet by remember { mutableStateOf<ItcmDetailSheet?>(null) }
    var noteText by remember { mutableStateOf("") }
    var selectedStatus by remember { mutableStateOf("") }
    var selectedPriority by remember { mutableStateOf("") }
    var reopenReason by remember { mutableStateOf("") }
    var isExportLoading by remember { mutableStateOf(false) }
    var pendingPhotoPrompt by remember { mutableStateOf<TicketSignOffBundle?>(null) }
    val signOffExportState by viewModel.signOffExportState.collectAsState()

    LaunchedEffect(ticketId) {
        viewModel.loadTicketDetail(ticketId)
    }

    LaunchedEffect(actionState) {
        when (val state = actionState) {
            is ActionUiState.Success -> {
                snackbarHostState.showSnackbar(state.message)
                activeSheet = null
                noteText = ""
                reopenReason = ""
                viewModel.resetActionState()
                viewModel.loadTicketDetail(ticketId, force = true)
            }
            is ActionUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetActionState()
            }
            else -> Unit
        }
    }

    val ticket = (detailState as? TicketDetailUiState.Success)?.response?.ticket
    val isActionLoading = actionState is ActionUiState.Loading

    fun renderExport(
        exportTicket: CallTicketDetail,
        exportNotes: List<CallTicketNoteDto>,
        exportHistory: List<CallTicketHistoryDto>,
        bundle: TicketSignOffBundle?
    ) {
        // Ineligible tickets (or missing bundle) keep the legacy basic copy.
        val eligible = bundle != null &&
            SignOffParity.isSignOffEligible(exportTicket.status, bundle.visits)
        generateTicketPdf(
            context, exportTicket, exportNotes, exportHistory,
            signOff = if (eligible) bundle else null
        )
    }

    LaunchedEffect(signOffExportState) {
        when (val state = signOffExportState) {
            is SignOffExportUiState.Ready -> {
                isExportLoading = false
                viewModel.resetSignOffExportState()
                val data = (detailState as? TicketDetailUiState.Success)?.response
                if (data != null && ticket != null) {
                    if (state.bundle.totalPhotoCount == 0 &&
                        SignOffParity.isSignOffEligible(ticket.status, state.bundle.visits)
                    ) {
                        // Desktop asks "Continue anyway?" when no photos are on file.
                        pendingPhotoPrompt = state.bundle
                    } else {
                        renderExport(ticket, data.notes, data.history, state.bundle)
                    }
                }
            }
            is SignOffExportUiState.Error -> {
                isExportLoading = false
                viewModel.resetSignOffExportState()
                // Fall back to the basic copy so export still works offline-ish.
                val data = (detailState as? TicketDetailUiState.Success)?.response
                if (data != null && ticket != null) {
                    renderExport(ticket, data.notes, data.history, null)
                }
                snackbarHostState.showSnackbar(state.message)
            }
            else -> Unit
        }
    }
    val primaryAction = ticket?.let {
        when {
            it.status.equals("Pending", ignoreCase = true) -> ItcmPrimaryAction("Start Work") {
                viewModel.updateStatus(ticketId, "In Progress", userId = UserSession.currentUser?.userId)
            }
            isFinalTicketStatus(it.status) -> ItcmPrimaryAction("Reopen Ticket") {
                reopenReason = ""
                activeSheet = ItcmDetailSheet.Reopen
            }
            else -> ItcmPrimaryAction("Mark as Resolved") {
                navController.navigate("call_ticket_resolve/$ticketId")
            }
        }
    }

    Scaffold(
        containerColor = ItcmUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text(ticket?.ticketCode ?: "Ticket details", fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                        Text("IT Call Monitoring", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ItcmUi.Ink)
                    }
                },
                actions = {
                    IconButton(onClick = { viewModel.loadTicketDetail(ticketId, force = true) }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh ticket", tint = ItcmUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ItcmUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) },
        bottomBar = {
            if (primaryAction != null) {
                ItcmTicketActionDock(
                    primaryLabel = primaryAction.label,
                    onPrimary = primaryAction.onClick,
                    onMore = { activeSheet = ItcmDetailSheet.More },
                    enabled = !isActionLoading
                )
            }
        }
    ) { padding ->
        when (val state = detailState) {
            is TicketDetailUiState.Loading -> ItcmDetailLoading(Modifier.padding(padding))
            is TicketDetailUiState.Error -> ItcmDetailError(
                modifier = Modifier.padding(padding),
                message = state.message,
                onRetry = { viewModel.loadTicketDetail(ticketId) }
            )
            is TicketDetailUiState.Success -> {
                val detail = state.response
                LazyColumn(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(ItcmUi.Canvas)
                        .padding(padding),
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 14.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    item { ItcmTicketHero(ticket = detail.ticket) }
                    item { ItcmIssueCard(ticket = detail.ticket) }
                    item { ItcmTicketFacts(ticket = detail.ticket) }
                    item { CallFieldWorkCard(ticketId = ticketId, viewModel = viewModel, onViewDetails = { navController.navigate("field_work_detail/$ticketId") }) }
                    if (detail.notes.isNotEmpty()) {
                        item { ItcmTimelineHeader("Notes & activity", Icons.Filled.Notes) }
                        items(detail.notes, key = { "note-${it.noteId}-${it.createdAt}" }) { note ->
                            ItcmNoteTimelineItem(note)
                        }
                    }
                    if (detail.history.isNotEmpty()) {
                        item {
                            var isHistoryExpanded by androidx.compose.runtime.saveable.rememberSaveable(ticketId) { mutableStateOf(true) }
                            val toggleLabel = if (isHistoryExpanded) "Collapse status history" else "Expand status history"

                            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clip(RoundedCornerShape(10.dp))
                                        .clickable(onClickLabel = toggleLabel) {
                                            isHistoryExpanded = !isHistoryExpanded
                                        }
                                        .padding(vertical = 4.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    Icon(Icons.Filled.History, contentDescription = null, modifier = Modifier.size(18.dp), tint = ItcmUi.Brand)
                                    Text(
                                        "Status history",
                                        modifier = Modifier.weight(1f),
                                        style = MaterialTheme.typography.titleSmall,
                                        color = ItcmUi.Ink,
                                        fontWeight = FontWeight.Bold
                                    )
                                    Text(
                                        text = if (isHistoryExpanded) "⌃" else "⌄",
                                        style = MaterialTheme.typography.titleMedium,
                                        color = ItcmUi.Muted
                                    )
                                }
                                if (isHistoryExpanded) {
                                    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                        detail.history.forEach { history ->
                                            ItcmHistoryTimelineItem(history)
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (detail.notes.isEmpty() && detail.history.isEmpty()) {
                        item {
                            ItcmSurfaceCard {
                                Text("No activity yet", style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
                                Text("Updates and technician notes will appear here.", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                            }
                        }
                    }
                }
            }
        }
    }

    ticket?.let { activeTicket ->
        when (activeSheet) {
            ItcmDetailSheet.More -> ItcmMoreActionsSheet(
                ticket = activeTicket,
                onDismiss = { activeSheet = null },
                onAddNote = { activeSheet = ItcmDetailSheet.AddNote },
                onUpdateStatus = {
                    selectedStatus = activeTicket.status.orEmpty()
                    activeSheet = ItcmDetailSheet.UpdateStatus
                },
                onUpdatePriority = {
                    selectedPriority = activeTicket.priority ?: "Medium"
                    activeSheet = ItcmDetailSheet.UpdatePriority
                },
                onExport = {
                    activeSheet = null
                    isExportLoading = true
                    viewModel.exportSignOffBundle(ticketId)
                }
            )
            ItcmDetailSheet.AddNote -> ItcmAddNoteSheet(
                noteText = noteText,
                loading = isActionLoading,
                onNoteChange = { noteText = it },
                onDismiss = { activeSheet = null },
                onSave = {
                    viewModel.addNote(
                        ticketId = ticketId,
                        noteText = noteText.trim(),
                        noteType = "Internal",
                        userId = UserSession.currentUser?.userId
                    )
                }
            )
            ItcmDetailSheet.UpdateStatus -> ItcmStatusSheet(
                currentStatus = activeTicket.status.orEmpty(),
                selectedStatus = selectedStatus,
                loading = isActionLoading,
                onSelected = { selectedStatus = it },
                onDismiss = { activeSheet = null },
                onSave = {
                    viewModel.updateStatus(ticketId, selectedStatus, userId = UserSession.currentUser?.userId)
                }
            )
            ItcmDetailSheet.UpdatePriority -> ItcmPrioritySheet(
                selectedPriority = selectedPriority,
                loading = isActionLoading,
                onSelected = { selectedPriority = it },
                onDismiss = { activeSheet = null },
                onSave = {
                    viewModel.updatePriority(ticketId, selectedPriority, userId = UserSession.currentUser?.userId)
                }
            )
            ItcmDetailSheet.Reopen -> ItcmReopenSheet(
                reason = reopenReason,
                loading = isActionLoading,
                onReasonChange = { reopenReason = it },
                onDismiss = { activeSheet = null },
                onReopen = {
                    viewModel.updateStatus(
                        ticketId = ticketId,
                        newStatus = "Reopened",
                        note = reopenReason.trim(),
                        userId = UserSession.currentUser?.userId
                    )
                }
            )
            null -> Unit
        }
    }

    if (isExportLoading) {
        AlertDialog(
            onDismissRequest = { },
            title = { Text("Preparing sign-off PDF") },
            text = {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(modifier = Modifier.size(24.dp))
                    Spacer(Modifier.width(12.dp))
                    Text("Loading field visits, photos and signature…")
                }
            },
            confirmButton = { }
        )
    }

    pendingPhotoPrompt?.let { bundle ->
        AlertDialog(
            onDismissRequest = { pendingPhotoPrompt = null },
            title = { Text("No field photos") },
            text = { Text("No field-visit photos are on file for this ticket. Continue anyway?") },
            confirmButton = {
                TextButton(onClick = {
                    pendingPhotoPrompt = null
                    val data = (detailState as? TicketDetailUiState.Success)?.response
                    if (data != null && ticket != null) {
                        renderExport(ticket, data.notes, data.history, bundle)
                    }
                }) { Text("Continue") }
            },
            dismissButton = {
                TextButton(onClick = { pendingPhotoPrompt = null }) { Text("Cancel") }
            }
        )
    }
}

@Composable
private fun ItcmTicketActionDock(
    primaryLabel: String,
    onPrimary: () -> Unit,
    onMore: () -> Unit,
    enabled: Boolean
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(ItcmUi.Surface)
            .padding(start = 16.dp, end = 16.dp, top = 10.dp, bottom = 8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(
                onClick = onPrimary,
                enabled = enabled,
                modifier = Modifier.weight(1f).height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) { Text(primaryLabel, fontWeight = FontWeight.Bold) }
            OutlinedButton(
                onClick = onMore,
                enabled = enabled,
                modifier = Modifier.height(52.dp),
                shape = RoundedCornerShape(16.dp)
            ) {
                Icon(Icons.Filled.MoreHoriz, contentDescription = null)
                Spacer(Modifier.width(4.dp))
                Text("More")
            }
        }
    }
}

@Composable
private fun ItcmTicketHero(ticket: CallTicketDetail?) {
    ItcmSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                Text(ticket?.ticketCode.orEmpty(), style = MaterialTheme.typography.titleLarge, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
                Text(ticket?.issue.orEmpty().ifBlank { "No issue summary" }, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            Spacer(Modifier.width(10.dp))
            ItcmStatusChip(ticket?.status)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
            ItcmPriorityChip(ticket?.priority)
            ticket?.createdAt?.takeIf { it.isNotBlank() }?.let { createdAt ->
                Text("Created $createdAt", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.SemiBold)
            }
        }
    }
}

@Composable
private fun ItcmIssueCard(ticket: CallTicketDetail?) {
    ItcmSurfaceCard {
        ItcmSectionHeading("Issue description")
        Text(ticket?.issue.orEmpty().ifBlank { "No issue details were provided." }, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink)
        ticket?.providedSolution?.takeIf { it.isNotBlank() }?.let { solution ->
            HorizontalDivider(color = ItcmUi.Divider)
            ItcmSectionHeading("Provided solution")
            Text(solution, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Resolved)
        }
    }
}

@Composable
private fun ItcmTicketFacts(ticket: CallTicketDetail?) {
    ItcmSurfaceCard {
        ItcmSectionHeading("Ticket information")
        val facts = listOf(
            "Requester" to ticket?.callerName,
            "Company" to ticket?.company,
            "Department" to ticket?.department,
            "Branch" to ticket?.branch,
            "Assigned to" to ticket?.assignedTo,
            "Issue type" to ticket?.issueType,
            "Created" to ticket?.createdAt,
            "Solved" to ticket?.solvedAt
        ).filter { !it.second.isNullOrBlank() }
        facts.forEachIndexed { index, (label, value) ->
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
                Text(label, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted, modifier = Modifier.width(104.dp))
                Text(value.orEmpty(), style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
            }
            if (index != facts.lastIndex) HorizontalDivider(color = ItcmUi.Divider)
        }
    }
}

@Composable
private fun ItcmTimelineHeader(title: String, icon: androidx.compose.ui.graphics.vector.ImageVector) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.padding(top = 4.dp)) {
        Icon(icon, contentDescription = null, modifier = Modifier.size(18.dp), tint = ItcmUi.Brand)
        Text(title, style = MaterialTheme.typography.titleSmall, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun ItcmNoteTimelineItem(note: CallTicketNoteDto) {
    val tone = when (note.noteType?.lowercase()) {
        "solution" -> ItcmUi.Resolved
        "escalation" -> ItcmUi.Escalated
        "troubleshooting" -> ItcmUi.Active
        else -> ItcmUi.Brand
    }
    ItcmSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Text(note.noteType ?: "Note", style = MaterialTheme.typography.labelMedium, color = tone, fontWeight = FontWeight.Bold)
            ItcmMetaText(note.createdAt.orEmpty())
        }
        note.createdBy?.takeIf { it.isNotBlank() }?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold) }
        Text(note.noteText.orEmpty(), style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
    }
}

@Composable
private fun ItcmHistoryTimelineItem(entry: CallTicketHistoryDto) {
    val label = buildString {
        append(entry.fieldName ?: "Ticket")
        if (!entry.newValue.isNullOrBlank()) {
            append(" · ")
            append(entry.oldValue?.takeIf { it.isNotBlank() } ?: "—")
            append(" → ")
            append(entry.newValue)
        }
    }
    ItcmSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically, modifier = Modifier.weight(1f)) {
                Box(modifier = Modifier.size(8.dp).clip(RoundedCornerShape(99.dp)).background(itcmStatusColor(entry.newValue)))
                Text(label, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            Spacer(Modifier.width(8.dp))
            ItcmMetaText(entry.changedAt.orEmpty())
        }
        entry.changedBy?.takeIf { it.isNotBlank() }?.let { ItcmMetaText("by $it") }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmMoreActionsSheet(
    ticket: CallTicketDetail,
    onDismiss: () -> Unit,
    onAddNote: () -> Unit,
    onUpdateStatus: () -> Unit,
    onUpdatePriority: () -> Unit,
    onExport: () -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = ItcmUi.Surface) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            ItcmSectionHeading("More actions", ticket.ticketCode)
            ItcmActionRow("Add note", "Record an internal update", onAddNote)
            if (!isFinalTicketStatus(ticket.status)) {
                ItcmActionRow("Update status", "Move the ticket through its existing workflow", onUpdateStatus)
                ItcmActionRow("Update priority", "Change urgency for the team", onUpdatePriority)
            }
            ItcmActionRow("Export PDF", "Create a portable ticket copy", onExport)
            Spacer(Modifier.height(20.dp))
        }
    }
}

@Composable
private fun ItcmActionRow(title: String, description: String, onClick: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).clickable(onClick = onClick).padding(vertical = 12.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(12.dp)).background(ItcmUi.BrandSoft), contentAlignment = Alignment.Center) {
            Icon(Icons.Filled.Add, contentDescription = null, tint = ItcmUi.Brand, modifier = Modifier.size(18.dp))
        }
        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
            Text(description, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmAddNoteSheet(
    noteText: String,
    loading: Boolean,
    onNoteChange: (String) -> Unit,
    onDismiss: () -> Unit,
    onSave: () -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = ItcmUi.Surface) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            ItcmSectionHeading("Add internal note", "This update is recorded in the ticket activity.")
            OutlinedTextField(
                value = noteText,
                onValueChange = onNoteChange,
                modifier = Modifier.fillMaxWidth(),
                minLines = 4,
                maxLines = 6,
                placeholder = { Text("Write a useful handoff or troubleshooting note…") },
                shape = RoundedCornerShape(16.dp)
            )
            Button(onClick = onSave, enabled = noteText.isNotBlank() && !loading, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) {
                Text("Save note", fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(20.dp))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmStatusSheet(
    currentStatus: String,
    selectedStatus: String,
    loading: Boolean,
    onSelected: (String) -> Unit,
    onDismiss: () -> Unit,
    onSave: () -> Unit
) {
    val statuses = itcmAllowedStatuses(currentStatus)
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = ItcmUi.Surface) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            ItcmSectionHeading("Update status", "Only valid current workflow transitions are shown.")
            statuses.forEach { status ->
                Row(modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).clickable { onSelected(status) }.padding(12.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    RadioButton(selected = selectedStatus == status, onClick = { onSelected(status) })
                    ItcmStatusChip(status)
                    Text(status, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink)
                }
            }
            Button(onClick = onSave, enabled = selectedStatus.isNotBlank() && !loading, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) { Text("Save status", fontWeight = FontWeight.Bold) }
            Spacer(Modifier.height(20.dp))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmPrioritySheet(
    selectedPriority: String,
    loading: Boolean,
    onSelected: (String) -> Unit,
    onDismiss: () -> Unit,
    onSave: () -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = ItcmUi.Surface) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            ItcmSectionHeading("Update priority", "Choose the urgency that best reflects current impact.")
            listOf("Low", "Medium", "High", "Critical").forEach { priority ->
                Row(modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).clickable { onSelected(priority) }.padding(12.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    RadioButton(selected = selectedPriority == priority, onClick = { onSelected(priority) })
                    ItcmPriorityChip(priority)
                }
            }
            Button(onClick = onSave, enabled = selectedPriority.isNotBlank() && !loading, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) { Text("Save priority", fontWeight = FontWeight.Bold) }
            Spacer(Modifier.height(20.dp))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ItcmReopenSheet(
    reason: String,
    loading: Boolean,
    onReasonChange: (String) -> Unit,
    onDismiss: () -> Unit,
    onReopen: () -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = ItcmUi.Surface) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            ItcmSectionHeading("Reopen ticket", "Explain why the ticket needs active work again.")
            OutlinedTextField(
                value = reason,
                onValueChange = onReasonChange,
                modifier = Modifier.fillMaxWidth(),
                minLines = 3,
                maxLines = 5,
                placeholder = { Text("Reason for reopening…") },
                shape = RoundedCornerShape(16.dp)
            )
            Button(onClick = onReopen, enabled = reason.isNotBlank() && !loading, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) {
                Text("Reopen ticket", fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(20.dp))
        }
    }
}

@Composable
private fun ItcmDetailLoading(modifier: Modifier) {
    Box(modifier = modifier.fillMaxSize().background(ItcmUi.Canvas), contentAlignment = Alignment.Center) {
        Text("Loading ticket…", color = ItcmUi.Muted)
    }
}

@Composable
private fun ItcmDetailError(modifier: Modifier, message: String, onRetry: () -> Unit) {
    Box(modifier = modifier.fillMaxSize().background(ItcmUi.Canvas).padding(28.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Couldn’t load this ticket", style = MaterialTheme.typography.titleMedium, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
            Button(onClick = onRetry) { Text("Try again") }
        }
    }
}

private fun isFinalTicketStatus(status: String?): Boolean = status?.trim() in listOf("Solved", "Resolved (Temporary)", "Closed")

private fun itcmAllowedStatuses(currentStatus: String): List<String> {
    val known = listOf("Pending", "In Progress", "Escalated", "Resolved (Temporary)", "Solved", "Closed")
    if (isFinalTicketStatus(currentStatus)) return listOf(currentStatus, "Reopened")
    val rank = mapOf(
        "Pending" to 0,
        "In Progress" to 1,
        "Escalated" to 2,
        "Resolved (Temporary)" to 3,
        "Solved" to 4,
        "Closed" to 4
    )
    val currentRank = rank[currentStatus] ?: -1
    return if (currentRank < 0) known else known.filter { (rank[it] ?: Int.MAX_VALUE) >= currentRank }
}