package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ButtonDefaults;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.lifecycle.ViewModel;
import androidx.navigation.NavController;
import com.example.yakultscanner.PinnedSetsStore;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.ScanHistoryEntry;
import com.example.yakultscanner.settings.ApiSettings;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.network.ConnectivityMonitor;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000H\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0007\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a>\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\b2\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\r2\u0006\u0010\u000e\u001a\u00020\u000f2\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00010\u0011H\u0003\u001aI\u0010\u0012\u001a\u00020\u00012\b\b\u0002\u0010\u0013\u001a\u00020\u00142\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\u0015\u001a\u00020\b2\u0006\u0010\n\u001a\u00020\u000b2\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u00172\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00010\u0011H\u0003\u00a2\u0006\u0002\u0010\u0018\u00a8\u0006\u0019"}, d2 = {"HomeScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/ui/screens/HomeViewModel;", "PrimaryScanCard", "title", "", "description", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "scale", "", "interactionSource", "Landroidx/compose/foundation/interaction/MutableInteractionSource;", "onClick", "Lkotlin/Function0;", "CompactActionButton", "modifier", "Landroidx/compose/ui/Modifier;", "subtitle", "badgeCount", "", "(Landroidx/compose/ui/Modifier;Ljava/lang/String;Ljava/lang/String;Landroidx/compose/ui/graphics/vector/ImageVector;Ljava/lang/Integer;Lkotlin/jvm/functions/Function0;)V", "app_devDebug"})
public final class HomeScreenKt {
    
    @androidx.compose.material3.ExperimentalMaterial3Api()
    @androidx.compose.runtime.Composable()
    public static final void HomeScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.ui.screens.HomeViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void PrimaryScanCard(java.lang.String title, java.lang.String description, androidx.compose.ui.graphics.vector.ImageVector icon, float scale, androidx.compose.foundation.interaction.MutableInteractionSource interactionSource, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void CompactActionButton(androidx.compose.ui.Modifier modifier, java.lang.String title, java.lang.String subtitle, androidx.compose.ui.graphics.vector.ImageVector icon, java.lang.Integer badgeCount, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
}