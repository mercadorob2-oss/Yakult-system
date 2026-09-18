package com.example.yakultscanner.ui.components;

import androidx.compose.animation.core.Spring;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.vector.ImageVector;
import androidx.compose.ui.text.font.FontWeight;
import androidx.navigation.NavController;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u00002\n\u0000\n\u0002\u0010\"\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\u001a\u0010\u0010\b\u001a\u00020\t2\b\u0010\n\u001a\u0004\u0018\u00010\u0002\u001a\u0012\u0010\u000b\u001a\u00020\f2\b\u0010\n\u001a\u0004\u0018\u00010\u0002H\u0002\u001a\u0010\u0010\r\u001a\u00020\u000e2\u0006\u0010\u000f\u001a\u00020\u0010H\u0007\"\u0014\u0010\u0000\u001a\b\u0012\u0004\u0012\u00020\u00020\u0001X\u0082\u0004\u00a2\u0006\u0002\n\u0000\"\u0017\u0010\u0003\u001a\b\u0012\u0004\u0012\u00020\u00050\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"hiddenBottomNavRoutes", "", "", "scannerBottomNavItems", "", "Lcom/example/yakultscanner/ui/components/NavItem;", "getScannerBottomNavItems", "()Ljava/util/List;", "shouldShowScannerBottomNav", "", "route", "selectedNavIndex", "", "CustomFloatingNavigationBar", "", "navController", "Landroidx/navigation/NavController;", "app_devDebug"})
public final class CustomNavigationBarKt {
    @org.jetbrains.annotations.NotNull()
    private static final java.util.Set<java.lang.String> hiddenBottomNavRoutes = null;
    @org.jetbrains.annotations.NotNull()
    private static final java.util.List<com.example.yakultscanner.ui.components.NavItem> scannerBottomNavItems = null;
    
    @org.jetbrains.annotations.NotNull()
    public static final java.util.List<com.example.yakultscanner.ui.components.NavItem> getScannerBottomNavItems() {
        return null;
    }
    
    public static final boolean shouldShowScannerBottomNav(@org.jetbrains.annotations.Nullable()
    java.lang.String route) {
        return false;
    }
    
    private static final int selectedNavIndex(java.lang.String route) {
        return 0;
    }
    
    @androidx.compose.runtime.Composable()
    public static final void CustomFloatingNavigationBar(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
}