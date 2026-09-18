package com.example.yakultscanner.ui.adaptive

import androidx.compose.ui.unit.dp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AdaptiveLayoutTest {
    @Test
    fun compactWidthPreservesPhoneLayoutPolicy() {
        val layout = adaptiveLayoutForWidth(599.dp)

        assertEquals(AdaptiveWindowWidth.Compact, layout.width)
        assertEquals(1, layout.gridColumns)
        assertFalse(layout.isTablet)
    }

    @Test
    fun mediumWidthUsesTabletTwoColumnPolicy() {
        val layout = adaptiveLayoutForWidth(600.dp)

        assertEquals(AdaptiveWindowWidth.Medium, layout.width)
        assertEquals(2, layout.gridColumns)
        assertTrue(layout.isTablet)
    }

    @Test
    fun expandedWidthUsesThreeColumnPolicy() {
        val layout = adaptiveLayoutForWidth(840.dp)

        assertEquals(AdaptiveWindowWidth.Expanded, layout.width)
        assertEquals(3, layout.gridColumns)
        assertTrue(layout.isTablet)
    }

    @Test
    fun splitScreenWidthCanReturnToCompactPolicy() {
        val layout = adaptiveLayoutForWidth(480.dp)

        assertEquals(AdaptiveWindowWidth.Compact, layout.width)
        assertEquals(1, layout.gridColumns)
    }

    @Test
    fun transmittalUsesCompactPathBelowTabletThreshold() {
        val layout = adaptiveLayoutForWidth(599.dp)

        assertFalse(layout.isTablet)
        assertEquals(AdaptiveWindowWidth.Compact, layout.width)
        assertEquals(1, layout.gridColumns)
    }

    @Test
    fun transmittalUsesTwoPaneTabletPathAtMediumWidth() {
        val layout = adaptiveLayoutForWidth(600.dp)

        assertTrue(layout.isTablet)
        assertEquals(AdaptiveWindowWidth.Medium, layout.width)
        assertEquals(2, layout.gridColumns)
    }

    @Test
    fun transmittalKeepsTabletPathAtExpandedWidth() {
        val layout = adaptiveLayoutForWidth(840.dp)

        assertTrue(layout.isTablet)
        assertEquals(AdaptiveWindowWidth.Expanded, layout.width)
        assertEquals(3, layout.gridColumns)
    }

    @Test
    fun compactLayoutKeepsPhoneSpacingAndBounds() {
        val layout = adaptiveLayoutForWidth(599.dp)

        assertEquals(20.dp, layout.horizontalPadding)
        assertEquals(600.dp, layout.contentMaxWidth)
        assertEquals(12.dp, layout.sectionSpacing)
        assertEquals(12.dp, layout.cardSpacing)
    }

    @Test
    fun expandedLayoutProvidesTabletContentBounds() {
        val layout = adaptiveLayoutForWidth(1024.dp)

        assertEquals(AdaptiveWindowWidth.Expanded, layout.width)
        assertEquals(32.dp, layout.horizontalPadding)
        assertEquals(1280.dp, layout.contentMaxWidth)
        assertEquals(3, layout.gridColumns)
        assertTrue(layout.isTablet)
    }

}
