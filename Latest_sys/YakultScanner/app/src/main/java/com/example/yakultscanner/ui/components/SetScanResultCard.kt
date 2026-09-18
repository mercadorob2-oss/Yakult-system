package com.example.yakultscanner.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Business
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CorporateFare
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PriorityHigh
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Draw
import androidx.compose.material.icons.filled.Timeline
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.yakultscanner.api.DetailedDispatchItemDto
import com.example.yakultscanner.api.SetMetadataDto

// Status badge colors
private val StatusPending = Color(0xFFFF9800)     // Orange
private val StatusReady = Color(0xFF4CAF50)       // Green
private val StatusDeployed = Color(0xFF2196F3)    // Blue
private val StatusIssue = Color(0xFFF44336)       // Red

@Composable
fun SetScanResultCard(
    setCode: String,
    employeeName: String,
    department: String,
    branch: String,
    company: String,
    status: String,
    metadata: SetMetadataDto?,
    items: List<DetailedDispatchItemDto>,
    onDeployClick: () -> Unit,
    onViewHistoryClick: () -> Unit,
    onTrackClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    var itemsExpanded by remember { mutableStateOf(false) }
    
    val statusColor = when (status.lowercase()) {
        "pending" -> StatusPending
        "ready", "prepared" -> StatusReady
        "deployed", "delivered", "confirmed" -> StatusDeployed
        "issue", "problem", "damaged" -> StatusIssue
        else -> MaterialTheme.colorScheme.secondary
    }
    
    val statusText = when (status.lowercase()) {
        "pending" -> "⏳ Pending"
        "ready", "prepared" -> "✅ Ready for Deployment"
        "deployed" -> "🚀 Deployed"
        "delivered" -> "📦 Delivered"
        "confirmed" -> "✓ Confirmed"
        else -> status
    }
    
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Header with Set Code and Status Badge
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column {
                    Text(
                        text = "📦 Set",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = "#$setCode",
                        fontSize = 20.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
                
                Surface(
                    color = statusColor.copy(alpha = 0.1f),
                    shape = RoundedCornerShape(8.dp),
                    border = androidx.compose.foundation.BorderStroke(1.dp, statusColor.copy(alpha = 0.5f))
                ) {
                    Text(
                        text = statusText,
                        color = statusColor,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
                    )
                }
            }
            
            Divider()
            
            // Employee & Location Info
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                InfoRow(Icons.Default.Person, employeeName)
                InfoRow(Icons.Default.Business, "$department | $branch")
                InfoRow(Icons.Default.CorporateFare, company)
            }
            
            // Metadata (Priority, Expected Delivery)
            if (metadata != null) {
                Divider()
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    metadata.priority?.let { priority ->
                        val priorityColor = when (priority.lowercase()) {
                            "urgent" -> Color(0xFFF44336)
                            "high" -> Color(0xFFFF9800)
                            else -> MaterialTheme.colorScheme.primary
                        }
                        MetadataChip(
                            icon = Icons.Default.PriorityHigh,
                            label = "Priority",
                            value = priority,
                            color = priorityColor
                        )
                    }
                    
                    metadata.expectedDeliveryUtc?.let { delivery ->
                        MetadataChip(
                            icon = Icons.Default.Schedule,
                            label = "Expected",
                            value = delivery,
                            color = MaterialTheme.colorScheme.secondary
                        )
                    }
                    
                    MetadataChip(
                        icon = Icons.Default.Inventory2,
                        label = "Items",
                        value = "${metadata.itemCount}",
                        color = MaterialTheme.colorScheme.tertiary
                    )
                }
            }
            
            Divider()
            
            // Expandable Items Section
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { itemsExpanded = !itemsExpanded }
                    .padding(vertical = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "📋 Items (${items.size})",
                    fontWeight = FontWeight.Medium
                )
                Icon(
                    imageVector = if (itemsExpanded) Icons.Default.KeyboardArrowUp else Icons.Default.KeyboardArrowDown,
                    contentDescription = "Toggle items"
                )
            }
            
            AnimatedVisibility(
                visible = itemsExpanded,
                enter = expandVertically() + fadeIn(),
                exit = shrinkVertically() + fadeOut()
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items.take(5).forEach { item ->
                        CompactItemRow(item = item)
                    }
                    if (items.size > 5) {
                        TextButton(
                            onClick = { /* Navigate to full list */ },
                            modifier = Modifier.align(Alignment.CenterHorizontally)
                        ) {
                            Text("View all ${items.size} items →")
                        }
                    }
                }
            }
            
            Divider()
            
            // Action Buttons
            if (status.lowercase() in listOf("pending", "ready", "prepared")) {
                Button(
                    onClick = onDeployClick,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFF4CAF50)
                    )
                ) {
                    Icon(Icons.Default.Draw, contentDescription = null)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Deploy with Signature", fontWeight = FontWeight.Bold)
                }
            }
            
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(
                    onClick = onViewHistoryClick,
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.Timeline, contentDescription = null)
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("History")
                }
                
                OutlinedButton(
                    onClick = onTrackClick,
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.LocationOn, contentDescription = null)
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("Track")
                }
            }
        }
    }
}

@Composable
private fun InfoRow(icon: androidx.compose.ui.graphics.vector.ImageVector, text: String) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = text,
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
private fun MetadataChip(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    value: String,
    color: Color
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.padding(horizontal = 8.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = label,
            tint = color,
            modifier = Modifier.size(20.dp)
        )
        Text(
            text = value,
            fontSize = 12.sp,
            fontWeight = FontWeight.SemiBold,
            color = color
        )
        Text(
            text = label,
            fontSize = 10.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun CompactItemRow(item: DetailedDispatchItemDto) {
    var showDetails by remember { mutableStateOf(false) }
    
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { showDetails = !showDetails },
        shape = RoundedCornerShape(8.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
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
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = item.itemType ?: "Unknown Item",
                        fontWeight = FontWeight.Medium,
                        fontSize = 14.sp
                    )
                    Text(
                        text = "SN: ${item.serialNumber ?: "N/A"}",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                
                // Repair indicator
                if (item.repairCount > 0) {
                    Surface(
                        color = Color(0xFFFF9800).copy(alpha = 0.1f),
                        shape = RoundedCornerShape(4.dp)
                    ) {
                        Text(
                            text = "🔧 ${item.repairCount}",
                            fontSize = 11.sp,
                            color = Color(0xFFFF9800),
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    }
                }
                
                ItemStatusChip(status = item.itemStatus ?: "Unknown")
            }
            
            AnimatedVisibility(visible = showDetails) {
                Column(
                    modifier = Modifier.padding(top = 8.dp),
                    verticalArrangement = Arrangement.spacedBy(2.dp)
                ) {
                    DetailText("Model:", item.modelNumber)
                    DetailText("Category:", item.itemCategory)
                    DetailText("Condition:", item.itemCondition)
                    if (!item.lastRepairAction.isNullOrBlank()) {
                        DetailText("Last Repair:", item.lastRepairAction)
                    }
                }
            }
        }
    }
}

@Composable
private fun DetailText(label: String, value: String?) {
    if (!value.isNullOrBlank() && value != "N/A") {
        Text(
            text = "$label $value",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}
