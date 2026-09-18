// Path: app/src/main/java/com/example/yakultscanner/ui/theme/Theme.kt

package com.example.yakultscanner.ui.theme

import android.app.Activity
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat

private val DarkColorScheme = darkColorScheme(
    primary = YakultPrimaryDark,
    onPrimary = YakultOnPrimaryDark,
    primaryContainer = YakultPrimaryContainerDark,
    onPrimaryContainer = YakultOnPrimaryContainerDark,
    secondary = YakultSecondaryDark,
    onSecondary = YakultOnSecondaryDark,
    secondaryContainer = YakultSecondaryContainerDark,
    onSecondaryContainer = YakultOnSecondaryContainerDark,
    tertiary = YakultTertiaryDark,
    onTertiary = YakultOnTertiaryDark,
    tertiaryContainer = YakultTertiaryContainerDark,
    onTertiaryContainer = YakultOnTertiaryContainerDark,
    background = YakultBackgroundDark,
    onBackground = YakultOnBackgroundDark,
    surface = YakultSurfaceDark,
    onSurface = YakultOnSurfaceDark,
    surfaceVariant = YakultSurfaceVariantDark,
    onSurfaceVariant = YakultOnSurfaceVariantDark,
    outline = YakultOutlineDark,
    outlineVariant = YakultOutlineVariantDark,
    inversePrimary = YakultInversePrimaryDark,
    inverseSurface = YakultInverseSurfaceDark,
    inverseOnSurface = YakultInverseOnSurfaceDark,
    scrim = YakultScrimDark
)

private val LightColorScheme = lightColorScheme(
    primary = YakultPrimaryLight,
    onPrimary = YakultOnPrimaryLight,
    primaryContainer = YakultPrimaryContainerLight,
    onPrimaryContainer = YakultOnPrimaryContainerLight,
    secondary = YakultSecondaryLight,
    onSecondary = YakultOnSecondaryLight,
    secondaryContainer = YakultSecondaryContainerLight,
    onSecondaryContainer = YakultOnSecondaryContainerLight,
    tertiary = YakultTertiaryLight,
    onTertiary = YakultOnTertiaryLight,
    tertiaryContainer = YakultTertiaryContainerLight,
    onTertiaryContainer = YakultOnTertiaryContainerLight,
    background = YakultBackgroundLight,
    onBackground = YakultOnBackgroundLight,
    surface = YakultSurfaceLight,
    onSurface = YakultOnSurfaceLight,
    surfaceVariant = YakultSurfaceVariantLight,
    onSurfaceVariant = YakultOnSurfaceVariantLight,
    outline = YakultOutlineLight,
    outlineVariant = YakultOutlineVariantLight,
    inversePrimary = YakultInversePrimaryLight,
    inverseSurface = YakultInverseSurfaceLight,
    inverseOnSurface = YakultInverseOnSurfaceLight,
    scrim = YakultScrimLight
)

@Composable
fun YakultScannerTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    // Dynamic color is available on Android 12+
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }

    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            // Status bar: primary brand color in both modes for strong identity
            window.statusBarColor = colorScheme.primary.toArgb()
            // Navigation bar: match background so it blends into the app
            window.navigationBarColor = colorScheme.background.toArgb()
            val insetsController = WindowCompat.getInsetsController(window, view)
            insetsController.isAppearanceLightStatusBars = !darkTheme
            insetsController.isAppearanceLightNavigationBars = !darkTheme
        }
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
