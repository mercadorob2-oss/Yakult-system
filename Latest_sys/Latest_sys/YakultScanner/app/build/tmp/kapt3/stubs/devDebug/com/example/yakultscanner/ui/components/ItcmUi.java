package com.example.yakultscanner.ui.components;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.layout.ColumnScope;
import androidx.compose.material3.CardDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;

/**
 * Visual primitives scoped to IT Call Monitoring. They intentionally do not alter the
 * scanner-wide Material theme because the ITCM redesign is limited to ticket workflows.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0014\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b&\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003R\u0013\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0006\u0010\u0007R\u0013\u0010\t\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\n\u0010\u0007R\u0013\u0010\u000b\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\f\u0010\u0007R\u0013\u0010\r\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u000e\u0010\u0007R\u0013\u0010\u000f\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0010\u0010\u0007R\u0013\u0010\u0011\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0012\u0010\u0007R\u0013\u0010\u0013\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0014\u0010\u0007R\u0013\u0010\u0015\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0016\u0010\u0007R\u0013\u0010\u0017\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u0018\u0010\u0007R\u0013\u0010\u0019\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u001a\u0010\u0007R\u0013\u0010\u001b\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u001c\u0010\u0007R\u0013\u0010\u001d\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\u001e\u0010\u0007R\u0013\u0010\u001f\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b \u0010\u0007R\u0013\u0010!\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b\"\u0010\u0007R\u0013\u0010#\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b$\u0010\u0007R\u0013\u0010%\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b&\u0010\u0007R\u0013\u0010\'\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b(\u0010\u0007R\u0013\u0010)\u001a\u00020\u0005\u00a2\u0006\n\n\u0002\u0010\b\u001a\u0004\b*\u0010\u0007\u00a8\u0006+"}, d2 = {"Lcom/example/yakultscanner/ui/components/ItcmUi;", "", "<init>", "()V", "Canvas", "Landroidx/compose/ui/graphics/Color;", "getCanvas-0d7_KjU", "()J", "J", "Surface", "getSurface-0d7_KjU", "SurfaceSubtle", "getSurfaceSubtle-0d7_KjU", "Ink", "getInk-0d7_KjU", "Muted", "getMuted-0d7_KjU", "Divider", "getDivider-0d7_KjU", "Brand", "getBrand-0d7_KjU", "BrandSoft", "getBrandSoft-0d7_KjU", "Critical", "getCritical-0d7_KjU", "High", "getHigh-0d7_KjU", "Medium", "getMedium-0d7_KjU", "Low", "getLow-0d7_KjU", "Pending", "getPending-0d7_KjU", "Active", "getActive-0d7_KjU", "Escalated", "getEscalated-0d7_KjU", "Resolved", "getResolved-0d7_KjU", "Temporary", "getTemporary-0d7_KjU", "Closed", "getClosed-0d7_KjU", "app_devDebug"})
public final class ItcmUi {
    private static final long Canvas = 0L;
    private static final long Surface = 0L;
    private static final long SurfaceSubtle = 0L;
    private static final long Ink = 0L;
    private static final long Muted = 0L;
    private static final long Divider = 0L;
    private static final long Brand = 0L;
    private static final long BrandSoft = 0L;
    private static final long Critical = 0L;
    private static final long High = 0L;
    private static final long Medium = 0L;
    private static final long Low = 0L;
    private static final long Pending = 0L;
    private static final long Active = 0L;
    private static final long Escalated = 0L;
    private static final long Resolved = 0L;
    private static final long Temporary = 0L;
    private static final long Closed = 0L;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.ui.components.ItcmUi INSTANCE = null;
    
    private ItcmUi() {
        super();
    }
}