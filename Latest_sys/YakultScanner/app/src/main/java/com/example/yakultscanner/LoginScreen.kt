package com.example.yakultscanner

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.LoginRequest
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.BuildConfig
import com.example.yakultscanner.settings.ApiSettings
import com.example.yakultscanner.settings.ApiEnvironment
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Lock
import androidx.compose.foundation.layout.size
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.OutlinedTextFieldDefaults
import kotlinx.coroutines.launch

import androidx.compose.ui.text.font.FontWeight
import java.net.URI
import com.google.gson.Gson
import android.util.Log

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoginScreen(navController: NavController) {
    var username by remember { mutableStateOf(TextFieldValue("")) }
    var password by remember { mutableStateOf(TextFieldValue("")) }
    var isLoading by remember { mutableStateOf(false) }
    var isPasswordVisible by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val focusManager = LocalFocusManager.current
    val snackbarHostState = remember { SnackbarHostState() }
    var showSettingsDialog by remember { mutableStateOf(false) }
    var tempHost by remember { mutableStateOf("") }
    var tempPort by remember { mutableStateOf("") }
    var tempEnvironment by remember { mutableStateOf(ApiSettings.selectedEnvironment) }
    var detectedDbName by remember { mutableStateOf<String?>(null) }
    var tempOfflineMode by remember { mutableStateOf(false) }
    var connectionTestMsg by remember { mutableStateOf<String?>(null) }
    var isTestingConnection by remember { mutableStateOf(false) }
    var isVisible by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        isVisible = true
    }

    // Tablet (width >= 600dp) gets a shorter header and a capped card; phones unchanged.
    val isTablet = LocalAdaptiveLayout.current.isTablet

    LaunchedEffect(showSettingsDialog) {
        if (showSettingsDialog) {
            tempEnvironment = ApiSettings.selectedEnvironment
            try {
                // Parse existing URL to split Host and Port
                val currentUri = URI(ApiSettings.apiBaseUrl)
                tempHost = currentUri.host ?: ""
                val p = currentUri.port
                tempPort = if (p != -1) p.toString() else "80"
            } catch (_: Exception) {
                tempHost = "192.168.27.215"
                tempPort = "7326"
            }
            // Load offline mode setting
            tempOfflineMode = ApiSettings.isOfflineMode
            detectedDbName = null
            connectionTestMsg = null
        }
    }

    // Hero Gradient
    val backgroundBrush = androidx.compose.ui.graphics.Brush.verticalGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            androidx.compose.ui.graphics.Color(0xFFC8102E) // Yakult deep red
        )
    )

    fun showMessage(message: String) {
        scope.launch {
            snackbarHostState.showSnackbar(message)
        }
    }

    fun performLogin() {
        val rawUsername = username.text.trim()
        val rawPassword = password.text.trim()

        if (rawUsername.isBlank() || rawPassword.isBlank()) {
            showMessage("Please enter both username and password.")
            return
        }

        isLoading = true
        focusManager.clearFocus()

        scope.launch {
            when (val result = safeApiCall {
                ApiClient.service.login(LoginRequest(username = rawUsername, password = rawPassword))
            }) {
                is ApiResult.Success -> {
                    val body = result.data
                    if (body.success && !body.token.isNullOrBlank()) {
                        UserSession.loginFromApi(rawUsername, body)
                        UserSession.cacheCredentials(rawUsername, rawPassword)
                        val displayName = (body.name ?: "").trim().ifBlank { rawUsername }
                        snackbarHostState.showSnackbar("Welcome, $displayName!")
                        navController.navigate("root_home") {
                            popUpTo("login") { inclusive = true }
                        }
                    } else {
                        showMessage(body.message.ifBlank { "Login failed." })
                    }
                }
                is ApiResult.HttpError -> {
                    val message = if (result.code == 401) {
                        result.userMessageOr("Invalid username or password.")
                    } else {
                        result.userMessageOr("We couldn't sign you in right now. Please try again.")
                    }
                    showMessage(message)
                }
                is ApiResult.NetworkError -> {
                    if (ApiSettings.isOfflineMode && UserSession.attemptOfflineLogin(rawUsername, rawPassword)) {
                        val displayName = UserSession.currentUser?.displayName ?: rawUsername
                        snackbarHostState.showSnackbar("Welcome back, $displayName! (Offline Mode)")
                        navController.navigate("root_home") {
                            popUpTo("login") { inclusive = true }
                        }
                    } else {
                        showMessage(result.userMessageOr(SCANNER_NETWORK_MESSAGE))
                    }
                }
                is ApiResult.UnknownError -> {
                    showMessage(result.userMessageOr(SCANNER_GENERIC_MESSAGE))
                }
            }
            isLoading = false
        }
    }

    fun constructUrl(): String {
        var host = tempHost.trim()
        val port = tempPort.trim()
        if (host.isBlank()) return ""
        
        // Remove http/https if user typed it, though we labeled it "Server IP"
        host = host.removePrefix("http://").removePrefix("https://").removeSuffix("/")
        
        return "http://$host:$port/"
    }

    fun saveSettings() {
        val newUrl = constructUrl()
        if (newUrl.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("IP Address cannot be empty") }
            return
        }
        
        val approved = ApiSettings.approvedEnvironments.firstOrNull { option ->
            option.environment == tempEnvironment && option.baseUrl.trimEnd('/') == newUrl.trimEnd('/')
        }
        if (approved != null) ApiSettings.selectEnvironment(approved.environment) else ApiSettings.setBaseUrl(newUrl)
        ApiSettings.isOfflineMode = tempOfflineMode
        ApiClient.clearCachedService()
        showSettingsDialog = false
        scope.launch {
            val msg = if (tempOfflineMode) "Settings saved (Offline Mode enabled)" else "API set to $newUrl"
            snackbarHostState.showSnackbar(msg)
        }
    }

    fun parseDbNameFromResponse(responseBody: String): String? {
        return try {
            val gson = Gson()
            val healthResponse = gson.fromJson(responseBody, com.example.yakultscanner.api.HealthResponse::class.java)
            healthResponse?.database?.takeIf { it?.isNotBlank() == true }
        } catch (e: Exception) {
            // If parsing fails, try regex as fallback
            try {
                val regex = """"database"\s*:\s*"([^"]+)"""".toRegex()
                regex.find(responseBody)?.groupValues?.get(1)
            } catch (_: Exception) {
                null
            }
        }
    }

    fun testConnection() {
        scope.launch {
            val testUrl = constructUrl()
            if (testUrl.isBlank()) {
                connectionTestMsg = "Invalid IP/Port"
                return@launch
            }

            isTestingConnection = true
            connectionTestMsg = "Checking API health..."
            
            try {
                kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                    try {
                        val client = okhttp3.OkHttpClient.Builder()
                             .connectTimeout(10, java.util.concurrent.TimeUnit.SECONDS)
                             .readTimeout(15, java.util.concurrent.TimeUnit.SECONDS)
                             .callTimeout(20, java.util.concurrent.TimeUnit.SECONDS)
                             .build()

                        val baseUrl = testUrl.trimEnd('/')

                        // Test the tiny handler first; the website root can be slow or precompiled.
                        val dbInfoUrl = baseUrl + "/dbinfo.ashx"
                        val dbInfoRequest = okhttp3.Request.Builder().url(dbInfoUrl).get().build()
                        var reachedServer = false
                        client.newCall(dbInfoRequest).execute().use { dbResponse ->
                             reachedServer = dbResponse.isSuccessful
                             if (dbResponse.isSuccessful) {
                                 val dbBody = dbResponse.body?.string() ?: ""
                                 Log.d("LoginScreen", "dbinfo.ashx response: $dbBody")
                                 detectedDbName = parseDbNameFromResponse(dbBody)
                             }
                        }

                        if (!reachedServer) {
                             val healthRequest = okhttp3.Request.Builder().url(baseUrl + "/api/health").get().build()
                             client.newCall(healthRequest).execute().use { healthResponse ->
                                 reachedServer = healthResponse.isSuccessful
                                 if (healthResponse.isSuccessful) {
                                     val healthBody = healthResponse.body?.string() ?: ""
                                     detectedDbName = parseDbNameFromResponse(healthBody)
                                 }
                             }
                        }

                        if (!reachedServer) {
                             connectionTestMsg = "Couldn't reach the API at that address. Please verify the server settings."
                             return@withContext
                        }

                        val dbDisplay = if (!detectedDbName.isNullOrBlank()) " | DB: $detectedDbName" else ""
                        connectionTestMsg = "Connected! (Yakult API$dbDisplay)"
                    } catch (e: Exception) {
                         Log.e("LoginScreen", "Connection test failed", e)
                         connectionTestMsg = "Connection test failed. Please verify the server address and try again."
                    }
                }
            } catch (e: Exception) {
                connectionTestMsg = "Connection test failed. Please try again."
            } finally {
                isTestingConnection = false
            }
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
        containerColor = androidx.compose.ui.graphics.Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(backgroundBrush)
                .padding(paddingValues)
        ) {
            if (showSettingsDialog) {
                AlertDialog(
                    onDismissRequest = { showSettingsDialog = false },
                    title = { Text("App Settings") },
                    text = {
                        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                            Text("Configure backend connection.")
                            Text("Approved environment", style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Bold)
                            androidx.compose.foundation.layout.Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                ApiSettings.approvedEnvironments.forEach { option ->
                                    val selected = tempEnvironment == option.environment
                                    if (selected) {
                                        Button(onClick = { }, modifier = Modifier.weight(1f)) { Text(option.label) }
                                    } else {
                                        OutlinedButton(
                                            onClick = {
                                                tempEnvironment = option.environment
                                                val uri = URI(option.baseUrl)
                                                tempHost = uri.host.orEmpty()
                                                tempPort = if (uri.port == -1) "80" else uri.port.toString()
                                            },
                                            modifier = Modifier.weight(1f)
                                        ) { Text(option.label) }
                                    }
                                }
                            }
                            if (tempEnvironment == ApiEnvironment.CUSTOM) {
                                Text("Custom diagnostic URL. Verify it carefully before signing in.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                            }
                            
                            androidx.compose.foundation.layout.Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                OutlinedTextField(
                                    value = tempHost,
                                    onValueChange = { tempHost = it; tempEnvironment = ApiEnvironment.CUSTOM },
                                    label = { Text("Server IP") },
                                    modifier = Modifier.weight(1f),
                                    singleLine = true,
                                    colors = OutlinedTextFieldDefaults.colors(
                                        focusedBorderColor = MaterialTheme.colorScheme.primary,
                                        unfocusedBorderColor = MaterialTheme.colorScheme.outline
                                    )
                                )
                                
                                OutlinedTextField(
                                    value = tempPort,
                                    onValueChange = { tempPort = it.filter { ch -> ch.isDigit() }; tempEnvironment = ApiEnvironment.CUSTOM },
                                    label = { Text("Port") },
                                    modifier = Modifier.width(90.dp),
                                    singleLine = true,
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    colors = OutlinedTextFieldDefaults.colors(
                                        focusedBorderColor = MaterialTheme.colorScheme.primary,
                                        unfocusedBorderColor = MaterialTheme.colorScheme.outline
                                    )
                                )
                            }
                            
                            // Test Connection Row
                            androidx.compose.foundation.layout.Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Button(
                                    onClick = { testConnection() },
                                    enabled = !isTestingConnection && tempHost.isNotBlank()
                                ) {
                                    if (isTestingConnection) {
                                        CircularProgressIndicator(
                                            modifier = Modifier.size(16.dp),
                                            color = MaterialTheme.colorScheme.onPrimary,
                                            strokeWidth = 2.dp
                                        )
                                        Spacer(modifier = Modifier.size(8.dp))
                                    }
                                    Text("Test Connection")
                                }
                            }
                            
                            if (connectionTestMsg != null) {
                                Text(
                                    text = connectionTestMsg!!,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = if (connectionTestMsg!!.startsWith("Connected")) 
                                                androidx.compose.ui.graphics.Color(0xFF2E7D32) // Green
                                            else 
                                                MaterialTheme.colorScheme.error
                                )
                            }

                            // Offline Mode Toggle
                            androidx.compose.foundation.layout.Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = "Offline Mode",
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontWeight = FontWeight.Medium
                                    )
                                    Text(
                                        text = "Save scans locally, sync when online",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                                    )
                                }
                                androidx.compose.material3.Switch(
                                    checked = tempOfflineMode,
                                    onCheckedChange = { tempOfflineMode = it }
                                )
                            }

                            Spacer(modifier = Modifier.height(8.dp))
                            
                            Text(
                                text = "App Version: ${BuildConfig.VERSION_NAME}",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f),
                                modifier = Modifier.align(Alignment.CenterHorizontally)
                            )
                        }
                    },
                    confirmButton = {
                        TextButton(onClick = { saveSettings() }) {
                            Text("Save")
                        }
                    },
                    dismissButton = {
                        TextButton(onClick = { showSettingsDialog = false }) {
                            Text("Cancel")
                        }
                    }
                )
            }

            // A restrained background texture keeps Yakult identity without competing with the form.
            Image(
                painter = painterResource(id = R.drawable.yor_splash_login_bg),
                contentDescription = null,
                modifier = Modifier.fillMaxSize().graphicsLayer(alpha = 0.26f),
                contentScale = ContentScale.Crop
            )

            Box(
                modifier = Modifier
                    .align(Alignment.TopStart)
                    .padding(top = 112.dp, start = 28.dp)
                    .size(58.dp)
                    .background(Color.White.copy(alpha = 0.12f), CircleShape)
            )
            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(bottom = 120.dp, end = 22.dp)
                    .size(94.dp)
                    .background(Color.White.copy(alpha = 0.08f), CircleShape)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 24.dp, vertical = if (isTablet) 16.dp else 28.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Surface(
                    shape = RoundedCornerShape(999.dp),
                    color = Color.White.copy(alpha = 0.16f),
                    border = androidx.compose.foundation.BorderStroke(1.dp, Color.White.copy(alpha = 0.22f))
                ) {
                    Text(
                        text = "INTERNAL INVENTORY ACCESS",
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                        style = MaterialTheme.typography.labelSmall,
                        fontWeight = FontWeight.Bold,
                        color = Color.White
                    )
                }

                Spacer(modifier = Modifier.height(if (isTablet) 10.dp else 18.dp))
                Text(
                    text = "Yakult IT Mobile",
                    style = if (isTablet) MaterialTheme.typography.headlineSmall else MaterialTheme.typography.displaySmall,
                    fontWeight = FontWeight.Bold,
                    color = Color.White,
                    textAlign = TextAlign.Center
                )
                Text(
                    text = "Secure access for authorized Yakult staff",
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color.White.copy(alpha = 0.84f),
                    textAlign = TextAlign.Center,
                    modifier = Modifier.padding(top = 6.dp, bottom = if (isTablet) 12.dp else 26.dp)
                )

                androidx.compose.animation.AnimatedVisibility(
                    visible = isVisible,
                    enter = androidx.compose.animation.slideInVertically(
                        initialOffsetY = { 28 },
                        animationSpec = androidx.compose.animation.core.tween(durationMillis = 480, easing = androidx.compose.animation.core.FastOutSlowInEasing)
                    ) + androidx.compose.animation.fadeIn(animationSpec = androidx.compose.animation.core.tween(480))
                ) {
                    Card(
                        // NOTE: widthIn must come BEFORE fillMaxWidth, otherwise the
                        // fill's min-width survives and the max cap is ignored.
                        modifier = if (isTablet) Modifier.widthIn(max = 560.dp).fillMaxWidth() else Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(30.dp),
                        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBF8)),
                        elevation = CardDefaults.cardElevation(defaultElevation = 14.dp)
                    ) {
                        Column(
                            modifier = Modifier.fillMaxWidth().padding(24.dp),
                            verticalArrangement = Arrangement.spacedBy(14.dp)
                        ) {
                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                                Surface(
                                    shape = CircleShape,
                                    color = MaterialTheme.colorScheme.primary.copy(alpha = 0.12f),
                                    modifier = Modifier.size(42.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Text("Y", color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.ExtraBold, style = MaterialTheme.typography.titleLarge)
                                    }
                                }
                                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                    Text("Welcome back", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                    Text("Sign in with your Yakult account to continue.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            }

                            OutlinedTextField(
                                value = username,
                                onValueChange = { username = it },
                                label = { Text("Username or employee ID") },
                                leadingIcon = { Icon(Icons.Filled.Person, contentDescription = null) },
                                modifier = Modifier.fillMaxWidth(),
                                enabled = !isLoading,
                                shape = RoundedCornerShape(16.dp),
                                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Next)
                            )

                            OutlinedTextField(
                                value = password,
                                onValueChange = { password = it },
                                label = { Text("Password") },
                                leadingIcon = { Icon(Icons.Filled.Lock, contentDescription = null) },
                                modifier = Modifier.fillMaxWidth(),
                                enabled = !isLoading,
                                shape = RoundedCornerShape(16.dp),
                                visualTransformation = if (isPasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                trailingIcon = {
                                    IconButton(onClick = { isPasswordVisible = !isPasswordVisible }, enabled = !isLoading) {
                                        Icon(
                                            imageVector = if (isPasswordVisible) Icons.Filled.VisibilityOff else Icons.Filled.Visibility,
                                            contentDescription = if (isPasswordVisible) "Hide password" else "Show password"
                                        )
                                    }
                                },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
                                keyboardActions = KeyboardActions(onDone = { performLogin() })
                            )

                            Button(
                                onClick = { performLogin() },
                                modifier = Modifier.fillMaxWidth().height(54.dp),
                                enabled = !isLoading,
                                shape = RoundedCornerShape(16.dp)
                            ) {
                                if (isLoading) {
                                    CircularProgressIndicator(modifier = Modifier.size(22.dp), color = MaterialTheme.colorScheme.onPrimary, strokeWidth = 2.dp)
                                } else {
                                    Text("Sign in", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                }
                            }

                            Surface(
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(14.dp),
                                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
                            ) {
                                Row(
                                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    Icon(Icons.Filled.Lock, contentDescription = null, modifier = Modifier.size(16.dp), tint = MaterialTheme.colorScheme.primary)
                                    Text("Use only your authorized Yakult account.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            }

                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                                Text("New to Yakult?", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                TextButton(onClick = { navController.navigate("register") }, enabled = !isLoading) {
                                    Text("Create an account")
                                }
                            }
                        }
                    }
                }
            }
            IconButton(
                onClick = { showSettingsDialog = true },
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(top = 34.dp, end = 20.dp)
                    .size(44.dp)
                    .background(Color.White.copy(alpha = 0.16f), CircleShape)
            ) {
                Icon(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = "Connection settings",
                    tint = Color.White
                )
            }
        }
    }
}