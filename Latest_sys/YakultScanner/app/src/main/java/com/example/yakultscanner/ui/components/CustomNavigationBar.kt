package com.example.yakultscanner.ui.components

import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PlaylistAdd
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.outlined.Dashboard
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.PlaylistAdd
import androidx.compose.material.icons.outlined.QrCodeScanner
import androidx.compose.material.icons.outlined.Assessment
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationRail
import androidx.compose.material3.NavigationRailItem
import androidx.compose.material3.NavigationRailItemDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.asAndroidPath
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.navigation.NavController
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.currentBackStackEntryAsState

data class NavItem(
    val route: String,
    val title: String,
    val selectedIcon: ImageVector,
    val unselectedIcon: ImageVector,
    val relatedRoutes: Set<String>
)

private val hiddenBottomNavRoutes = setOf(
    "login",
    "register",
    "scanner_device",
    "scanner_camera")

val scannerBottomNavItems = listOf(
    NavItem(
        route = "root_home",
        title = "Home",
        selectedIcon = Icons.Filled.Dashboard,
        unselectedIcon = Icons.Outlined.Dashboard,
        relatedRoutes = setOf(
            "root_home",
            "borrow_home",
            "borrow_items",
            "borrow_records",
            "borrow_add_item",
            "reports",
            "pending_updates",
            "processed_updates",
            "call_monitoring",
            "call_ticket_detail/{ticketId}",
            "call_ticket_create"
        )
    ),
    NavItem(
        route = "serial_scan_home",
        title = "Scanner",
        selectedIcon = Icons.Filled.QrCodeScanner,
        unselectedIcon = Icons.Outlined.QrCodeScanner,
        relatedRoutes = setOf(
            "serial_scan_home",
            "home",
            "batch_serial_entry",
            "transmittal_scan",
            "gatepass_scan",
            "details/{scannedString}?token={token}",
            "details_readonly/{scannedString}",
            "dispatch_item_details/{serialNumber}?setCode={setCode}&dispatchStatus={dispatchStatus}&employee={employee}"
        )
    ),
    NavItem(
        route = "direct_serial_scan",
        title = "Quick Scan",
        selectedIcon = Icons.Filled.PlaylistAdd,
        unselectedIcon = Icons.Outlined.PlaylistAdd,
        relatedRoutes = setOf("direct_serial_scan", "direct_scan_details/{scannedString}")
    ),
    NavItem(
        route = "reports",
        title = "Reports",
        selectedIcon = Icons.Filled.Assessment,
        unselectedIcon = Icons.Outlined.Assessment,
        relatedRoutes = setOf("reports", "reports_module/{moduleId}?range={range}")
    ),
    NavItem(
        route = "history",
        title = "History",
        selectedIcon = Icons.Filled.History,
        unselectedIcon = Icons.Outlined.History,
        relatedRoutes = setOf("history", "report_issues", "uploaded/{setCode}")
    ),
    NavItem(
        route = "saved_transmittal_files",
        title = "Files",
        selectedIcon = Icons.Filled.Folder,
        unselectedIcon = Icons.Outlined.Folder,
        relatedRoutes = setOf("saved_transmittal_files")
    ),
    NavItem(
        route = "app_info",
        title = "Settings",
        selectedIcon = Icons.Filled.Info,
        unselectedIcon = Icons.Outlined.Info,
        relatedRoutes = setOf("app_info", "help")
    )
)

fun shouldShowScannerBottomNav(route: String?): Boolean {
    return route != null && route !in hiddenBottomNavRoutes
}

private fun selectedNavIndex(route: String?): Int {
    val matchedIndex = scannerBottomNavItems.indexOfFirst { route != null && route in it.relatedRoutes }
    return if (matchedIndex >= 0) matchedIndex else 0
}

@Composable
fun CustomFloatingNavigationBar(navController: NavController) {
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route
    if (!shouldShowScannerBottomNav(currentRoute)) return

    val selectedIndex = remember(currentRoute) {
        selectedNavIndex(currentRoute)
    }

    val animatedSelectedIndex by animateFloatAsState(
        targetValue = selectedIndex.toFloat(),
        animationSpec = spring(dampingRatio = 0.5f, stiffness = Spring.StiffnessLow),
        label = "selectedTabIndex"
    )

    val surfaceColor = ScannerWorkspaceUi.Surface
    val onSurfaceColor = ScannerWorkspaceUi.Ink
    val onSurfaceVariantColor = ScannerWorkspaceUi.Muted
    val primaryColor = ScannerWorkspaceUi.Brand

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(74.dp)
            .background(androidx.compose.ui.graphics.Color.Transparent)
    ) {
        Box(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(58.dp)
                .drawBehind {
                    val width = size.width
                    val height = size.height
                    val itemWidth = width / scannerBottomNavItems.size
                    val centerX = (animatedSelectedIndex * itemWidth) + (itemWidth / 2f)

                    val curveWidth = 78.dp.toPx()
                    val curveHeight = 28.dp.toPx()

                    val path = Path().apply {
                        moveTo(0f, 0f)
                        val startX = centerX - curveWidth / 2
                        val endX = centerX + curveWidth / 2
                        lineTo(startX, 0f)
                        cubicTo(startX + curveWidth * 0.15f, 0f, startX + curveWidth * 0.20f, curveHeight, centerX, curveHeight)
                        cubicTo(startX + curveWidth * 0.80f, curveHeight, startX + curveWidth * 0.85f, 0f, endX, 0f)
                        lineTo(width, 0f)
                        lineTo(width, height)
                        lineTo(0f, height)
                        close()
                    }

                    drawContext.canvas.nativeCanvas.apply {
                        val shadowColor = android.graphics.Color.argb(30, 0, 0, 0)
                        val paint = android.graphics.Paint().apply {
                            color = surfaceColor.toArgb()
                            setShadowLayer(12f, 0f, -4f, shadowColor)
                        }
                        drawPath(path.asAndroidPath(), paint)
                    }

                    drawPath(path, surfaceColor)
                }
        )

        BoxWithConstraints(modifier = Modifier.fillMaxWidth().height(74.dp)) {
            val totalWidth = maxWidth
            val widthPerItem = totalWidth / scannerBottomNavItems.size

            Box(
                modifier = Modifier
                    .offset(x = widthPerItem * animatedSelectedIndex)
                    .width(widthPerItem)
                    .height(50.dp),
                contentAlignment = Alignment.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(46.dp)
                        .clip(CircleShape)
                        .background(surfaceColor)
                        .padding(3.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .clip(CircleShape)
                            .background(primaryColor),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = scannerBottomNavItems[selectedIndex].selectedIcon,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onPrimary,
                            modifier = Modifier.size(26.dp)
                        )
                    }
                }
            }
        }

        Row(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(58.dp),
            horizontalArrangement = Arrangement.SpaceAround,
            verticalAlignment = Alignment.CenterVertically
        ) {
            scannerBottomNavItems.forEachIndexed { index, item ->
                val isSelected = selectedIndex == index

                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxHeight()
                        .clickable(
                            interactionSource = remember { MutableInteractionSource() },
                            indication = null
                        ) {
                            if (currentRoute != item.route) {
                                navController.navigate(item.route) {
                                    popUpTo(navController.graph.findStartDestination().id) {
                                        saveState = true
                                    }
                                    launchSingleTop = true
                                    restoreState = true
                                }
                            }
                        },
                    contentAlignment = Alignment.Center
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
                        modifier = Modifier.padding(top = 7.dp)
                    ) {
                        Icon(
                            imageVector = if (isSelected) item.selectedIcon else item.unselectedIcon,
                            contentDescription = item.title,
                            tint = if (isSelected) androidx.compose.ui.graphics.Color.Transparent else onSurfaceVariantColor,
                            modifier = Modifier.size(22.dp)
                        )

                        Spacer(modifier = Modifier.height(2.dp))

                        Text(
                            text = item.title,
                            fontSize = 11.sp,
                            fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal,
                            color = if (isSelected) onSurfaceColor else onSurfaceVariantColor
                        )
                    }
                }
            }
        }
    }
}



/**
 * Tablet navigation keeps the same destinations as the phone bottom bar, but uses the available
 * vertical space instead of stretching seven destinations across the bottom of a wide screen.
 */
@Composable
fun CustomNavigationRail(
    navController: NavController,
    modifier: Modifier = Modifier
) {
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route
    if (!shouldShowScannerBottomNav(currentRoute)) return

    val selectedIndex = selectedNavIndex(currentRoute)
    val surfaceColor = ScannerWorkspaceUi.Surface
    val onSurfaceColor = ScannerWorkspaceUi.Ink
    val onSurfaceVariantColor = ScannerWorkspaceUi.Muted
    val primaryColor = ScannerWorkspaceUi.Brand

    androidx.compose.material3.NavigationRail(
        modifier = modifier
            .widthIn(min = 112.dp, max = 128.dp)
            .fillMaxHeight(),
        containerColor = surfaceColor,
        header = { Spacer(modifier = Modifier.height(8.dp)) }
    ) {
        scannerBottomNavItems.forEachIndexed { index, item ->
            androidx.compose.material3.NavigationRailItem(
                selected = selectedIndex == index,
                onClick = {
                    if (currentRoute != item.route) {
                        navController.navigate(item.route) {
                            popUpTo(navController.graph.findStartDestination().id) {
                                saveState = true
                            }
                            launchSingleTop = true
                            restoreState = true
                        }
                    }
                },
                icon = {
                    Icon(
                        imageVector = if (selectedIndex == index) item.selectedIcon else item.unselectedIcon,
                        contentDescription = item.title
                    )
                },
                label = {
                    Text(
                        text = item.title,
                        fontSize = 11.sp,
                        fontWeight = if (selectedIndex == index) FontWeight.Bold else FontWeight.Normal,
                        maxLines = 1
                    )
                },
                alwaysShowLabel = true,
                colors = androidx.compose.material3.NavigationRailItemDefaults.colors(
                    selectedIconColor = MaterialTheme.colorScheme.onPrimary,
                    indicatorColor = primaryColor,
                    unselectedIconColor = onSurfaceVariantColor,
                    selectedTextColor = onSurfaceColor,
                    unselectedTextColor = onSurfaceVariantColor
                )
            )
        }
    }
}
