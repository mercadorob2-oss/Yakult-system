package com.example.yakultscanner

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.provider.Settings
import android.util.Log

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.Camera
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.animation.animateColorAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api

import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.navigation.NavController
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.Executors
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import androidx.compose.material.icons.filled.FlashlightOff
import androidx.compose.material.icons.filled.FlashlightOn
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.automirrored.filled.ArrowBack

import com.google.gson.Gson
import com.example.yakultscanner.data.model.DispatchSet
import com.example.yakultscanner.addScanToHistory
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.SCANNER_INVALID_QR_MESSAGE
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import com.example.yakultscanner.api.SCANNER_UNSUPPORTED_QR_MESSAGE
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.utils.normalizeSerial
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import kotlinx.coroutines.launch
import java.util.UUID

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EnhancedScannerScreen(
    navController: NavController,
    useDeviceScannerMode: Boolean,
    returnRoute: String = "details"
) {
    val context = LocalContext.current
    val adaptiveLayout = LocalAdaptiveLayout.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val haptic = LocalHapticFeedback.current

    // States
    var hasScanned by remember { mutableStateOf(false) }
    var lastScanAtMs by remember { mutableStateOf(0L) }
    var isFlashlightOn by remember { mutableStateOf(false) }
    val cameraState = remember { mutableStateOf<Camera?>(null) }
    var isScanning by remember { mutableStateOf(false) }
    var lastScanStatus by remember { mutableStateOf<String?>("Ready") }
    var lastScanStatusColor by remember { mutableStateOf(Color.White.copy(alpha = 0.85f)) }
    var showSuccessOverlay by remember { mutableStateOf(false) }

    val isSerialFlow = isSerialReturnRoute(returnRoute)
    val flowLabel = if (isSerialFlow) "Serial" else "Dispatch"

    val useDeviceScanner = useDeviceScannerMode
    var hardwareInput by remember { mutableStateOf("") }
    val hardwareFocusRequester = remember { FocusRequester() }

    var hasCameraPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA)
                    == PackageManager.PERMISSION_GRANTED
        )
    }

    val analysisExecutor = remember { Executors.newSingleThreadExecutor() }
    DisposableEffect(Unit) {
        onDispose { analysisExecutor.shutdown() }
    }

    var continuousSerialMode by rememberSaveable { mutableStateOf(false) }
    val pendingSerials = remember { mutableStateListOf<String>() }

    fun commitPendingSerials() {
        if (!isSerialFlow || pendingSerials.isEmpty()) return
        val previous = navController.previousBackStackEntry ?: return
        previous.savedStateHandle[SCAN_RESULT_SERIALS_KEY] = ArrayList(pendingSerials)
        navController.popBackStack()
    }


    suspend fun handleScannedValue(raw: String) {
        val scannedValue = raw.trim()
        if (scannedValue.isEmpty()) return

        if (hasScanned) return

        val now = System.currentTimeMillis()
        if (now - lastScanAtMs < 1200L) return
        lastScanAtMs = now

        hasScanned = true
        lastScanStatus = "Scan received"
        lastScanStatusColor = Color.White.copy(alpha = 0.85f)

        if (isSerialFlow) {

            val previous = navController.previousBackStackEntry
            if (previous == null) {
                hasScanned = false
                lastScanStatus = "No screen to return result to"
                lastScanStatusColor = Color(0xFFFF6B6B)
                haptic.performHapticFeedback(HapticFeedbackType.LongPress)

                snackbarHostState.showSnackbar("No screen to return the scan result to.")
                return
            }

            val normalized = normalizeSerial(scannedValue)
            if (normalized.isBlank()) {
                hasScanned = false
                lastScanStatus = "Invalid serial"
                lastScanStatusColor = Color(0xFFFF6B6B)
                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                snackbarHostState.showSnackbar("Invalid serial")
                return
            }

            if (!continuousSerialMode) {
                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                previous.savedStateHandle[SCAN_RESULT_SERIAL_KEY] = normalized
                navController.popBackStack()
                return
            }

            if (pendingSerials.any { it == normalized }) {
                hasScanned = false
                lastScanStatus = "Already captured (${pendingSerials.size})"
                lastScanStatusColor = Color(0xFFFFB86B)
                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                snackbarHostState.showSnackbar("Serial already captured")
                return
            }

            haptic.performHapticFeedback(HapticFeedbackType.LongPress)
            pendingSerials.add(normalized)
            lastScanStatus = "Added to batch (${pendingSerials.size})"
            lastScanStatusColor = Color(0xFF67E08D)
            hasScanned = false

            return
        }

        val planAPrefix = "yakult:set:v1:"
        if (scannedValue.startsWith(planAPrefix, ignoreCase = true)) {
            val tokenPart = scannedValue.substring(planAPrefix.length).trim()
            val tokenGuid = try {
                UUID.fromString(tokenPart)
                tokenPart
            } catch (_: Exception) {
                null
            }

            if (tokenGuid == null) {
                hasScanned = false
                lastScanStatus = "Unsupported QR code"
                lastScanStatusColor = Color(0xFFFF6B6B)
                haptic.performHapticFeedback(HapticFeedbackType.LongPress)

                snackbarHostState.showSnackbar(SCANNER_INVALID_QR_MESSAGE)
                return
            }

            when (val result = safeApiCall { ApiClient.service.getDispatchSet(token = tokenGuid) }) {
                is ApiResult.Success -> {
                    val set = result.data.set
                    if (!result.data.success || set == null || set.setCode.isNullOrBlank()) {
                        hasScanned = false
                        lastScanStatus = "Couldn't load set"
                        lastScanStatusColor = Color(0xFFFFB86B)
                        haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                        snackbarHostState.showSnackbar("We couldn't load the dispatch details for this QR code. Please try again.")
                        return
                    }

                    lastScanStatus = "Set loaded"
                    lastScanStatusColor = Color(0xFF67E08D)
                    showSuccessOverlay = true

                    val resolvedJson = Gson().toJson(set)
                    addScanToHistory(context, resolvedJson, set)

                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    val encodedValue = URLEncoder.encode(
                        resolvedJson,
                        StandardCharsets.UTF_8.toString()
                    )
                    val tokenQuery = if (returnRoute == "details") "?token=$tokenGuid" else ""
                    navController.navigate("$returnRoute/$encodedValue$tokenQuery")
                    return
                }
                is ApiResult.HttpError -> {
                    hasScanned = false
                    lastScanStatus = "Couldn't load set"
                    lastScanStatusColor = Color(0xFFFFB86B)
                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    snackbarHostState.showSnackbar(
                        result.userMessageOr("We couldn't load the dispatch details for this QR code. Please try again.")
                    )
                    return
                }
                is ApiResult.NetworkError -> {
                    hasScanned = false
                    lastScanStatus = "Connection issue"
                    lastScanStatusColor = Color(0xFFFFB86B)
                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    snackbarHostState.showSnackbar(result.userMessageOr(SCANNER_NETWORK_MESSAGE))
                    return
                }
                is ApiResult.UnknownError -> {
                    hasScanned = false
                    lastScanStatus = "Scan error"
                    lastScanStatusColor = Color(0xFFFFB86B)
                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    snackbarHostState.showSnackbar(result.userMessageOr(SCANNER_GENERIC_MESSAGE))
                    return
                }
            }
        }

        val dispatchSet = try {
            Gson().fromJson(scannedValue, DispatchSet::class.java)
        } catch (_: Exception) {
            null
        }

        if (dispatchSet == null || dispatchSet.setCode.isNullOrBlank()) {
            hasScanned = false
            lastScanStatus = "Unsupported QR code"
            lastScanStatusColor = Color(0xFFFF6B6B)
            haptic.performHapticFeedback(HapticFeedbackType.LongPress)
            snackbarHostState.showSnackbar(SCANNER_UNSUPPORTED_QR_MESSAGE)
            return
        }

        try {
            addScanToHistory(context, scannedValue, dispatchSet)
        } catch (_: Exception) {
        }

        haptic.performHapticFeedback(HapticFeedbackType.LongPress)
        lastScanStatus = "QR scanned"
        lastScanStatusColor = Color(0xFF67E08D)
        showSuccessOverlay = true

        val encodedValue = URLEncoder.encode(
            scannedValue,
            StandardCharsets.UTF_8.toString()
        )
        navController.navigate("$returnRoute/$encodedValue")
    }

    // Gallery launcher
    val galleryLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.PickVisualMedia()
    ) { uri: Uri? ->
        if (uri != null) {
            lastScanAtMs = 0L
            processGalleryImage(context, uri) { qrCode ->
                val scannedValue = qrCode.rawValue ?: ""
                scope.launch { handleScannedValue(scannedValue) }
            }
        } else {
            scope.launch {
                snackbarHostState.showSnackbar("No image selected")
            }
        }
    }

    // Camera permission launcher
    val cameraPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        hasCameraPermission = isGranted
        if (!isGranted) {
            lastScanStatus = "Camera permission required"
            lastScanStatusColor = Color(0xFFFFB86B)
            scope.launch {
                snackbarHostState.showSnackbar("Camera permission is required for scanning. You can also enable it in Settings.")
            }
        }
    }

    fun openAppSettings(context: Context) {
        val intent = Intent(
            Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
            Uri.fromParts("package", context.packageName, null)
        )
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(intent)
    }

    LaunchedEffect(useDeviceScanner) {
        if (useDeviceScanner) {
            hasScanned = false
            lastScanAtMs = 0L
            hardwareInput = ""
            lastScanStatus = "Ready"
            lastScanStatusColor = Color.White.copy(alpha = 0.85f)
            // Ensure the hardware input field has focus so external scanners in keyboard-wedge mode
            // send their keystrokes directly into it.
            hardwareFocusRequester.requestFocus()
        }
    }

    val navBackStackEntry = navController.currentBackStackEntry
    val scannerEventFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_SCREEN_EVENT_KEY, null)
    }
    val scannerEvent = scannerEventFlow?.collectAsState(initial = null)?.value

    LaunchedEffect(scannerEvent) {
        val incoming = scannerEvent?.trim().orEmpty()
        if (incoming.isBlank()) return@LaunchedEffect

        handleScannedValue(incoming)
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_SCREEN_EVENT_KEY)
    }

    DisposableEffect(lifecycleOwner, useDeviceScanner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                hasScanned = false
                lastScanAtMs = 0L
                lastScanStatus = "Ready"
                lastScanStatusColor = Color.White.copy(alpha = 0.85f)
                if (useDeviceScanner) {
                    hardwareInput = ""
                    hardwareFocusRequester.requestFocus()
                }
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
        }
    }

    // Auto-dismiss success overlay after 1.2s
    LaunchedEffect(showSuccessOverlay) {
        if (showSuccessOverlay) {
            kotlinx.coroutines.delay(1200L)
            showSuccessOverlay = false
        }
    }

    // Animated border color
    val borderColor by animateColorAsState(
        targetValue = when {
            hasScanned -> Color(0xFF67E08D)
            isScanning -> Color.Green.copy(alpha = 0.7f)
            else -> MaterialTheme.colorScheme.primary
        },
        animationSpec = androidx.compose.animation.core.tween(300),

        label = "borderColor"
    )

    // Immersive Layout
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
    ) {
        // 1. Camera Layer
        if (!useDeviceScanner) {
            if (hasCameraPermission) {
                AndroidView(
                    factory = { ctx ->
                        val previewView = PreviewView(ctx)
                        val cameraProviderFuture = ProcessCameraProvider.getInstance(ctx)

                        cameraProviderFuture.addListener({
                            val cameraProvider = cameraProviderFuture.get()
                            val preview = Preview.Builder().build().also {
                                it.setSurfaceProvider(previewView.surfaceProvider)
                            }

                            val imageAnalyzer = ImageAnalysis.Builder()
                                .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                                .build()
                                .also {
                                    it.setAnalyzer(analysisExecutor) { imageProxy ->
                                        // Only scan if not already scanned to save resources/battery
                                        if (!hasScanned) {
                                            isScanning = true
                                            processImageProxy(imageProxy) { qrCode ->
                                                val scannedValue = qrCode.rawValue ?: ""
                                                previewView.post {
                                                    scope.launch { handleScannedValue(scannedValue) }
                                                }
                                            }
                                        } else {
                                            isScanning = false
                                            imageProxy.close()
                                        }
                                    }
                                }

                            try {
                                cameraProvider.unbindAll()
                                val camera = cameraProvider.bindToLifecycle(
                                    lifecycleOwner,
                                    CameraSelector.DEFAULT_BACK_CAMERA,
                                    preview,
                                    imageAnalyzer
                                )
                                cameraState.value = camera
                                camera.cameraControl.enableTorch(isFlashlightOn)
                            } catch (exc: Exception) {
                                Log.e("ScannerScreen", "Use case binding failed", exc)
                            }
                        }, ContextCompat.getMainExecutor(ctx))
                        previewView
                    },
                    modifier = Modifier.fillMaxSize()
                )
                
                // 2. Overlay Layer
                com.example.yakultscanner.ui.components.ScannerOverlay(
                    isScanning = isScanning,
                    borderColor = borderColor,
                    maxFrameSize = if (adaptiveLayout.isTablet) 520.dp else androidx.compose.ui.unit.Dp.Unspecified
                )
            } else {
                // Permission Denied Sate
                 Column(
                    modifier = Modifier.align(Alignment.Center),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        "Camera Access Required",
                        color = Color.White,
                        style = MaterialTheme.typography.titleLarge
                    )
                    Spacer(modifier = Modifier.height(16.dp))
                    Button(onClick = { cameraPermissionLauncher.launch(Manifest.permission.CAMERA) }) {
                        Text("Grant Permission")
                    }
                }
            }
        } else {
            // Device Scanner Mode
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(24.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.QrCodeScanner,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier
                            .size(64.dp)
                            .padding(bottom = 16.dp)
                    )
                    Text(
                        text = "Device Scanner Ready",
                        style = MaterialTheme.typography.headlineSmall,
                        color = Color.White
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Press hardware button to scan",
                        style = MaterialTheme.typography.bodyMedium,
                        color = Color.White.copy(alpha = 0.7f)
                    )
                    
                    // Invisible input to catch hardware wedge
                    BasicTextField(
                        value = hardwareInput,
                        onValueChange = { newText ->
                             val hasTerminator = newText.any { it == '\n' || it == '\r' }
                            if (hasTerminator) {
                                val value = newText.replace("\n", "").replace("\r", "").trim()
                                hardwareInput = ""
                                if (value.isNotEmpty()) {
                                    scope.launch { handleScannedValue(value) }
                                }
                            } else {
                                hardwareInput = newText
                            }
                        },
                        modifier = Modifier
                            .size(1.dp)
                            .focusRequester(hardwareFocusRequester),
                        keyboardOptions = KeyboardOptions.Default.copy(imeAction = ImeAction.Done),
                         keyboardActions = KeyboardActions(onDone = {
                            val value = hardwareInput.trim()
                            hardwareInput = ""
                            if (value.isNotEmpty()) {
                                scope.launch { handleScannedValue(value) }
                            }
                        })
                    )
                }
            }
        }

        // 4. Success Overlay
        androidx.compose.animation.AnimatedVisibility(
            visible = showSuccessOverlay,
            enter = androidx.compose.animation.fadeIn(animationSpec = androidx.compose.animation.core.tween(150)) +
                    androidx.compose.animation.scaleIn(initialScale = 0.6f, animationSpec = androidx.compose.animation.core.spring(dampingRatio = 0.5f)),
            exit = androidx.compose.animation.fadeOut(animationSpec = androidx.compose.animation.core.tween(300)) +
                   androidx.compose.animation.scaleOut(targetScale = 1.2f, animationSpec = androidx.compose.animation.core.tween(300)),
            modifier = Modifier.fillMaxSize()
        ) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(Color.Black.copy(alpha = 0.45f)),
                contentAlignment = Alignment.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(140.dp)
                        .background(
                            color = Color(0xFF67E08D),
                            shape = androidx.compose.foundation.shape.CircleShape
                        ),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = "✓",
                        fontSize = 72.sp,
                        color = Color.White,
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }
            }
        }


        // 3. Top Navigation (Overlay)
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 48.dp, start = 8.dp, end = 16.dp), // Top padding for status bar
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = { navController.popBackStack() }) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                    contentDescription = "Back",
                    tint = Color.White,
                    modifier = Modifier.background(Color.Black.copy(alpha = 0.3f), androidx.compose.foundation.shape.CircleShape).padding(8.dp)
                )
            }
            Spacer(modifier = Modifier.width(16.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = when {
                        isSerialFlow && useDeviceScanner -> "Serial Scanner"
                        isSerialFlow -> "Scan Serial"
                        useDeviceScanner -> "Hardware Scanner"
                        else -> "Scan QR Code"
                    },
                    style = MaterialTheme.typography.titleLarge,
                    color = Color.White,
                    modifier = Modifier.shadow(8.dp)
                )
                Text(
                    text = "Flow: $flowLabel",
                    style = MaterialTheme.typography.bodySmall,
                    color = Color.White.copy(alpha = 0.75f)
                )
            }

            if (!lastScanStatus.isNullOrBlank()) {
                Surface(
                    color = Color.Black.copy(alpha = 0.35f),
                    shape = androidx.compose.foundation.shape.RoundedCornerShape(10.dp),
                    tonalElevation = 0.dp
                ) {
                    Text(
                        text = lastScanStatus ?: "",
                        color = lastScanStatusColor,
                        fontSize = 12.sp,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                        maxLines = 1
                    )
                }
            }
        }

        if (isSerialFlow) {
            Surface(
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = 112.dp, start = 16.dp, end = 16.dp)
                    .then(if (adaptiveLayout.isTablet) Modifier.widthIn(max = 520.dp) else Modifier),
                color = Color.Black.copy(alpha = 0.45f),
                shape = androidx.compose.foundation.shape.RoundedCornerShape(18.dp)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = if (continuousSerialMode) "Batch serial capture" else "Single serial capture",
                                color = Color.White,
                                style = MaterialTheme.typography.titleSmall
                            )
                            Text(
                                text = if (continuousSerialMode) {
                                    "${pendingSerials.size} serial(s) ready"
                                } else {
                                    "Turn on batch mode to capture multiple serials before returning."
                                },
                                color = Color.White.copy(alpha = 0.75f),
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        Switch(
                            checked = continuousSerialMode,
                            onCheckedChange = { enabled ->
                                continuousSerialMode = enabled
                                hasScanned = false
                                if (!enabled && pendingSerials.isNotEmpty()) {
                                    pendingSerials.clear()
                                }
                            }
                        )
                    }

                    if (continuousSerialMode) {
                        if (pendingSerials.isEmpty()) {
                            Text(
                                text = "Scanned serials will appear here.",
                                color = Color.White.copy(alpha = 0.8f),
                                style = MaterialTheme.typography.bodySmall
                            )
                        } else {
                            LazyColumn(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(max = 140.dp),
                                verticalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                items(pendingSerials.reversed()) { serial ->
                                    Surface(
                                        color = Color.White.copy(alpha = 0.1f),
                                        shape = androidx.compose.foundation.shape.RoundedCornerShape(12.dp)
                                    ) {
                                        Text(
                                            text = serial,
                                            color = Color.White,
                                            modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                                            style = MaterialTheme.typography.bodyMedium
                                        )
                                    }
                                }
                            }
                        }

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            OutlinedButton(
                                onClick = { pendingSerials.clear() },
                                enabled = pendingSerials.isNotEmpty(),
                                modifier = Modifier.weight(1f)
                            ) {
                                Text("Clear")
                            }
                            Button(
                                onClick = { commitPendingSerials() },
                                enabled = pendingSerials.isNotEmpty(),
                                modifier = Modifier.weight(1f)
                            ) {
                                Text("Use ${pendingSerials.size}")
                            }
                        }
                    }
                }
            }
        }

        // 4. Bottom Controls (Only for Camera Mode)
        if (!useDeviceScanner && hasCameraPermission) {
            com.example.yakultscanner.ui.components.ScannerControls(
                isFlashlightOn = isFlashlightOn,
                onToggleFlashlight = {
                    isFlashlightOn = !isFlashlightOn
                    cameraState.value?.cameraControl?.enableTorch(isFlashlightOn)
                },
                onOpenHistory = { navController.navigate("history") },
                onOpenGallery = {
                    // Photo Picker/SAF does not require storage permissions.
                    galleryLauncher.launch(
                        PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)
                    )
                },
                modifier = Modifier.align(Alignment.BottomCenter)
            )
        }

        // 5. Snackbar
        SnackbarHost(
            hostState = snackbarHostState,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = if (!useDeviceScanner) 110.dp else 24.dp)
        )
    }
}

private fun processImageProxy(
    imageProxy: ImageProxy,
    onQrCodeScanned: (Barcode) -> Unit
) {
    @androidx.annotation.OptIn(androidx.camera.core.ExperimentalGetImage::class)
    val mediaImage = imageProxy.image
    if (mediaImage != null) {
        val image = InputImage.fromMediaImage(
            mediaImage,
            imageProxy.imageInfo.rotationDegrees
        )

        val options = BarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()

        val scanner = BarcodeScanning.getClient(options)

        scanner.process(image)
            .addOnSuccessListener { barcodes ->
                barcodes.firstOrNull()?.let(onQrCodeScanned)
            }
            .addOnFailureListener {
                Log.e("QrScanner", "Failed to scan QR code", it)
            }
            .addOnCompleteListener {
                imageProxy.close()
            }
    } else {
        imageProxy.close()
    }
}

private fun processGalleryImage(
    context: Context,
    imageUri: Uri,
    onQrCodeScanned: (Barcode) -> Unit
) {
    try {
        val image = InputImage.fromFilePath(context, imageUri)
        val options = BarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()
        val scanner = BarcodeScanning.getClient(options)

        scanner.process(image)
            .addOnSuccessListener { barcodes ->
                barcodes.firstOrNull()?.let(onQrCodeScanned) ?: run {
                    android.widget.Toast.makeText(
                        context,
                        "No QR code found in the selected image",
                        android.widget.Toast.LENGTH_SHORT
                    ).show()
                }
            }
            .addOnFailureListener {
                Log.e("QrScanner", "Failed to scan QR code from gallery image", it)
                android.widget.Toast.makeText(
                    context,
                    "We couldn't read a QR code from that image. Please try a clearer image or scan again.",
                    android.widget.Toast.LENGTH_SHORT
                ).show()
            }
    } catch (e: Exception) {
        Log.e("QrScanner", "Error processing gallery image", e)
        android.widget.Toast.makeText(
            context,
            "We couldn't open that image. Please select another image and try again.",
            android.widget.Toast.LENGTH_SHORT
        ).show()
    }
}
