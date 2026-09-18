package com.example.yakultscanner.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp

object ScannerWorkspaceUi {
    val Canvas = Color(0xFFFFF8F5)
    val Surface = Color(0xFFFFFEFC)
    val SurfaceSubtle = Color(0xFFF1E9ED)
    val Ink = Color(0xFF3B2525)
    val Muted = Color(0xFF7A6268)
    val Divider = Color(0xFFE7D9DD)
    val Brand = Color(0xFFE60012)
    val BrandDark = Color(0xFFB50012)
    val BrandSoft = Color(0xFFFFE6E8)
    val Success = Color(0xFF23824F)
    val Warning = Color(0xFF9A6700)
    val Error = Color(0xFFB42318)
    val Info = Color(0xFFA36A72)
}

@Composable
fun ScannerSurfaceCard(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = ScannerWorkspaceUi.Surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, ScannerWorkspaceUi.Divider)
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp), content = content)
    }
}

@Composable
fun ScannerWorkspaceHeader(title: String, subtitle: String, modifier: Modifier = Modifier, trailing: (@Composable () -> Unit)? = null) {
    Surface(modifier = modifier.fillMaxWidth(), color = ScannerWorkspaceUi.Ink, shape = RoundedCornerShape(24.dp)) {
        Row(modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                Text(title, style = MaterialTheme.typography.titleLarge, color = Color.White, fontWeight = FontWeight.Bold)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = Color.White.copy(alpha = 0.72f), maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            trailing?.invoke()
        }
    }
}

@Composable
fun ScannerSectionHeader(title: String, subtitle: String? = null, modifier: Modifier = Modifier) {
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(3.dp)) {
        Text(title, style = MaterialTheme.typography.titleSmall, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold)
        subtitle?.takeIf { it.isNotBlank() }?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted) }
    }
}

@Composable
fun ScannerModeSegment(firstLabel: String, secondLabel: String, firstSelected: Boolean, onFirstSelected: () -> Unit, onSecondSelected: () -> Unit, modifier: Modifier = Modifier) {
    Row(modifier = modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(ScannerWorkspaceUi.SurfaceSubtle).padding(4.dp), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        ScannerModeChoice(firstLabel, firstSelected, onFirstSelected, Modifier.weight(1f))
        ScannerModeChoice(secondLabel, !firstSelected, onSecondSelected, Modifier.weight(1f))
    }
}

@Composable
private fun ScannerModeChoice(label: String, selected: Boolean, onClick: () -> Unit, modifier: Modifier) {
    Box(modifier = modifier.clip(RoundedCornerShape(12.dp)).background(if (selected) ScannerWorkspaceUi.Surface else ScannerWorkspaceUi.SurfaceSubtle).clickable(onClick = onClick).padding(vertical = 11.dp), contentAlignment = Alignment.Center) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = if (selected) ScannerWorkspaceUi.Ink else ScannerWorkspaceUi.Muted, fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium)
    }
}

@Composable
fun ScannerMetricChip(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(modifier = modifier, shape = RoundedCornerShape(14.dp), color = ScannerWorkspaceUi.SurfaceSubtle) {
        Column(modifier = Modifier.padding(horizontal = 11.dp, vertical = 9.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(value, style = MaterialTheme.typography.titleMedium, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold)
            Text(label, style = MaterialTheme.typography.labelSmall, color = ScannerWorkspaceUi.Muted)
        }
    }
}

@Composable
fun ScannerStatusChip(status: String, modifier: Modifier = Modifier) {
    val normalized = status.lowercase()
    val color = when {
        normalized.contains("sent") -> ScannerWorkspaceUi.Success
        normalized.contains("partial") || normalized.contains("pending") -> ScannerWorkspaceUi.Warning
        normalized.contains("fail") -> ScannerWorkspaceUi.Error
        else -> ScannerWorkspaceUi.Muted
    }
    Surface(modifier = modifier, shape = RoundedCornerShape(999.dp), color = color.copy(alpha = 0.12f)) {
        Text(status, modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp), style = MaterialTheme.typography.labelSmall, color = color, fontWeight = FontWeight.Bold)
    }
}

@Composable
fun ScannerActionBar(primaryLabel: String, onPrimary: () -> Unit, secondaryLabel: String? = null, onSecondary: (() -> Unit)? = null, primaryEnabled: Boolean = true, modifier: Modifier = Modifier) {
    Surface(modifier = modifier.fillMaxWidth(), color = ScannerWorkspaceUi.Surface, shadowElevation = 8.dp) {
        Row(modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
            if (secondaryLabel != null && onSecondary != null) OutlinedButton(onClick = onSecondary, modifier = Modifier.height(50.dp), shape = RoundedCornerShape(16.dp)) { Text(secondaryLabel) }
            Button(onClick = onPrimary, enabled = primaryEnabled, modifier = Modifier.weight(1f).height(50.dp), shape = RoundedCornerShape(16.dp)) { Text(primaryLabel, fontWeight = FontWeight.Bold) }
        }
    }
}

@Composable
fun ScannerEmptyState(title: String, message: String, modifier: Modifier = Modifier) {
    ScannerSurfaceCard(modifier = modifier) {
        Text(title, style = MaterialTheme.typography.titleSmall, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold)
        Text(message, style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
    }
}

@Composable
fun ScannerMonospaceValue(value: String, modifier: Modifier = Modifier) {
    Text(value, modifier = modifier, style = MaterialTheme.typography.bodyMedium, color = ScannerWorkspaceUi.Ink, fontFamily = androidx.compose.ui.text.font.FontFamily.Monospace, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
}

@Composable
fun ScannerSpacer(height: Int = 12) {
    Spacer(Modifier.height(height.dp))
}