package com.example.yakultscanner.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.History
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.yakultscanner.api.DeploymentHistoryDto

@Composable
fun DeploymentTimeline(
    history: List<DeploymentHistoryDto>,
    modifier: Modifier = Modifier
) {
    LazyColumn(
        modifier = modifier,
        verticalArrangement = Arrangement.spacedBy(0.dp),
        contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp)
    ) {
        items(history) { event ->
            TimelineItem(
                event = event,
                isLast = event == history.last()
            )
        }
        
        if (history.isEmpty()) {
            item {
                EmptyTimelineState()
            }
        }
    }
}

@Composable
private fun TimelineItem(
    event: DeploymentHistoryDto,
    isLast: Boolean
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Timeline line and dot
        Column(
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Status dot
            val statusColor = when (event.newStatus?.lowercase()) {
                "deployed", "delivered", "confirmed" -> Color(0xFF4CAF50)
                "pending" -> Color(0xFFFF9800)
                "cancelled" -> Color(0xFFF44336)
                else -> MaterialTheme.colorScheme.primary
            }
            
            Surface(
                shape = CircleShape,
                color = statusColor,
                modifier = Modifier.size(12.dp)
            ) { }
            
            // Connecting line
            if (!isLast) {
                Box(
                    modifier = Modifier
                        .width(2.dp)
                        .height(60.dp)
                        .padding(top = 4.dp)
                        .background(MaterialTheme.colorScheme.outlineVariant)
                )
            }
        }
        
        // Event card
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = if (isLast) 0.dp else 8.dp),
            shape = RoundedCornerShape(12.dp),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
            )
        ) {
            Column(
                modifier = Modifier.padding(12.dp),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = event.newStatus ?: "Updated",
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 14.sp,
                        color = when (event.newStatus?.lowercase()) {
                            "deployed", "delivered", "confirmed" -> Color(0xFF4CAF50)
                            "pending" -> Color(0xFFFF9800)
                            else -> MaterialTheme.colorScheme.onSurface
                        }
                    )
                    
                    Text(
                        text = formatTimestamp(event.deployedAt),
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                
                if (!event.deployedBy.isNullOrBlank()) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            Icons.Default.Person,
                            contentDescription = null,
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = event.deployedBy,
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
                
                if (!event.deviceId.isNullOrBlank()) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            Icons.Default.PhoneAndroid,
                            contentDescription = null,
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = event.deviceId,
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
                
                if (!event.previousStatus.isNullOrBlank() && event.previousStatus != event.newStatus) {
                    Text(
                        text = "From: ${event.previousStatus}",
                        fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

@Composable
private fun EmptyTimelineState() {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Icon(
            Icons.Default.History,
            contentDescription = null,
            modifier = Modifier.size(48.dp),
            tint = MaterialTheme.colorScheme.outline
        )
        Text(
            text = "No deployment history yet",
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = "History will appear after first deployment",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.outline
        )
    }
}

private fun formatTimestamp(timestamp: String?): String {
    if (timestamp.isNullOrBlank()) return "Unknown"
    // Simple formatting - in production use proper date formatting
    return timestamp.replace("T", " ").substringBefore(".")
}
