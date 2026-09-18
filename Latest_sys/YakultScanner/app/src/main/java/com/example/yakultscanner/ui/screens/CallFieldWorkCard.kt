package com.example.yakultscanner.ui.screens

import android.graphics.Bitmap
import android.util.Base64
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import com.example.yakultscanner.api.CallFieldVisitDto
import com.example.yakultscanner.ui.components.ItcmUi
import com.example.yakultscanner.ui.components.ItcmSurfaceCard
import com.example.yakultscanner.ui.components.SignaturePad
import com.example.yakultscanner.ui.components.SignatureView
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.FieldVisitsUiState
import com.example.yakultscanner.viewmodels.FieldVisitActionUiState
import java.io.ByteArrayOutputStream

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CallFieldWorkCard(
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel(),
    onViewDetails: (() -> Unit)? = null
) {
    val visitsState by viewModel.fieldVisitsState.collectAsState()
    val actionState by viewModel.fieldVisitActionState.collectAsState()
    val pending by viewModel.pendingFieldActions.collectAsState()
    var showSchedule by remember { mutableStateOf(false) }
    var notesDraft by rememberSaveable(ticketId) { mutableStateOf("") }
    var showSignature by remember { mutableStateOf(false) }
    var signatureView by remember { mutableStateOf<SignatureView?>(null) }
    val context = LocalContext.current

    LaunchedEffect(ticketId) { viewModel.loadFieldVisits(ticketId) }
    LaunchedEffect(Unit) { viewModel.loadItEmployees() }
    val itEmployees by viewModel.itEmployees.collectAsState()
    var selectedTechId by remember(ticketId) { mutableStateOf<Int?>(null) }
    var techExpanded by remember { mutableStateOf(false) }
    // Keep detailState observed but don't trigger load here — parent ItcmTicketDetailScreen already loads it
    val detailState by viewModel.detailState.collectAsState()
    val photoBase64 by viewModel.fieldPhotoBase64.collectAsState()
    val signatureBase64 by viewModel.fieldSignatureBase64.collectAsState()

    val photoLauncher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) {
            val visit = (visitsState as? FieldVisitsUiState.Success)?.visits?.firstOrNull() ?: return@rememberLauncherForActivityResult
            try {
                val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() } ?: return@rememberLauncherForActivityResult
                if (bytes.size > 20 * 1024 * 1024) {
                    android.widget.Toast.makeText(context, "File too large (max 20 MB)", android.widget.Toast.LENGTH_LONG).show()
                    return@rememberLauncherForActivityResult
                }
                val base64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
                val name = uri.lastPathSegment?.substringAfterLast('/') ?: "field-photo.jpg"
                val mime = context.contentResolver.getType(uri) ?: "image/jpeg"
                viewModel.uploadFieldVisitPhoto(visit.fieldVisitId, ticketId, name, mime, base64)
            } catch (_: Exception) {}
        }
    }

    ItcmSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Column {
                Text("Field Work", style = MaterialTheme.typography.titleSmall, color = ItcmUi.Ink, fontWeight = FontWeight.Bold)
                Text("One visit per ticket • Scheduled -> Completed/Cancelled • Cancelled reschedulable", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
            }
            if (pending.isNotEmpty()) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    Icon(Icons.Filled.Warning, null, tint = ItcmUi.Pending, modifier = Modifier.size(14.dp))
                    Text("${pending.size} pending", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Pending, fontWeight = FontWeight.Bold)
                }
            }
        }

        when (val s = visitsState) {
            FieldVisitsUiState.Idle, FieldVisitsUiState.Loading -> Box(Modifier.fillMaxWidth().padding(12.dp), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(color = ItcmUi.Brand, modifier = Modifier.size(22.dp))
            }
            is FieldVisitsUiState.Error -> {
                Text(s.message, color = ItcmUi.Critical, style = MaterialTheme.typography.bodySmall)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(onClick = { viewModel.loadFieldVisits(ticketId) }) { Text("Retry") }
                    if (pending.isNotEmpty()) TextButton(onClick = { viewModel.retryPendingFieldWork(ticketId) }) { Text("Sync pending") }
                }
            }
                        is FieldVisitsUiState.Success -> {
                val visit = s.visits.firstOrNull()
                val attachments = s.attachments.filter { visit != null && it.fieldVisitId == visit.fieldVisitId }
                if (visit == null) {
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(12.dp)).padding(14.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Box(modifier = Modifier.size(36.dp).clip(RoundedCornerShape(8.dp)).background(color = ItcmUi.Brand.copy(alpha=0.12f)), contentAlignment = Alignment.Center) {
                                Icon(Icons.Filled.History, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp))
                            }
                            Column {
                                Text("No field visit yet", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                Text("Schedule one visit per ticket • pick technician • add notes", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                            }
                        }
                        if (itEmployees.isNotEmpty()) {
                            ExposedDropdownMenuBox(expanded = techExpanded, onExpandedChange = { techExpanded = !techExpanded }) {
                                OutlinedTextField(
                                    value = itEmployees.find { it.id == selectedTechId }?.name ?: "",
                                    onValueChange = {},
                                    readOnly = true,
                                    label = { Text("Technician") },
                                    placeholder = { Text("Select technician") },
                                    trailingIcon = { Icon(Icons.Filled.Edit, null, modifier = Modifier.size(16.dp)) },
                                    modifier = Modifier.fillMaxWidth().menuAnchor(),
                                    shape = RoundedCornerShape(10.dp)
                                )
                                ExposedDropdownMenu(expanded = techExpanded, onDismissRequest = { techExpanded = false }) {
                                    DropdownMenuItem(text = { Text("— No technician —") }, onClick = { selectedTechId = null; techExpanded = false })
                                    itEmployees.forEach { emp ->
                                        DropdownMenuItem(text = { Text(emp.name) }, onClick = { selectedTechId = emp.id; techExpanded = false })
                                    }
                                }
                            }
                        }
                        OutlinedTextField(value = notesDraft, onValueChange = { notesDraft = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Notes (optional)") }, placeholder = { Text("Reason for visit, location, etc.") }, maxLines = 3, shape = RoundedCornerShape(10.dp))
                        Button(onClick = { viewModel.scheduleFieldVisit(ticketId, technicianEmpId = selectedTechId, notes = notesDraft.takeIf { it.isNotBlank() }) }, modifier = Modifier.fillMaxWidth(), colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand)) {
                            Icon(Icons.Filled.Warning, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(6.dp)); Text("Schedule Visit", fontWeight = FontWeight.Bold)
                        }
                        TextButton(onClick = { onViewDetails?.invoke() }, modifier = Modifier.fillMaxWidth()) { Text("View details → Photos & Timeline") }
                    }
                } else {
                    // Summary card - tap View Details for full tabs (Overview/Field Timeline/Ticket Timeline/Photos)
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(10.dp)).padding(12.dp)) {
                            Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(fieldStatusBg(visit.status)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                                Text(visit.status ?: "-", color = fieldStatusFg(visit.status), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                            }
                            Column(modifier = Modifier.weight(1f)) {
                                Text("Visit #${visit.fieldVisitId} • ${visit.technicianName ?: "—"}", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                                Text("Scheduled: ${visit.scheduledAt?.let { formatVisitTime(it) } ?: "—"} • ${attachments.size} photo(s) • ${if (visit.hasSignature) "✓ Signature" else "No signature"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                            }
                            Icon(Icons.Filled.Visibility, null, tint = ItcmUi.Brand, modifier = Modifier.size(20.dp))
                        }
                        if (!visit.notes.isNullOrBlank()) Text(visit.notes!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted, modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(8.dp)).padding(10.dp))
                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onClick = { onViewDetails?.invoke() }, modifier = Modifier.weight(1f)) { Icon(Icons.Filled.History, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(4.dp)); Text("View Details") }
                            val status = visit.status ?: "Scheduled"
                            if (status.equals("Scheduled", true)) {
                                Button(onClick = { viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Completed") }, modifier = Modifier.weight(1f), colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Resolved)) { Text("Complete") }
                            } else if (status.equals("Cancelled", true)) {
                                Button(onClick = { viewModel.fieldVisitAction(visit.fieldVisitId, ticketId, "Scheduled", technicianEmpId = selectedTechId ?: visit.technicianEmpId) }, modifier = Modifier.weight(1f).height(44.dp), shape = RoundedCornerShape(10.dp), colors = ButtonDefaults.buttonColors(containerColor = ItcmUi.Brand)) { Icon(Icons.Filled.Refresh, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(6.dp)); Text("Reschedule", fontWeight = FontWeight.Bold) }
                            }
                        }
                        if (actionState is FieldVisitActionUiState.Error) {
                            val err = (actionState as FieldVisitActionUiState.Error).message
                            val friendly = if (err.contains("already has a field visit", true)) "Already has visit. Tap View Details → Reschedule if cancelled." else err
                            Text(friendly, color = ItcmUi.Critical, style = MaterialTheme.typography.labelSmall)
                        }
                    }
                }
            }
            }
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

@Composable
private fun FieldVisitDetail(visit: CallFieldVisitDto, attachments: List<com.example.yakultscanner.api.CallFieldVisitAttachmentDto>) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(fieldStatusBg(visit.status)).padding(horizontal = 8.dp, vertical = 4.dp)) {
                Text(visit.status ?: '-'.toString(), color = fieldStatusFg(visit.status), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
            }
            visit.scheduledAt?.let { Text("Scheduled: ${formatVisitTime(it)}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
        }
        Text("Technician: ${visit.technicianName ?: '-'.toString()}", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold)
        if (!visit.notes.isNullOrBlank()) Text(visit.notes!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            visit.completedAt?.let { Text("Completed: ${formatVisitTime(it)}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.Bold) }
        }
        if (visit?.hasSignature == true) Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) { Icon(Icons.Filled.CheckCircle, null, tint = ItcmUi.Resolved, modifier = Modifier.size(14.dp)); Text("Customer signature on file", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.SemiBold) }
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

private fun fieldStatusBg(s: String?): androidx.compose.ui.graphics.Color = when (s?.lowercase()) {
    "completed" -> ItcmUi.Resolved.copy(alpha = 0.12f); "cancelled" -> ItcmUi.Critical.copy(alpha = 0.12f); "checkedin" -> ItcmUi.BrandSoft; "checkedout" -> ItcmUi.Active.copy(alpha = 0.12f); "intransit" -> ItcmUi.Pending.copy(alpha = 0.12f); else -> ItcmUi.SurfaceSubtle
}
private fun fieldStatusFg(s: String?): androidx.compose.ui.graphics.Color = when (s?.lowercase()) {
    "completed" -> ItcmUi.Resolved; "cancelled" -> ItcmUi.Critical; "checkedin" -> ItcmUi.Brand; "checkedout" -> ItcmUi.Active; "intransit" -> ItcmUi.Pending; else -> ItcmUi.Muted
}
private fun formatVisitTime(iso: String?): String {
    if (iso.isNullOrBlank()) return "-"
    // Handle legacy .NET JavaScriptSerializer "/Date(1788230764000)/" or "/Date(1788230764000+0800)/"
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







