package com.example.yakultscanner;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.layout.ContentScale;
import androidx.compose.ui.text.input.ImeAction;
import androidx.compose.ui.text.input.KeyboardType;
import androidx.compose.foundation.text.KeyboardOptions;
import androidx.compose.ui.text.input.PasswordVisualTransformation;
import androidx.compose.ui.text.input.TextFieldValue;
import androidx.compose.ui.text.input.VisualTransformation;
import androidx.compose.ui.text.style.TextAlign;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.LoginRequest;
import com.example.yakultscanner.BuildConfig;
import com.example.yakultscanner.settings.ApiSettings;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.OutlinedTextFieldDefaults;
import androidx.compose.ui.text.font.FontWeight;
import java.net.URI;
import com.google.gson.Gson;
import android.util.Log;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u000e\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u00a8\u0006\u0004"}, d2 = {"LoginScreen", "", "navController", "Landroidx/navigation/NavController;", "app_devDebug"})
public final class LoginScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void LoginScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
}