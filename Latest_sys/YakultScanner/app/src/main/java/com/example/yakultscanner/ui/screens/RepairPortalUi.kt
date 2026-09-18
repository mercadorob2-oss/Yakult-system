package com.example.yakultscanner.ui.screens

import android.app.Activity
import android.view.View
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp

/** Compact, enterprise visual language used only by the Repair Portal mobile surfaces. */
object RepairUi {
    val Canvas = Color(0xFFF4F6F8)
    val Surface = Color(0xFFFFFFFF)
    val SurfaceSubtle = Color(0xFFEFF2F5)
    val Ink = Color(0xFF16222B)
    val Muted = Color(0xFF6B7A85)
    val Divider = Color(0xFFE6ECF0)
    val Brand = Color(0xFF1F4A61)
    val BrandSoft = Color(0xFFE6EFF3)
    val Active = Color(0xFF1E6E97)
    val ActiveSoft = Color(0xFFE0EEF6)
    val Warning = Color(0xFF8A5A14)
    val WarningSoft = Color(0xFFFAEEDB)
    val Critical = Color(0xFFB42318)
    val CriticalSoft = Color(0xFFFCE9E7)
    val Complete = Color(0xFF1F7A4D)
    val CompleteSoft = Color(0xFFE1F1E8)
    val Caution = Color(0xFF946A08)
    val CautionSoft = Color(0xFFFBF0CE)
    val Shadow = Color(0x141B2733)

    /** Consistent 4dp-based spacing scale for a symmetrical, modern rhythm. */
    val SpaceXs = 4.dp
    val SpaceSm = 8.dp
    val Space = 12.dp
    val SpaceLg = 16.dp
    val SpaceXl = 24.dp

    /** Rounded-corner scale — larger radii read as a more contemporary mobile app. */
    val RadiusSm = 12.dp
    val Radius = 18.dp
    val RadiusPill = 999.dp
}

/** Keeps the repair workspace visually distinct from the app-wide red chrome and restores it on exit. */
@Composable
fun RepairSystemBars() {
    val view = LocalView.current
    val window = (view.context as? Activity)?.window
    DisposableEffect(window, view) {
        val oldStatus = window?.statusBarColor
        val oldNavigation = window?.navigationBarColor
        val oldFlags = view.systemUiVisibility
        onDispose {
            if (window != null) {
                oldStatus?.let { window.statusBarColor = it }
                oldNavigation?.let { window.navigationBarColor = it }
                view.systemUiVisibility = oldFlags
            }
        }
    }
    SideEffect {
        if (window != null) {
            window.statusBarColor = RepairUi.Surface.toArgb()
            window.navigationBarColor = RepairUi.Surface.toArgb()
            view.systemUiVisibility = view.systemUiVisibility or View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR or View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR
        }
    }
}
@Composable
fun RepairSurfaceCard(
    modifier: Modifier = Modifier.fillMaxWidth(),
    tonalColor: Color = RepairUi.Surface,
    content: @Composable ColumnScope.() -> Unit
) {
    Box(
        modifier = modifier
            .shadow(
                elevation = 10.dp,
                shape = RoundedCornerShape(RepairUi.Radius),
                ambientColor = RepairUi.Shadow,
                spotColor = RepairUi.Shadow
            )
            .clip(RoundedCornerShape(RepairUi.Radius))
            .background(tonalColor)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            content = content
        )
    }
}

@Composable
fun RepairPrimaryButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    content: @Composable () -> Unit
) {
    Button(
        onClick = onClick,
        modifier = modifier.heightIn(min = 50.dp),
        enabled = enabled,
        shape = RoundedCornerShape(14.dp),
        contentPadding = PaddingValues(horizontal = 20.dp, vertical = 12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = RepairUi.Brand,
            contentColor = RepairUi.Surface,
            disabledContainerColor = RepairUi.SurfaceSubtle,
            disabledContentColor = RepairUi.Muted
        ),
        content = { content() }
    )
}

@Composable
fun RepairSecondaryButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    contentPadding: PaddingValues = PaddingValues(horizontal = 24.dp, vertical = 8.dp),
    content: @Composable () -> Unit
) {
    OutlinedButton(
        onClick = onClick,
        modifier = modifier.heightIn(min = 46.dp),
        enabled = enabled,
        shape = RoundedCornerShape(14.dp),
        border = BorderStroke(1.dp, if (enabled) RepairUi.Divider else RepairUi.SurfaceSubtle),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = RepairUi.Brand, disabledContentColor = RepairUi.Muted),
        contentPadding = contentPadding,
        content = { content() }
    )
}

@Composable
fun repairTextFieldColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = RepairUi.Brand,
    unfocusedBorderColor = RepairUi.Divider,
    disabledBorderColor = RepairUi.Divider,
    focusedLabelColor = RepairUi.Brand,
    unfocusedLabelColor = RepairUi.Muted,
    cursorColor = RepairUi.Brand,
    focusedTextColor = RepairUi.Ink,
    unfocusedTextColor = RepairUi.Ink
)

@Composable
fun RepairSectionHeading(title: String, subtitle: String? = null) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(title, style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
        subtitle?.takeIf { it.isNotBlank() }?.let {
            Text(it, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
        }
    }
}

@Composable
fun RepairStatusPill(status: String?) {
    val normalized = status.orEmpty().replace(" ", "").lowercase()
    val (background, foreground) = when {
        normalized in setOf("completed", "resolved", "returned") -> RepairUi.CompleteSoft to RepairUi.Complete
        normalized in setOf("unrepairable", "discarded") -> RepairUi.CriticalSoft to RepairUi.Critical
        normalized.contains("repair") || normalized.contains("diagnos") || normalized.contains("test") -> RepairUi.ActiveSoft to RepairUi.Active
        normalized.contains("await") || normalized.contains("waitingparts") -> RepairUi.WarningSoft to RepairUi.Warning
        normalized == "waiting" -> RepairUi.CompleteSoft to RepairUi.Complete
        else -> RepairUi.BrandSoft to RepairUi.Brand
    }
    RepairPill(text = formatRepairStatus(status), background = background, foreground = foreground)
}

@Composable
fun RepairPriorityPill(priority: String?, compact: Boolean = false) {
    val normalized = priority.orEmpty().lowercase()
    val (background, foreground) = when (normalized) {
        "critical" -> RepairUi.CriticalSoft to RepairUi.Critical
        "high" -> RepairUi.WarningSoft to RepairUi.Warning
        "low" -> RepairUi.SurfaceSubtle to RepairUi.Muted
        else -> RepairUi.CautionSoft to RepairUi.Caution
    }
    val level = priority?.takeIf { it.isNotBlank() } ?: "Medium"
    RepairPill(
        text = if (compact) level else "$level priority",
        background = background,
        foreground = foreground
    )
}

@Composable
private fun RepairPill(text: String, background: Color, foreground: Color) {
    Text(
        text = text,
        modifier = Modifier
            .clip(RoundedCornerShape(8.dp))
            .background(background)
            .padding(horizontal = 10.dp, vertical = 5.dp),
        style = MaterialTheme.typography.labelSmall,
        color = foreground,
        fontWeight = FontWeight.SemiBold,
        maxLines = 1
    )
}

@Composable
fun RepairMetaText(text: String, modifier: Modifier = Modifier) {
    Text(text, modifier = modifier, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
}

@Composable
fun RepairInfoRow(label: String, value: String?) {
    value?.takeIf { it.isNotBlank() }?.let {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
            Text(label, modifier = Modifier.weight(.36f), style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            Text(it, modifier = Modifier.weight(.64f), style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
        }
    }
}

@Composable
fun RepairActionRow(
    title: String,
    description: String,
    icon: ImageVector,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(RepairUi.SurfaceSubtle)
            .clickable(enabled = enabled, onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.size(40.dp).clip(RoundedCornerShape(12.dp)).background(if (enabled) RepairUi.BrandSoft else RepairUi.Surface),
            contentAlignment = Alignment.Center
        ) {
            Icon(icon, contentDescription = null, modifier = Modifier.size(20.dp), tint = if (enabled) RepairUi.Brand else RepairUi.Muted)
        }
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(1.dp)) {
            Text(title, style = MaterialTheme.typography.bodyMedium, color = if (enabled) RepairUi.Ink else RepairUi.Muted, fontWeight = FontWeight.Bold)
            Text(description, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
        Text("›", style = MaterialTheme.typography.titleLarge, color = RepairUi.Muted)
    }
}

private fun formatRepairStatus(status: String?): String {
    val value = status?.trim().orEmpty()
    return when (value) {
        "AwaitingParts" -> "Awaiting parts"
        "WaitingDiagnosis" -> "Waiting diagnosis"
        else -> value.ifBlank { "Waiting" }
    }
}
