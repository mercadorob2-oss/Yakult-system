package com.example.yakultscanner.ui.adaptive

import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/** Width buckets used by the app's responsive layouts. */
enum class AdaptiveWindowWidth {
    Compact,
    Medium,
    Expanded
}

/**
 * Shared responsive values. Compact values intentionally match the existing phone layout as
 * closely as possible; larger values provide room for tablet and split-screen reflow.
 */
data class AdaptiveLayout(
    val width: AdaptiveWindowWidth,
    val horizontalPadding: Dp,
    val contentMaxWidth: Dp,
    val sectionSpacing: Dp,
    val cardSpacing: Dp,
    val gridMinItemWidth: Dp,
    val gridColumns: Int
) {
    val isTablet: Boolean get() = width != AdaptiveWindowWidth.Compact
}

private val CompactLayout = AdaptiveLayout(
    width = AdaptiveWindowWidth.Compact,
    horizontalPadding = 20.dp,
    contentMaxWidth = 600.dp,
    sectionSpacing = 12.dp,
    cardSpacing = 12.dp,
    gridMinItemWidth = 280.dp,
    gridColumns = 1
)

private val MediumLayout = AdaptiveLayout(
    width = AdaptiveWindowWidth.Medium,
    horizontalPadding = 24.dp,
    contentMaxWidth = 960.dp,
    sectionSpacing = 16.dp,
    cardSpacing = 16.dp,
    gridMinItemWidth = 320.dp,
    gridColumns = 2
)

private val ExpandedLayout = AdaptiveLayout(
    width = AdaptiveWindowWidth.Expanded,
    horizontalPadding = 32.dp,
    contentMaxWidth = 1280.dp,
    sectionSpacing = 20.dp,
    cardSpacing = 20.dp,
    gridMinItemWidth = 360.dp,
    gridColumns = 3
)

/**
 * Maps the current window width to the Material-style compact/medium/expanded buckets.
 * Keeping this as a pure function makes the responsive policy easy to unit test.
 */
fun adaptiveLayoutForWidth(width: Dp): AdaptiveLayout = when {
    width < 600.dp -> CompactLayout
    width < 840.dp -> MediumLayout
    else -> ExpandedLayout
}

val LocalAdaptiveLayout = staticCompositionLocalOf { CompactLayout }

/**
 * Observes the actual Compose window constraints. This responds to tablets, rotation, and
 * split-screen resizing without relying on the physical device model.
 */
@Composable
fun AdaptiveLayoutProvider(content: @Composable () -> Unit) {
    BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        CompositionLocalProvider(
            LocalAdaptiveLayout provides adaptiveLayoutForWidth(maxWidth),
            content = content
        )
    }
}
