package com.example.yakultscanner.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.yakultscanner.data.model.ItemEditState
import com.example.yakultscanner.api.SetItemUpdateDto

@Composable
fun ItemStatusChip(
    status: String,
    modifier: Modifier = Modifier
) {
    val color = when (status.lowercase()) {
        "good", "functional", "ok" -> Color(0xFF4CAF50) // Green
        "defective", "broken", "bad" -> Color(0xFFF44336) // Red
        "missing" -> Color(0xFFFF9800) // Orange
        "repaired" -> Color(0xFFA36A72) // Warm mauve
        else -> MaterialTheme.colorScheme.secondary
    }

    Surface(
        modifier = modifier,
        color = color.copy(alpha = 0.1f),
        shape = RoundedCornerShape(16.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, color.copy(alpha = 0.5f))
    ) {
        Text(
            text = status.uppercase(),
            color = color,
            fontSize = 11.sp,
            fontWeight = FontWeight.Bold,
            maxLines = 1,
            softWrap = false,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
        )
    }
}

@Composable
fun ReportIssueDialog(
    editState: ItemEditState,
    onDismiss: () -> Unit,
    onSave: (ItemEditState) -> Unit
) {
    var status by remember { mutableStateOf(editState.statusInput) }
    var remark by remember { mutableStateOf(editState.remarkInput) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Update Item Status") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
                Text("Item: ${editState.base.type ?: editState.base.description}")
                Text("Current Status: ${editState.base.status}", style = MaterialTheme.typography.bodySmall)
                
                OutlinedTextField(
                    value = status,
                    onValueChange = { status = it },
                    label = { Text("New Status") },
                    singleLine = true
                )
                
                OutlinedTextField(
                    value = remark,
                    onValueChange = { remark = it },
                    label = { Text("Remarks / Issue Description") },
                    minLines = 3
                )
            }
        },
        confirmButton = {
            Button(onClick = {
                onSave(editState.copy(statusInput = status, remarkInput = remark, isEditing = true))
            }) {
                Text("Update")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel")
            }
        }
    )
}

@Composable
fun Footer(
    onGeneratePdf: () -> Unit,
    onUpload: () -> Unit,
    onViewUploads: () -> Unit,
    onDeploy: () -> Unit,
    onSyncNow: () -> Unit,
    canUpload: Boolean,
    canViewUploads: Boolean,
    canDeploy: Boolean,
    canSync: Boolean,
    isUploading: Boolean,
    isSyncing: Boolean,
    pendingCount: Int,
    deployed: Boolean = false
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp, bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Surface(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(18.dp),
            color = ScannerWorkspaceUi.SurfaceSubtle
        ) {
            Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text("Field actions", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, color = ScannerWorkspaceUi.Ink)
                Text(
                    text = when {
                        deployed -> "This set has been deployed. You can still review its files and history."
                        canUpload -> "Save your item changes before uploading them to the office system."
                        canSync -> "$pendingCount update(s) are waiting to sync."
                        else -> "Review signatures, attachments, and deployment status before leaving."
                    },
                    style = MaterialTheme.typography.bodySmall,
                    color = ScannerWorkspaceUi.Muted
                )
            }
        }

        if (canSync) {
            Button(
                onClick = onSyncNow,
                enabled = !isSyncing && pendingCount > 0,
                modifier = Modifier.fillMaxWidth().height(50.dp),
                colors = ButtonDefaults.buttonColors(containerColor = ScannerWorkspaceUi.Warning, contentColor = Color.White),
                shape = RoundedCornerShape(16.dp)
            ) {
                if (isSyncing) {
                    CircularProgressIndicator(modifier = Modifier.size(19.dp), color = Color.White, strokeWidth = 2.dp)
                    Spacer(Modifier.width(8.dp))
                    Text("Syncing…")
                } else {
                    Text("Sync $pendingCount pending update(s)", fontWeight = FontWeight.Bold)
                }
            }
        }

        Button(
            onClick = onDeploy,
            enabled = canDeploy && !deployed,
            modifier = Modifier.fillMaxWidth().height(54.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = if (deployed) ScannerWorkspaceUi.Success else ScannerWorkspaceUi.Brand,
                disabledContainerColor = ScannerWorkspaceUi.SurfaceSubtle,
                disabledContentColor = ScannerWorkspaceUi.Muted,
                contentColor = Color.White
            ),
            shape = RoundedCornerShape(16.dp)
        ) {
            Text(
                when {
                    deployed -> "Deployed"
                    !canDeploy -> "Deploy requires a token scan"
                    else -> "Deploy field job"
                },
                fontWeight = FontWeight.Bold
            )
        }

        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(
                onClick = onUpload,
                enabled = canUpload && !isUploading,
                modifier = Modifier.weight(1f).height(50.dp),
                shape = RoundedCornerShape(16.dp)
            ) {
                if (isUploading) {
                    CircularProgressIndicator(modifier = Modifier.size(19.dp), color = Color.White, strokeWidth = 2.dp)
                    Spacer(Modifier.width(6.dp))
                    Text("Uploading…")
                } else {
                    Text("Upload changes", fontWeight = FontWeight.Bold)
                }
            }
            if (canViewUploads) {
                OutlinedButton(onClick = onViewUploads, modifier = Modifier.weight(1f).height(50.dp), shape = RoundedCornerShape(16.dp)) {
                    Text("View uploads")
                }
            }
        }

        OutlinedButton(onClick = onGeneratePdf, modifier = Modifier.fillMaxWidth().height(48.dp), shape = RoundedCornerShape(16.dp)) {
            Text("Generate PDF copy")
        }
    }
}