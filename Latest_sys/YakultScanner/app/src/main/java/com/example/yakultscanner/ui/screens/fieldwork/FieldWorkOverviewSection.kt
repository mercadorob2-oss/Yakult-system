package com.example.yakultscanner.ui.screens.fieldwork

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.ui.components.ItcmUi

private fun formatVisitTimeLocal(iso: String?): String {
    if (iso.isNullOrBlank()) return "-"
    return try {
        val p = listOf("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd")
        var d: java.util.Date? = null
        for (fmt in p) try { val sdf = java.text.SimpleDateFormat(fmt, java.util.Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }; d = sdf.parse(iso); if (d!=null) break } catch (_:Exception){}
        if (d!=null) java.text.SimpleDateFormat("MMM d, h:mm a", java.util.Locale.US).format(d) else iso
    } catch (_:Exception){ iso }
}

@Composable
fun FieldWorkOverviewSection(
    visit: com.example.yakultscanner.api.CallFieldVisitDto?,
    ticketCode: String?,
    ticketStatus: String?,
    issue: String?,
    department: String?,
    branch: String?,
    photoCount: Int,
    hasSignature: Boolean
) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(10.dp)).padding(12.dp)) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Column { Text("Visit #", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text("${visit?.fieldVisitId ?: "—"}", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink) }
            Column { Text("Photos", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text("$photoCount", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = if (photoCount>0) ItcmUi.Brand else ItcmUi.Muted) }
            Column { Text("Signature", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(if (hasSignature) "✓ On file" else "—", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = if (hasSignature) ItcmUi.Resolved else ItcmUi.Muted) }
        }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Column { Text("Scheduled", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit?.scheduledAt?.let { formatVisitTimeLocal(it) } ?: "—", style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink) }
            Column { Text("Completed", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted); Text(visit?.completedAt?.let { formatVisitTimeLocal(it) } ?: "Not yet", style = MaterialTheme.typography.bodySmall, color = if (visit?.completedAt!=null) ItcmUi.Resolved else ItcmUi.Muted, fontWeight = FontWeight.Bold) }
        }
        if (!visit?.notes.isNullOrBlank()) { Text("Notes:", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold); Text(visit!!.notes!!, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink) }
        HorizontalDivider(color = ItcmUi.Divider)
        Text("Ticket: ${ticketCode ?: "—"} • ${ticketStatus ?: ""}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold)
        if (!issue.isNullOrBlank()) Text(issue, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Ink)
        Text("${department ?: "—"} • ${branch ?: "—"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
    }
}
