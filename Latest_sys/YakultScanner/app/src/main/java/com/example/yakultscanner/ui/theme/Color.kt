// Path: app/src/main/java/com/example/yakultscanner/ui/theme/Color.kt

package com.example.yakultscanner.ui.theme

import androidx.compose.ui.graphics.Color

// Yakult brand-inspired palette

// Light scheme
val YakultPrimaryLight = Color(0xFFE60012)
val YakultOnPrimaryLight = Color(0xFFFFFFFF)
val YakultPrimaryContainerLight = Color(0xFFFFDAD6)
val YakultOnPrimaryContainerLight = Color(0xFF3F0003)

val YakultSecondaryLight = Color(0xFFB31B33)
val YakultOnSecondaryLight = Color(0xFFFFFFFF)
val YakultSecondaryContainerLight = Color(0xFFFFDAD9)
val YakultOnSecondaryContainerLight = Color(0xFF41000A)

val YakultTertiaryLight = Color(0xFFA74900)
val YakultOnTertiaryLight = Color(0xFFFFFFFF)
val YakultTertiaryContainerLight = Color(0xFFFFDAC1)
val YakultOnTertiaryContainerLight = Color(0xFF351000)

val YakultBackgroundLight = Color(0xFFFFF8F3)
val YakultOnBackgroundLight = Color(0xFF211919)
val YakultSurfaceLight = YakultBackgroundLight
val YakultOnSurfaceLight = YakultOnBackgroundLight
val YakultSurfaceVariantLight = Color(0xFFF8E1E0)
val YakultOnSurfaceVariantLight = Color(0xFF5F3F41)

val YakultOutlineLight = Color(0xFF8C5A5D)
val YakultOutlineVariantLight = Color(0xFFE6BDBF)
val YakultInversePrimaryLight = Color(0xFFFFB3AC)
val YakultInverseSurfaceLight = Color(0xFF372222)
val YakultInverseOnSurfaceLight = Color(0xFFFFEDEA)
val YakultScrimLight = Color(0xFF000000)

// Dark scheme — warm charcoal, brand-aligned
// Background system uses warm near-black with subtle red undertone instead of cold blue-grey,
// keeping the Yakult red identity alive even in dark mode.

val YakultPrimaryDark = Color(0xFFFF4560)           // vibrant brand red — pops on dark bg
val YakultOnPrimaryDark = Color(0xFF5C0012)          // deep dark red for text on primary
val YakultPrimaryContainerDark = Color(0xFF7F0D1D)   // rich deep red container
val YakultOnPrimaryContainerDark = Color(0xFFFFDADF) // warm light pink text on container

val YakultSecondaryDark = Color(0xFFFF7D8E)          // soft rose accent
val YakultOnSecondaryDark = Color(0xFF55000F)         // deep dark for text on secondary
val YakultSecondaryContainerDark = Color(0xFF74121F)  // dark rose container
val YakultOnSecondaryContainerDark = Color(0xFFFFCDD3) // light text on secondary container

val YakultTertiaryDark = Color(0xFFFFAF5B)            // warm amber for dates/quantities
val YakultOnTertiaryDark = Color(0xFF472300)           // deep brown text on tertiary
val YakultTertiaryContainerDark = Color(0xFF673600)    // dark amber container
val YakultOnTertiaryContainerDark = Color(0xFFFFDBAF)  // soft light amber text

// Warm charcoal background layers — three distinct elevation stops
val YakultBackgroundDark = Color(0xFF3A2C2F)          // warm dark charcoal (~20% lightness)
val YakultOnBackgroundDark = Color(0xFFF0E2E3)         // warm white text
val YakultSurfaceDark = Color(0xFF453337)              // card elevation — clearly lifted from background
val YakultOnSurfaceDark = Color(0xFFF0E2E3)            // warm white text
val YakultSurfaceVariantDark = Color(0xFF584044)       // chip/input/tag bg — good visible contrast
val YakultOnSurfaceVariantDark = Color(0xFFC9AAAD)    // muted warm rose-grey text on variant

val YakultOutlineDark = Color(0xFF8A6669)             // warm muted reddish-grey outline
val YakultOutlineVariantDark = Color(0xFF684C50)      // subtle warm dividers
val YakultInversePrimaryDark = Color(0xFFB5001A)      // rich red for inverse elements (snackbar CTA)
val YakultInverseSurfaceDark = Color(0xFFEEE0E1)      // warm light surface for snackbars/toasts
val YakultInverseOnSurfaceDark = Color(0xFF201516)    // dark warm text on inverse surface
val YakultScrimDark = Color(0xFF000000)
