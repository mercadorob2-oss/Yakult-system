package com.example.yakultscanner.ui.screens.fieldwork

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.api.CallTicketHistoryDto
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
fun FieldWorkTimelineSection(
    photoCount: Int,
    hasSignature: Boolean,
    fieldHistory: List<CallTicketHistoryDto>
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text("Current: $photoCount photo(s) • Signature ${if (hasSignature) "on file" else "none"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted, fontWeight = FontWeight.Bold, modifier = Modifier.padding(bottom = 4.dp))
        if (fieldHistory.isEmpty()) {
            Text("No field status history yet — just scheduled.", color = ItcmUi.Muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(8.dp))
        } else {
            fieldHistory.forEachIndexed { idx, h ->
                val isReschedule = h.oldValue.equals("Cancelled", true) && h.newValue.equals("Scheduled", true)
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.width(20.dp)) {
                        Box(modifier = Modifier.size(12.dp).clip(RoundedCornerShape(6.dp)).background(if (h.newValue.equals("Completed",true)) ItcmUi.Resolved else if (h.newValue.equals("Cancelled",true)) ItcmUi.Critical else ItcmUi.Brand))
                        if (idx != fieldHistory.lastIndex) Box(modifier = Modifier.width(2.dp).height(32.dp).background(color = ItcmUi.Divider))
                    }
                    Column(modifier = Modifier.weight(1f).background(color = if (isReschedule) ItcmUi.Pending.copy(alpha=0.12f) else ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(10.dp)).padding(12.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Text("${h.oldValue ?: "-"} -> ${h.newValue ?: "-"}", style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                            if (isReschedule) Text("RESCHEDULE", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Pending, fontWeight = FontWeight.Bold, modifier = Modifier.background(color = ItcmUi.SurfaceSubtle, shape = RoundedCornerShape(4.dp)).padding(horizontal = 6.dp, vertical = 2.dp))
                        }
                        Text("${h.changedAt?.let { formatVisitTimeLocal(it) } ?: "-"} • ${h.changedBy ?: "System"}", style = MaterialTheme.typography.labelSmall, color = ItcmUi.Muted)
                    }
                }
            }
        }
    }
}


