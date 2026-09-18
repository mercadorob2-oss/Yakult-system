package com.example.yakultscanner.ui.screens.fieldwork

import android.graphics.BitmapFactory
import android.util.Base64
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.api.CallFieldVisitAttachmentDto
import com.example.yakultscanner.ui.components.ItcmUi

@Composable
fun FieldWorkEvidenceSection(
    attachments: List<CallFieldVisitAttachmentDto>,
    hasSignature: Boolean,
    photoBase64: String?,
    signatureBase64: String?,
    selectedPhotoId: Int?,
    onPhotoSelected: (Int) -> Unit,
    onAddPhoto: () -> Unit,
    onAddSignature: () -> Unit,
    isLoadingPhoto: Boolean
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = onAddPhoto, modifier = Modifier.weight(1f)) { Icon(Icons.Filled.CameraAlt, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(6.dp)); Text("Add Photo") }
            OutlinedButton(onClick = onAddSignature, modifier = Modifier.weight(1f)) { Icon(Icons.Filled.Edit, null, modifier = Modifier.size(16.dp)); Spacer(Modifier.width(6.dp)); Text(if (hasSignature) "Update Signature" else "Add Signature") }
        }
        if (attachments.isEmpty()) {
            Text("No photos yet.", color = ItcmUi.Muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(8.dp))
        } else {
            attachments.forEach { att ->
                Row(modifier = Modifier.fillMaxWidth().background(if (selectedPhotoId == att.attachmentId) ItcmUi.Brand.copy(alpha=0.08f) else ItcmUi.SurfaceSubtle, RoundedCornerShape(8.dp)).clickable { onPhotoSelected(att.attachmentId) }.padding(10.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(att.fileName ?: "Photo", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold)
                        Text("${(att.fileSizeBytes ?: 0)/1024} KB • ${att.uploadedByName ?: "—"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                    Icon(Icons.Filled.Visibility, null, tint = ItcmUi.Brand, modifier = Modifier.size(18.dp))
                }
            }
            if (photoBase64 != null) {
                val bmp = try { val bytes = Base64.decode(photoBase64, Base64.DEFAULT); BitmapFactory.decodeByteArray(bytes, 0, bytes.size) } catch (_: Exception) { null }
                if (bmp != null) Image(bitmap = bmp.asImageBitmap(), contentDescription = "Preview", modifier = Modifier.fillMaxWidth().height(240.dp).clip(RoundedCornerShape(12.dp)).background(color = ItcmUi.SurfaceSubtle))
            } else if (selectedPhotoId != null) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.padding(8.dp)) { CircularProgressIndicator(Modifier.size(16.dp), color = ItcmUi.Brand); Text("Loading preview…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
            }
        }
        HorizontalDivider(color = ItcmUi.Divider)
        Text("Customer Signature", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
        if (hasSignature) {
            if (signatureBase64 != null) {
                val sigBmp = try { val b = Base64.decode(signatureBase64, Base64.DEFAULT); BitmapFactory.decodeByteArray(b,0,b.size) } catch (_:Exception){ null }
                if (sigBmp != null) Image(bitmap = sigBmp.asImageBitmap(), contentDescription = "Signature", modifier = Modifier.fillMaxWidth().height(140.dp).clip(RoundedCornerShape(10.dp)).background(androidx.compose.ui.graphics.Color.White).padding(8.dp))
                else Text("✓ Signature on file", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Resolved, fontWeight = FontWeight.Bold)
            } else {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.padding(6.dp)) { CircularProgressIndicator(Modifier.size(14.dp), color = ItcmUi.Brand, strokeWidth=2.dp); Text("Loading signature…", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted) }
            }
        } else {
            Text("No signature yet — tap Add Signature to capture.", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
        }
    }
}

