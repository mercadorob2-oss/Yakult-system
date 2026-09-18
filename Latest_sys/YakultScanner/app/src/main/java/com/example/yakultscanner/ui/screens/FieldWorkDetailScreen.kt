package com.example.yakultscanner.ui.screens

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.util.Base64
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.FileProvider
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Draw
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.StickyNote2
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.Warning
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.SignaturePad
import com.example.yakultscanner.ui.components.SignatureView
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.FieldVisitActionUiState
import com.example.yakultscanner.viewmodels.FieldVisitsUiState
import com.example.yakultscanner.viewmodels.TicketDetailUiState
import java.io.ByteArrayOutputStream
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FieldWorkDetailScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val visitsState by viewModel.fieldVisitsState.collectAsState()
    val actionState by viewModel.fieldVisitActionState.collectAsState()
    val detailState by viewModel.detailState.collectAsState()
    val itEmployees by viewModel.itEmployees.collectAsState()
    val photoBase64 by viewModel.fieldPhotoBase64.collectAsState()
    val signatureBase64 by viewModel.fieldSignatureBase64.collectAsState()
    var selectedTab by remember { mutableStateOf(0) }
    var notesDraft by rememberSaveable(ticketId) { mutableStateOf("") }
    var selectedTechId by remember(ticketId) { mutableStateOf<Int?>(null) }
    var techExpanded by remember { mutableStateOf(false) }
    var showSignature by remember { mutableStateOf(false) }
    var selectedPhotoId by remember { mutableStateOf<Int?>(null) }
    var showPhotoSource by remember { mutableStateOf(false) }
    var pendingCameraUri by remember { mutableStateOf<Uri?>(null) }
    val context = LocalContext.current

    fun uploadPhotoBytes(bytes: ByteArray, name: String, mime: String) {
        val visit = (visitsState as? FieldVisitsUiState.Success)?.visits?.firstOrNull() ?: return
        if (bytes.size > 20 * 1024 * 1024) {
            android.widget.Toast.makeText(context, "File too large (max 20 MB)", android.widget.Toast.LENGTH_LONG).show()
            return
        }
        try {
            val base64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
            viewModel.uploadFieldVisitPhoto(visit.fieldVisitId, ticketId, name, mime, base64)
        } catch (_: Exception) {}
    }

    val photoLauncher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) {
            try {
                val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() } ?: return@rememberLauncherForActivityResult
                val name = uri.lastPathSegment?.substringAfterLast('/') ?: "field-photo.jpg"
                val mime = context.contentResolver.getType(uri) ?: "image/jpeg"
                uploadPhotoBytes(bytes, name, mime)
            } catch (_: Exception) {}
        }
    }

    val cameraLauncher = rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()) { success ->
        val uri = pendingCameraUri
        if (success && uri != null) {
            try {
                val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() } ?: return@rememberLauncherForActivityResult
                uploadPhotoBytes(bytes, "field-photo-${System.currentTimeMillis()}.jpg", "image/jpeg")
            } catch (_: Exception) {}
        }
    }

    fun launchCamera() {
        try {
            val file = File(context.filesDir, "field_visit_${System.currentTimeMillis()}.jpg")
            val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
            pendingCameraUri = uri
            cameraLauncher.launch(uri)
        } catch (_: Exception) {}
    }

    LaunchedEffect(ticketId) {
        viewModel.loadFieldVisits(ticketId)
        viewModel.loadTicketDetail(ticketId)
    }
    LaunchedEffect(Unit) { viewModel.loadItEmployees() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("Field Work Detail", fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                        Text("Ticket #$ticketId • One visit per ticket", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ItcmUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ItcmUi.Surface)
            )
        }
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().background(color = ItcmUi.Canvas).padding(padding)) {
            ScrollableTabRow(selectedTabIndex = selectedTab, containerColor = ItcmUi.Surface, contentColor = ItcmUi.Brand, edgePadding = 8.dp) {
                Tab(selected = selectedTab == 0, onClick = { selectedTab = 0 }, text = { Text("Overview", maxLines = 1, fontWeight = if (selectedTab==0) FontWeight.Bold else FontWeight.Normal) })
                Tab(selected = selectedTab == 1, onClick = { selectedTab = 1 }, text = { Text("Field Timeline", maxLines = 1, fontWeight = if (selectedTab==1) FontWeight.Bold else FontWeight.Normal) })
                Tab(selected = selectedTab == 2, onClick = { selectedTab = 2 }, text = { Text("Ticket Timeline", maxLines = 1, fontWeight = if (selectedTab==2) FontWeight.Bold else FontWeight.Normal) })
                val count = (visitsState as? FieldVisitsUiState.Success)?.attachments?.size ?: 0
                Tab(selected = selectedTab == 3, onClick = { selectedTab = 3 }, text = { Text("Photos ($count)", maxLines = 1, fontWeight = if (selectedTab==3) FontWeight.Bold else FontWeight.Normal) })
            }
            when (val s = visitsState) {
                FieldVisitsUiState.Idle, FieldVisitsUiState.Loading -> Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator(color = ItcmUi.Brand) }
                is FieldVisitsUiState.Error -> Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.spacedBy(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(s.message, color = ItcmUi.Critical, style = MaterialTheme.typography.bodySmall)
                    Button(onClick = { viewModel.loadFieldVisits(ticketId) }) { Text("Retry") }
                }
                is FieldVisitsUiState.Success -> {
                    val visit = s.visits.firstOrNull()
                    val attachments = s.attachments.filter { visit != null && it.fieldVisitId == visit.fieldVisitId }
                    val detailHistory = (detailState as? TicketDetailUiState.Success)?.response?.history ?: emptyList()
                    val fieldHistory = detailHistory.filter { it.fieldName.equals("FieldVisitStatus", true) }.sortedBy { it.changedAt }
                    val ticketHistory = detailHistory.sortedByDescending { it.changedAt }.take(60)
                    LaunchedEffect(visit?.fieldVisitId, visit?.hasSignature) {
                        if (visit?.hasSignature == true) viewModel.loadFieldVisitSignature(visit.fieldVisitId) else viewModel.clearFieldSignature()
                    }
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        when (selectedTab) {
                            0 -> {
                                if (visit == null) {
                                    item {
                                        ItcmSurfaceCard {
                                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Brand.copy(alpha=0.12f)), contentAlignment = Alignment.Center) {
                                                    Icon(Icons.Filled.History, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp))
                                                }
                                                Column {
                                                    Text("No field visit yet", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                    Text("Schedule one visit per ticket • pick technician • add notes", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                }
                                            }
                                            Spacer(Modifier.height(10.dp))
                                            if (itEmployees.isNotEmpty()) {
                                                ExposedDropdownMenuBox(expanded = techExpanded, onExpandedChange = { techExpanded = !techExpanded }) {
                                                    OutlinedTextField(
                                                        value = itEmployees.find { it.id == selectedTechId }?.name ?: "",
                                                        onValueChange = {}, readOnly = true,
                                                        label = { Text("Technician") }, placeholder = { Text("Select technician") },
                                                        trailingIcon = { Icon(Icons.Filled.Edit, null, modifier = Modifier.size(16.dp)) },
                                                        modifier = Modifier.fillMaxWidth().menuAnchor(), shape = RoundedCornerShape(10.dp)
                                                    )
                                                    ExposedDropdownMenu(expanded = techExpanded, onDismissRequest = { techExpanded = false }) {
                                                        DropdownMenuItem(text = { Text("— No technician —") }, onClick = { selectedTechId = null; techExpanded = false })
                                                        itEmployees.forEach { emp -> DropdownMenuItem(text = { Text(emp.name) }, onClick = { selectedTechId = emp.id; techExpanded = false }) }
                                                    }
                                                }
                                                Spacer(Modifier.height(8.dp))
                                            }
                                            OutlinedTextField(value = notesDraft, onValueChange = { notesDraft = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Notes (optional)") }, placeholder = { Text("Reason for visit, location, etc.") }, maxLines = 3, shape = RoundedCornerShape(10.dp))
                                            Spacer(Modifier.height(8.dp))
                                            Button(onClick = { viewModel.scheduleFieldVisit(ticketId, technicianEmpId = selectedTechId, notes = notesDraft.takeIf { it.isNotBlank() }) }, modifier = Modifier.fillMaxWidth(), colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand)) {
                                                Icon(Icons.Filled.Warning, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(6.dp)); Text("Schedule Visit", fontWeight = FontWeight.Bold)
                                            }
                                        }
                                    }
                                } else {
                                    item {
                                        val ticket = (detailState as? TicketDetailUiState.Success)?.response?.ticket
                                        ItcmSurfaceCard {
                                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                                Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(color = fieldStatusBg(visit.status)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                                                    Text(visit.status ?: "-", color = fieldStatusFg(visit.status), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                                                }
                                                Text("Visit #${visit.fieldVisitId} • Ticket #${visit.ticketId}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold)
                                                if (visit.hasSignature) Box(modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(color = ItcmUi.Resolved.copy(alpha=0.12f)).padding(horizontal=6.dp, vertical=2.dp)) { Text("✓ Signature", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.Bold) }
                                            }
                                            Spacer(Modifier.height(8.dp))
                                            Column(verticalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(10.dp)).padding(12.dp)) {
                                                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                                                    Column { Text("Technician", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit.technicianName ?: "—", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink) }
                                                    Column { Text("Scheduled", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit.scheduledAt?.let { formatVisitTime(it) } ?: "—", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink) }
                                                    Column { Text("Completed", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit.completedAt?.let { formatVisitTime(it) } ?: "Not yet", style = MaterialTheme.typography.bodySmall, color = if (visit.completedAt!=null) ItcmUi.Resolved else ItcmUi.Muted, fontWeight = FontWeight.Bold) }
                                                }
                                                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                                                    Column { Text("Photos", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text("${attachments.size}", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = if (attachments.isNotEmpty()) ItcmUi.Brand else ItcmUi.Muted) }
                                                    Column { Text("Signature", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(if (visit.hasSignature) "✓ On file" else "—", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = if (visit.hasSignature) ItcmUi.Resolved else ItcmUi.Muted) }
                                                    Column { Text("Status", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit.status ?: "—", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = when (visit.status?.lowercase()) { "completed" -> ItcmUi.Resolved; "cancelled" -> ItcmUi.Critical; else -> ItcmUi.Brand }) }
                                                }
                                                if (!visit.notes.isNullOrBlank()) { Text("Notes:", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold); Text(visit.notes!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink) }
                                                if (ticket != null) {
                                                    HorizontalDivider(color = ItcmUi.Divider)
                                                    Text("Ticket: ${ticket.ticketCode ?: "#${ticket.ticketId}"} • ${ticket.status ?: ""}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold)
                                                    if (!ticket.issue.isNullOrBlank()) Text(ticket.issue!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink)
                                                    Text("${ticket.department ?: "—"} • ${ticket.branch ?: "—"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                }
                                            }
                                            Spacer(Modifier.height(8.dp))
                                            OutlinedTextField(value = notesDraft, onValueChange = { notesDraft = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Notes / Reason") }, placeholder = { Text("Add notes for this action...") }, maxLines = 3, shape = RoundedCornerShape(10.dp))
                                            Spacer(Modifier.height(8.dp))
                                            val status = visit.status ?: "Scheduled"
                                            when {
                                                status.equals("Scheduled", true) -> Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                    Button(onClick = { viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Completed", notes = notesDraft.takeIf { it.isNotBlank() }) }, colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Resolved), modifier = Modifier.weight(1f)) { Text("Complete") }
                                                    OutlinedButton(onClick = { viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Cancelled", notes = notesDraft.takeIf { it.isNotBlank() }) }, modifier = Modifier.weight(1f)) { Text("Cancel") }
                                                }
                                                status.equals("Cancelled", true) -> Column(
                                                    verticalArrangement = Arrangement.spacedBy(10.dp),
                                                    modifier = Modifier.fillMaxWidth().background(color = ItcmUi.Critical.copy(alpha = 0.06f), shape = RoundedCornerShape(12.dp)).padding(12.dp)
                                                ) {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                        Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Critical.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) {
                                                            Icon(Icons.Filled.Refresh, null, tint = ItcmUi.Critical, modifier = Modifier.size(20.dp))
                                                        }
                                                        Column {
                                                            Text("Visit cancelled", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                            Text("Pick technician • new date = now • tap Reschedule to reopen", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                        }
                                                    }
                                                    if (itEmployees.isNotEmpty()) {
                                                        ExposedDropdownMenuBox(expanded = techExpanded, onExpandedChange = { techExpanded = !techExpanded }) {
                                                            OutlinedTextField(
                                                                value = itEmployees.find { it.id == (selectedTechId ?: visit.technicianEmpId) }?.name ?: "",
                                                                onValueChange = {}, readOnly = true,
                                                                label = { Text("Technician") }, placeholder = { Text("Select technician") },
                                                                supportingText = { Text("Current: ${visit.technicianName ?: "—"}") },
                                                                leadingIcon = { Icon(Icons.Filled.Person, null, modifier = Modifier.size(18.dp)) },
                                                                trailingIcon = { Icon(Icons.Filled.Edit, null, modifier = Modifier.size(16.dp)) },
                                                                modifier = Modifier.fillMaxWidth().menuAnchor(), shape = RoundedCornerShape(10.dp)
                                                            )
                                                            ExposedDropdownMenu(expanded = techExpanded, onDismissRequest = { techExpanded = false }) {
                                                                DropdownMenuItem(text = { Text("— Keep ${visit.technicianName ?: "current"} —") }, onClick = { selectedTechId = null; techExpanded = false })
                                                                itEmployees.forEach { emp -> DropdownMenuItem(text = { Text(emp.name) }, onClick = { selectedTechId = emp.id; techExpanded = false }) }
                                                            }
                                                        }
                                                    }
                                                    Button(
                                                        onClick = { viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Scheduled", notes = notesDraft.takeIf { it.isNotBlank() }, technicianEmpId = selectedTechId ?: visit.technicianEmpId) },
                                                        colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand),
                                                        modifier = Modifier.fillMaxWidth().height(48.dp),
                                                        shape = RoundedCornerShape(12.dp)
                                                    ) { Icon(Icons.Filled.Refresh, null, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(8.dp)); Text("Reschedule Visit", fontWeight = FontWeight.Bold) }
                                                }
                                                status.equals("Completed", true) -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                                    Icon(Icons.Filled.CheckCircle, null, tint = ItcmUi.Resolved, modifier = Modifier.size(18.dp)); Text("Visit Completed — no further actions.", color = ItcmUi.Muted, style = MaterialTheme.typography.bodySmall)
                                                }
                                            }
                                            if (status.equals("Scheduled", true) || status.equals("Cancelled", true)) {
                                                if (actionState is FieldVisitActionUiState.Loading) Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) { CircularProgressIndicator(Modifier.size(16.dp), color = ItcmUi.Brand, strokeWidth = 2.dp); Text("Updating…", color = ItcmUi.Muted, style = MaterialTheme.typography.labelSmall) }
                                                if (actionState is FieldVisitActionUiState.Error) {
                                                    val err = (actionState as FieldVisitActionUiState.Error).message
                                                    val friendly = if (err.contains("already has a field visit", true)) "This ticket already has a field visit. If it was cancelled, tap Reschedule." else err
                                                    Text(friendly, color = ItcmUi.Critical, style = MaterialTheme.typography.labelSmall)
                                                }
                                                if (actionState is FieldVisitActionUiState.Success) Text((actionState as FieldVisitActionUiState.Success).message, color = ItcmUi.Resolved, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.SemiBold)
                                            }
                                            if (visit.hasSignature && signatureBase64 != null) {
                                                Spacer(Modifier.height(8.dp))
                                                val sigBmp = try { val b = Base64.decode(signatureBase64, Base64.DEFAULT); BitmapFactory.decodeByteArray(b,0,b.size) } catch (_:Exception){ null }
                                                if (sigBmp != null) Image(bitmap = sigBmp.asImageBitmap(), contentDescription = "Signature", modifier = Modifier.fillMaxWidth().height(100.dp).clip(RoundedCornerShape(8.dp)).background(color = androidx.compose.ui.graphics.Color.White))
                                            }
                                        }
                                    }
                                }
                            }
                            1 -> {
                                item {
                                    if (visit == null) {
                                        ItcmSurfaceCard {
                                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                Box(modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.Schedule, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                Column {
                                                    Text("No field history yet", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                    Text("Schedule a visit to start the Field Timeline.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                }
                                            }
                                        }
                                    } else {
                                        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                            ItcmSurfaceCard {
                                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                    Box(modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(color = fieldStatusBg(visit.status).copy(alpha = 0.5f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.History, null, tint = fieldStatusFg(visit.status), modifier = Modifier.size(20.dp)) }
                                                    Column(modifier = Modifier.weight(1f)) {
                                                        Text("Field Timeline", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                        Text("Visit #${visit.fieldVisitId} • ${attachments.size} photo(s) • ${if (visit.hasSignature) "signature on file" else "no signature"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                    }
                                                    Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(color = fieldStatusBg(visit.status)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                                                        Text(visit.status ?: "-", color = fieldStatusFg(visit.status), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                                                    }
                                                }
                                            }
                                            if (fieldHistory.isEmpty()) {
                                                ItcmSurfaceCard {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                        Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.CheckCircle, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                        Column {
                                                            Text("Just scheduled", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                            Text("Status changes (complete, cancel, reschedule) will appear here.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                        }
                                                    }
                                                }
                                            } else {
                                                val grouped = fieldHistory.sortedByDescending { it.changedAt ?: "" }.groupBy { h -> formatDayKey(h.changedAt) }
                                                grouped.forEach { (day, items) ->
                                                    Text("$day • ${items.size}", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Muted, modifier = Modifier.padding(top = 4.dp))
                                                    items.forEachIndexed { idx, h ->
                                                        val isReschedule = h.oldValue.equals("Cancelled", true) && h.newValue.equals("Scheduled", true)
                                                        val accent = when {
                                                            h.newValue.equals("Completed", true) -> ItcmUi.Resolved
                                                            h.newValue.equals("Cancelled", true) -> ItcmUi.Critical
                                                            isReschedule -> ItcmUi.Pending
                                                            else -> ItcmUi.Brand
                                                        }
                                                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                                            Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.width(22.dp)) {
                                                                Box(modifier = Modifier.size(26.dp).clip(RoundedCornerShape(13.dp)).background(color = accent.copy(alpha = 0.14f)), contentAlignment = Alignment.Center) {
                                                                    Icon(if (isReschedule) Icons.Filled.Refresh else Icons.Filled.History, null, tint = accent, modifier = Modifier.size(14.dp))
                                                                }
                                                                if (idx != items.lastIndex) Box(modifier = Modifier.width(2.dp).height(30.dp).background(color = ItcmUi.Divider))
                                                            }
                                                            Column(modifier = Modifier.weight(1f).background(color = ItcmUi.Surface, shape = RoundedCornerShape(12.dp)).padding(12.dp)) {
                                                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                                                    val old = (h.oldValue ?: "").trim(); val new = (h.newValue ?: "").trim()
                                                                    Box(modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(color = accent.copy(alpha = 0.12f)).padding(horizontal = 8.dp, vertical = 3.dp)) {
                                                                        Text(
                                                                            when {
                                                                                old.isEmpty() && new.isEmpty() -> "Updated"
                                                                                old.isEmpty() -> new
                                                                                else -> "$old → $new"
                                                                            },
                                                                            color = accent, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, maxLines = 1
                                                                        )
                                                                    }
                                                                    if (isReschedule) Box(modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(color = ItcmUi.Pending.copy(alpha = 0.14f)).padding(horizontal = 8.dp, vertical = 3.dp)) {
                                                                        Text("RESCHEDULE", color = ItcmUi.Pending, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                                                                    }
                                                                }
                                                                Spacer(Modifier.height(4.dp))
                                                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                                                    Icon(Icons.Filled.Schedule, null, tint = ItcmUi.Muted, modifier = Modifier.size(12.dp))
                                                                    Text(h.changedAt?.let { formatVisitTime(it) } ?: "-", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                                    Icon(Icons.Filled.Person, null, tint = ItcmUi.Muted, modifier = Modifier.size(12.dp))
                                                                    Text(h.changedBy ?: "System", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.SemiBold, maxLines = 1)
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            2 -> {
                                item {
                                    when (val detailStateVal = detailState) {
                                        is TicketDetailUiState.Loading -> Box(Modifier.fillMaxWidth().padding(24.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator(Modifier.size(22.dp), color = ItcmUi.Brand) }
                                        is TicketDetailUiState.Error -> ItcmSurfaceCard { Text(detailStateVal.message, color = ItcmUi.Critical, style = MaterialTheme.typography.bodySmall) }
                                        is TicketDetailUiState.Success -> {
                                            val th = detailStateVal.response.history.sortedByDescending { it.changedAt ?: "" }.take(60)
                                            if (th.isEmpty()) {
                                                ItcmSurfaceCard {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                        Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.History, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                        Column { Text("No ticket history yet", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink); Text("Status, assignment and field changes will appear here.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
                                                    }
                                                }
                                            } else {
                                                var timelineFilter by remember { mutableStateOf("All") }
                                                val filters = listOf("All", "Field visits", "Notes", "Status")
                                                val filtered = th.filter { h ->
                                                    when (timelineFilter) {
                                                        "Field visits" -> h.fieldName.equals("FieldVisitStatus", true) || h.fieldName.equals("FieldVisitSignature", true)
                                                        "Notes" -> h.fieldName.equals("NoteAdded", true)
                                                        "Status" -> h.fieldName.equals("Status", true)
                                                        else -> true
                                                    }
                                                }
                                                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                                    ItcmSurfaceCard {
                                                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                            Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.History, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                            Column(modifier = Modifier.weight(1f)) {
                                                                Text("Ticket Timeline", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                                Text("${filtered.size} of ${th.size} events • newest first", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                            }
                                                        }
                                                        Spacer(Modifier.height(4.dp))
                                                        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                            items(filters) { f ->
                                                                val sel = timelineFilter == f
                                                                if (sel) Button(onClick = {}, colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand), contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)) { Text(f, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold) }
                                                                else OutlinedButton(onClick = { timelineFilter = f }, contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)) { Text(f, style = MaterialTheme.typography.labelSmall) }
                                                            }
                                                        }
                                                    }
                                                    if (filtered.isEmpty()) {
                                                        Text("No events match this filter.", color = ItcmUi.Muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(8.dp))
                                                    } else {
                                                        val grouped = filtered.groupBy { h -> formatDayKey(h.changedAt) }
                                                        grouped.forEach { (day, items) ->
                                                            Text("$day • ${items.size}", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Muted, modifier = Modifier.padding(top = 4.dp))
                                                            items.forEachIndexed { idx, h ->
                                                                val isReschedule = h.oldValue.equals("Cancelled", true) && h.newValue.equals("Scheduled", true)
                                                                val accent = timelineAccent(h.fieldName, h.newValue)
                                                                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                                                    Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.width(22.dp)) {
                                                                        Box(modifier = Modifier.size(26.dp).clip(RoundedCornerShape(13.dp)).background(color = accent.copy(alpha = 0.14f)), contentAlignment = Alignment.Center) {
                                                                            Icon(timelineIcon(h.fieldName), null, tint = accent, modifier = Modifier.size(14.dp))
                                                                        }
                                                                        if (idx != items.lastIndex) Box(modifier = Modifier.width(2.dp).height(30.dp).background(color = ItcmUi.Divider))
                                                                    }
                                                                    Column(modifier = Modifier.weight(1f).background(color = ItcmUi.Surface, shape = RoundedCornerShape(12.dp)).padding(12.dp)) {
                                                                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                                                            Box(modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(color = accent.copy(alpha = 0.12f)).padding(horizontal = 8.dp, vertical = 3.dp)) {
                                                                                Text(prettyFieldName(h.fieldName), color = accent, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, maxLines = 1)
                                                                            }
                                                                            if (isReschedule) Box(modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(color = ItcmUi.Pending.copy(alpha = 0.14f)).padding(horizontal = 8.dp, vertical = 3.dp)) {
                                                                                Text("RESCHEDULE", color = ItcmUi.Pending, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                                                                            }
                                                                        }
                                                                        Spacer(Modifier.height(4.dp))
                                                                        val old = (h.oldValue ?: "").trim(); val new = (h.newValue ?: "").trim()
                                                                        Text(
                                                                            when {
                                                                                old.isEmpty() && new.isEmpty() -> "Updated"
                                                                                old.isEmpty() -> new
                                                                                new.isEmpty() -> old
                                                                                else -> "$old → $new"
                                                                            },
                                                                            style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold, color = ItcmUi.Ink
                                                                        )
                                                                        Spacer(Modifier.height(2.dp))
                                                                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                                                            Icon(Icons.Filled.Schedule, null, tint = ItcmUi.Muted, modifier = Modifier.size(12.dp))
                                                                            Text(h.changedAt?.let { formatVisitTime(it) } ?: "-", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                                            Icon(Icons.Filled.Person, null, tint = ItcmUi.Muted, modifier = Modifier.size(12.dp))
                                                                            Text(h.changedBy ?: "System", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.SemiBold, maxLines = 1)
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else -> Text("No ticket timeline.", color = ItcmUi.Muted, style = MaterialTheme.typography.bodySmall)
                                    }
                                }
                            }
                            3 -> {
                                item {
                                    val atts = (visitsState as? FieldVisitsUiState.Success)?.attachments?.filter { visit != null && it.fieldVisitId == visit.fieldVisitId } ?: emptyList()
                                    val photoCount = atts.size
                                    val atCap = photoCount >= 20
                                    val selected = atts.find { it.attachmentId == selectedPhotoId }
                                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                        ItcmSurfaceCard {
                                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                Box(modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.CameraAlt, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                Column(modifier = Modifier.weight(1f)) {
                                                    Text("Evidence", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                    Text("$photoCount/20 photos • ${if (visit?.hasSignature == true) "signature on file" else "no signature yet"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                }
                                                Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(color = if (visit?.status.equals("Completed", true)) ItcmUi.Resolved.copy(alpha = 0.12f) else ItcmUi.Brand.copy(alpha = 0.10f)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                                                    Text(visit?.status ?: "No visit", color = if (visit?.status.equals("Completed", true)) ItcmUi.Resolved else ItcmUi.Brand, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                                                }
                                            }
                                            Spacer(Modifier.height(2.dp))
                                            LinearProgressIndicator(progress = (photoCount / 20f).coerceIn(0f, 1f), modifier = Modifier.fillMaxWidth().height(6.dp).clip(RoundedCornerShape(3.dp)), color = if (atCap) ItcmUi.Critical else ItcmUi.Brand, trackColor = ItcmUi.Divider)
                                            Spacer(Modifier.height(2.dp))
                                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                Button(
                                                    onClick = { if (visit != null && !atCap) showPhotoSource = true },
                                                    enabled = visit != null && !atCap,
                                                    colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand),
                                                    contentPadding = PaddingValues(horizontal = 8.dp, vertical = 0.dp),
                                                    modifier = Modifier.weight(1f).height(48.dp), shape = RoundedCornerShape(12.dp)
                                                ) { Icon(Icons.Filled.CameraAlt, null, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)); Text(if (atCap) "Limit 20/20" else "Add Photo", fontWeight = FontWeight.Bold, maxLines = 1) }
                                                OutlinedButton(
                                                    onClick = { if (visit != null) showSignature = true },
                                                    enabled = visit != null,
                                                    contentPadding = PaddingValues(horizontal = 8.dp, vertical = 0.dp),
                                                    modifier = Modifier.weight(1f).height(48.dp), shape = RoundedCornerShape(12.dp)
                                                ) { Icon(if (visit?.hasSignature == true) Icons.Filled.CheckCircle else Icons.Filled.Draw, null, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)); Text(if (visit?.hasSignature == true) "Signed ✓" else "Sign", fontWeight = FontWeight.SemiBold, maxLines = 1) }
                                            }
                                            if (atCap) Text("Photo limit reached — delete is not available yet.", color = ItcmUi.Critical, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.SemiBold)
                                        }
                                        if (atts.isEmpty()) {
                                            ItcmSurfaceCard {
                                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                    Box(modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(color = ItcmUi.Brand.copy(alpha = 0.10f)), contentAlignment = Alignment.Center) { Icon(Icons.Filled.CameraAlt, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp)) }
                                                    Column(modifier = Modifier.weight(1f)) {
                                                        Text("No photos yet", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                        Text("Add site photos (PNG/JPG, max 20 MB each). Tap a row to preview.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                                                    }
                                                }
                                                Button(onClick = { if (visit != null) showPhotoSource = true }, enabled = visit != null, colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand), modifier = Modifier.fillMaxWidth()) { Text("Add first photo", fontWeight = FontWeight.Bold) }
                                            }
                                        } else {
                                            Text("Photos", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Muted)
                                            atts.forEach { att ->
                                                val sel = selectedPhotoId == att.attachmentId
                                                Column(modifier = Modifier.fillMaxWidth().background(color = if (sel) ItcmUi.Brand.copy(alpha = 0.07f) else ItcmUi.Surface, shape = RoundedCornerShape(14.dp)).clickable {
                                                    selectedPhotoId = att.attachmentId
                                                    viewModel.loadFieldVisitPhoto(att.attachmentId)
                                                }.padding(12.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                                        Box(modifier = Modifier.size(40.dp).clip(RoundedCornerShape(10.dp)).background(color = if (sel) ItcmUi.Brand.copy(alpha = 0.14f) else ItcmUi.SurfaceSubtle), contentAlignment = Alignment.Center) {
                                                            Icon(Icons.Filled.CameraAlt, null, tint = if (sel) ItcmUi.Brand else ItcmUi.Muted, modifier = Modifier.size(20.dp))
                                                        }
                                                        Column(modifier = Modifier.weight(1f)) {
                                                            Text(att.fileName ?: "Photo", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.Bold, maxLines = 1)
                                                            Text("${formatBytes(att.fileSizeBytes)} • ${att.uploadedByName ?: "—"} • ${att.uploadedAt?.let { formatVisitTime(it) } ?: "-"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, maxLines = 1)
                                                        }
                                                        if (sel) Icon(Icons.Filled.Check, null, tint = ItcmUi.Brand, modifier = Modifier.size(18.dp))
                                                        else Icon(Icons.Filled.Visibility, null, tint = ItcmUi.Brand, modifier = Modifier.size(18.dp))
                                                    }
                                                }
                                            }
                                            ItcmSurfaceCard {
                                                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                                                    Column(modifier = Modifier.weight(1f)) {
                                                        Text(selected?.fileName ?: "Preview", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink, maxLines = 1)
                                                        Text(if (selected != null) "${formatBytes(selected.fileSizeBytes)} • ${selected.uploadedByName ?: "—"} • ${selected.uploadedAt?.let { formatVisitTime(it) } ?: "-"}" else "Select a photo above to preview", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, maxLines = 1)
                                                    }
                                                    if (selected != null) IconButton(onClick = { selectedPhotoId = null; viewModel.clearFieldPhoto() }) { Icon(Icons.Filled.Close, null, tint = ItcmUi.Muted) }
                                                }
                                                Spacer(Modifier.height(4.dp))
                                                if (photoBase64 != null) {
                                                    val bmp = try { val bytes = Base64.decode(photoBase64, Base64.DEFAULT); BitmapFactory.decodeByteArray(bytes, 0, bytes.size) } catch (_: Exception) { null }
                                                    if (bmp != null) Image(bitmap = bmp.asImageBitmap(), contentDescription = "Preview", modifier = Modifier.fillMaxWidth().height(260.dp).clip(RoundedCornerShape(14.dp)).background(color = ItcmUi.SurfaceSubtle))
                                                    else Text("Preview failed to decode.", color = ItcmUi.Critical, style = MaterialTheme.typography.labelSmall)
                                                } else if (selectedPhotoId != null) {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.padding(8.dp)) { CircularProgressIndicator(Modifier.size(18.dp), color = ItcmUi.Brand); Text("Loading preview…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
                                                } else {
                                                    Text("No photo selected.", color = ItcmUi.Muted, style = MaterialTheme.typography.labelSmall)
                                                }
                                            }
                                        }
                                        ItcmSurfaceCard {
                                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                                Box(modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(color = if (visit?.hasSignature == true) ItcmUi.Resolved.copy(alpha = 0.12f) else ItcmUi.SurfaceSubtle), contentAlignment = Alignment.Center) {
                                                    Icon(if (visit?.hasSignature == true) Icons.Filled.CheckCircle else Icons.Filled.Draw, null, tint = if (visit?.hasSignature == true) ItcmUi.Resolved else ItcmUi.Muted, modifier = Modifier.size(20.dp))
                                                }
                                                Column(modifier = Modifier.weight(1f)) {
                                                    Text("Customer Signature", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                                    Text(if (visit?.hasSignature == true) "Signed • tap Update to replace" else "Not captured — required before completing", style = MaterialTheme.typography.labelSmall, color = if (visit?.hasSignature == true) ItcmUi.Resolved else ItcmUi.Muted, fontWeight = FontWeight.SemiBold)
                                                }
                                                OutlinedButton(onClick = { if (visit != null) showSignature = true }, enabled = visit != null, contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)) { Text(if (visit?.hasSignature == true) "Update" else "Capture") }
                                            }
                                            if (visit?.hasSignature == true) {
                                                Spacer(Modifier.height(4.dp))
                                                if (signatureBase64 != null) {
                                                    val sigBmp = try { val b = Base64.decode(signatureBase64, Base64.DEFAULT); BitmapFactory.decodeByteArray(b, 0, b.size) } catch (_: Exception) { null }
                                                    if (sigBmp != null) Image(bitmap = sigBmp.asImageBitmap(), contentDescription = "Signature", modifier = Modifier.fillMaxWidth().height(150.dp).clip(RoundedCornerShape(12.dp)).background(color = androidx.compose.ui.graphics.Color.White).padding(8.dp))
                                                    else Text("✓ Signature on file", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.Bold)
                                                } else {
                                                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.padding(6.dp)) { CircularProgressIndicator(Modifier.size(16.dp), color = ItcmUi.Brand, strokeWidth = 2.dp); Text("Loading signature…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        if (showPhotoSource) {
            AlertDialog(
                onDismissRequest = { showPhotoSource = false },
                containerColor = ItcmUi.Surface,
                title = { Text("Add Photo", color = ItcmUi.Ink, fontWeight = FontWeight.Bold) },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Choose a source for the site photo (PNG/JPG, max 20 MB).", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                        Button(
                            onClick = { showPhotoSource = false; launchCamera() },
                            colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand),
                            modifier = Modifier.fillMaxWidth()
                        ) { Icon(Icons.Filled.CameraAlt, null, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)); Text("Open Camera", fontWeight = FontWeight.Bold) }
                        OutlinedButton(
                            onClick = { showPhotoSource = false; photoLauncher.launch("image/*") },
                            modifier = Modifier.fillMaxWidth()
                        ) { Icon(Icons.Filled.PhotoLibrary, null, modifier = Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)); Text("Open Gallery") }
                    }
                },
                confirmButton = {},
                dismissButton = { TextButton(onClick = { showPhotoSource = false }) { Text("Cancel") } }
            )
        }
        if (showSignature) {
            SignatureCaptureDialog(
                onDismiss = { showSignature = false },
                onSave = { bmp ->
                    val visit = (visitsState as? FieldVisitsUiState.Success)?.visits?.firstOrNull() ?: return@SignatureCaptureDialog
                    val baos = ByteArrayOutputStream()
                    bmp.compress(Bitmap.CompressFormat.PNG, 90, baos)
                    val base64 = Base64.encodeToString(baos.toByteArray(), Base64.NO_WRAP)
                    viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Completed", signatureBase64 = base64)
                    showSignature = false
                }
            )
        }
    }
}

@Composable
private fun FieldVisitDetail(visit: com.example.yakultscanner.api.CallFieldVisitDto, attachments: List<com.example.yakultscanner.api.CallFieldVisitAttachmentDto>) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(color = fieldStatusBg(visit.status)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                Text(visit.status ?: "-", color = fieldStatusFg(visit.status), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
            }
            visit.scheduledAt?.let { Text("Scheduled: ${formatVisitTime(it)}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
        }
        Text("Technician: ${visit.technicianName ?: "-"}", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold)
        if (!visit.notes.isNullOrBlank()) Text(visit.notes!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            visit.completedAt?.let { Text("Completed: ${formatVisitTime(it)}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.Bold) }
        }
        if (visit.hasSignature) Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) { Icon(Icons.Filled.CheckCircle, null, tint = ItcmUi.Resolved, modifier = Modifier.size(14.dp)); Text("Customer signature on file", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.SemiBold) }
    }
}

@Composable
private fun SignatureCaptureDialog(onDismiss: () -> Unit, onSave: (Bitmap) -> Unit) {
    var sigView by remember { mutableStateOf<SignatureView?>(null) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = ItcmUi.Surface,
        title = { Text("Customer Signature", color = ItcmUi.Ink, fontWeight = FontWeight.Bold) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Ask the customer to sign. Signature is saved with the field visit (choice 2.B).", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                SignaturePad(label = "Sign here", onViewReady = { sigView = it })
            }
        },
        confirmButton = {
            Button(onClick = {
                val bmp = sigView?.getSignatureBitmap()
                if (bmp != null && sigView?.hasSignature == true) onSave(bmp) else onDismiss()
            }, colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand)) { Text("Save Signature") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

private fun formatBytes(b: Int?): String {
    val v = b ?: 0
    if (v < 1024) return "$v B"
    if (v < 1024 * 1024) return "${v / 1024} KB"
    return String.format(java.util.Locale.US, "%.1f MB", v / (1024.0 * 1024.0))
}

private fun formatDayKey(iso: String?): String {
    if (iso.isNullOrBlank()) return "Unknown date"
    return try {
        val p = listOf("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd")
        var d: java.util.Date? = null
        for (fmt in p) try { val sdf = java.text.SimpleDateFormat(fmt, java.util.Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }; d = sdf.parse(iso); if (d != null) break } catch (_: Exception) {}
        if (d != null) java.text.SimpleDateFormat("MMM d, yyyy", java.util.Locale.US).format(d) else iso
    } catch (_: Exception) { iso }
}

private fun prettyFieldName(raw: String?): String {
    if (raw.isNullOrBlank()) return "Update"
    return raw.replace(Regex("(?<=[a-z0-9])(?=[A-Z])"), " ").replace('_', ' ').trim().split(' ').joinToString(" ") { w -> w.replaceFirstChar { it.uppercase() } }
}

private fun timelineAccent(field: String?, newValue: String?): androidx.compose.ui.graphics.Color = when {
    field.equals("FieldVisitStatus", true) && newValue.equals("Completed", true) -> ItcmUi.Resolved
    field.equals("FieldVisitStatus", true) && newValue.equals("Cancelled", true) -> ItcmUi.Critical
    field.equals("FieldVisitStatus", true) -> ItcmUi.Brand
    field.equals("FieldVisitSignature", true) -> ItcmUi.Resolved
    field.equals("NoteAdded", true) -> ItcmUi.Muted
    field.equals("Status", true) -> ItcmUi.Active
    field.equals("Priority", true) -> ItcmUi.High
    field.equals("Created", true) -> ItcmUi.Closed
    else -> ItcmUi.Muted
}

private fun timelineIcon(field: String?): androidx.compose.ui.graphics.vector.ImageVector = when {
    field.equals("FieldVisitStatus", true) -> Icons.Filled.Refresh
    field.equals("FieldVisitSignature", true) -> Icons.Filled.Draw
    field.equals("NoteAdded", true) -> Icons.Filled.StickyNote2
    field.equals("Status", true) -> Icons.Filled.Schedule
    field.equals("Priority", true) -> Icons.Filled.Warning
    field.equals("Created", true) -> Icons.Filled.CheckCircle
    else -> Icons.Filled.History
}

private fun fieldStatusBg(s: String?): androidx.compose.ui.graphics.Color = when (s?.lowercase()) {
    "completed" -> ItcmUi.Resolved.copy(alpha = 0.12f); "cancelled" -> ItcmUi.Critical.copy(alpha = 0.12f); "checkedin" -> ItcmUi.BrandSoft; "checkedout" -> ItcmUi.Active.copy(alpha = 0.12f); "intransit" -> ItcmUi.Pending.copy(alpha = 0.12f); else -> ItcmUi.SurfaceSubtle
}
private fun fieldStatusFg(s: String?): androidx.compose.ui.graphics.Color = when (s?.lowercase()) {
    "completed" -> ItcmUi.Resolved; "cancelled" -> ItcmUi.Critical; "checkedin" -> ItcmUi.Brand; "checkedout" -> ItcmUi.Active; "intransit" -> ItcmUi.Pending; else -> ItcmUi.Muted
}
private fun formatVisitTime(iso: String?): String {
    if (iso.isNullOrBlank()) return "-"
    if (iso.trim().startsWith("/Date(")) {
        return try {
            val m = Regex("""\/Date\((\d+)([+-]\d+)?\)\/""").find(iso)
            val ms = m?.groupValues?.get(1)?.toLongOrNull()
            if (ms != null) java.text.SimpleDateFormat("MMM d, h:mm a", java.util.Locale.US).format(java.util.Date(ms)) else iso
        } catch (_:Exception){ iso }
    }
    return try {
        val p = listOf("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd")
        var d: java.util.Date? = null
        for (fmt in p) try { val sdf = java.text.SimpleDateFormat(fmt, java.util.Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }; d = sdf.parse(iso); if (d!=null) break } catch (_:Exception){}
        if (d!=null) java.text.SimpleDateFormat("MMM d, h:mm a", java.util.Locale.US).format(d) else iso
    } catch (_:Exception){ iso }
}
