package com.example.yakultscanner.ui.screens

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

internal enum class BorrowCardTone {
    Neutral,
    Accent,
    Success,
    Warning,
    Error
}

@Composable
internal fun BorrowStateCard(
    title: String,
    message: String,
    tone: BorrowCardTone,
    modifier: Modifier = Modifier,
    badgeText: String? = null,
    actionContent: (@Composable ColumnScope.() -> Unit)? = null
) {
    val colors = borrowToneColors(tone)
    Card(
        modifier = modifier
            .fillMaxWidth()
            .animateContentSize(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = colors.containerColor),
        border = BorderStroke(1.dp, colors.borderColor)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(colors.contentColor.copy(alpha = 0.12f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = borrowToneIcon(tone),
                        contentDescription = null,
                        tint = colors.contentColor
                    )
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(3.dp)
                ) {
                    Text(
                        text = title,
                        fontWeight = FontWeight.Bold,
                        color = colors.contentColor
                    )
                    Text(
                        text = message,
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.contentColor.copy(alpha = 0.86f)
                    )
                }
                if (!badgeText.isNullOrBlank()) {
                    BorrowModeBadge(text = badgeText, tone = tone)
                }
            }
            actionContent?.invoke(this)
        }
    }
}

@Composable
internal fun BorrowSummaryCard(
    title: String,
    subtitle: String,
    lines: List<String>,
    modifier: Modifier = Modifier,
    tone: BorrowCardTone = BorrowCardTone.Accent,
    badgeText: String? = null
) {
    val colors = borrowToneColors(tone)
    Card(
        modifier = modifier
            .fillMaxWidth()
            .animateContentSize(),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = colors.containerColor),
        border = BorderStroke(1.dp, colors.borderColor)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(3.dp)
                ) {
                    Text(
                        text = title,
                        fontWeight = FontWeight.Bold,
                        color = colors.contentColor
                    )
                    Text(
                        text = subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.contentColor.copy(alpha = 0.84f)
                    )
                }
                if (!badgeText.isNullOrBlank()) {
                    BorrowModeBadge(text = badgeText, tone = tone)
                }
            }
            lines.forEach { line ->
                Text(
                    text = line,
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.contentColor.copy(alpha = 0.92f)
                )
            }
        }
    }
}

@Composable
internal fun BorrowModeBadge(
    text: String,
    tone: BorrowCardTone,
    modifier: Modifier = Modifier
) {
    val colors = borrowToneColors(tone)
    Surface(
        modifier = modifier,
        color = colors.badgeContainerColor,
        contentColor = colors.badgeContentColor,
        shape = RoundedCornerShape(999.dp)
    ) {
        Text(
            text = text,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Bold
        )
    }
}

private data class BorrowToneColors(
    val containerColor: Color,
    val contentColor: Color,
    val borderColor: Color,
    val badgeContainerColor: Color,
    val badgeContentColor: Color
)

@Composable
private fun borrowToneColors(tone: BorrowCardTone): BorrowToneColors {
    return when (tone) {
        BorrowCardTone.Neutral -> BorrowToneColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.42f),
            contentColor = MaterialTheme.colorScheme.onSurface,
            borderColor = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.28f),
            badgeContainerColor = MaterialTheme.colorScheme.surface,
            badgeContentColor = MaterialTheme.colorScheme.onSurface
        )
        BorrowCardTone.Accent -> BorrowToneColors(
            containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.74f),
            contentColor = MaterialTheme.colorScheme.onPrimaryContainer,
            borderColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.22f),
            badgeContainerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.95f),
            badgeContentColor = MaterialTheme.colorScheme.onSurface
        )
        BorrowCardTone.Success -> BorrowToneColors(
            containerColor = MaterialTheme.colorScheme.tertiaryContainer.copy(alpha = 0.9f),
            contentColor = MaterialTheme.colorScheme.onTertiaryContainer,
            borderColor = MaterialTheme.colorScheme.tertiary.copy(alpha = 0.2f),
            badgeContainerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
            badgeContentColor = MaterialTheme.colorScheme.onSurface
        )
        BorrowCardTone.Warning -> BorrowToneColors(
            containerColor = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.86f),
            contentColor = MaterialTheme.colorScheme.onSecondaryContainer,
            borderColor = MaterialTheme.colorScheme.secondary.copy(alpha = 0.2f),
            badgeContainerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
            badgeContentColor = MaterialTheme.colorScheme.onSurface
        )
        BorrowCardTone.Error -> BorrowToneColors(
            containerColor = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.88f),
            contentColor = MaterialTheme.colorScheme.onErrorContainer,
            borderColor = MaterialTheme.colorScheme.error.copy(alpha = 0.22f),
            badgeContainerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
            badgeContentColor = MaterialTheme.colorScheme.onSurface
        )
    }
}

private fun borrowToneIcon(tone: BorrowCardTone): ImageVector {
    return when (tone) {
        BorrowCardTone.Success -> Icons.Filled.CheckCircle
        BorrowCardTone.Warning,
        BorrowCardTone.Error -> Icons.Filled.Warning
        BorrowCardTone.Neutral,
        BorrowCardTone.Accent -> Icons.Filled.Info
    }
}
