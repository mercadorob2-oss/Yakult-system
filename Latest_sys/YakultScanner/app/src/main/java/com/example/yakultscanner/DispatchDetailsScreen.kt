package com.example.yakultscanner

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.Base64
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.background
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import java.util.Locale
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.Image
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import java.io.ByteArrayOutputStream
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.navigation.NavController
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import com.example.yakultscanner.api.SetItemUpdateDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.data.model.DispatchItem
import com.example.yakultscanner.settings.ApiSettings
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.example.yakultscanner.data.model.DispatchSet
import com.example.yakultscanner.data.model.ItemEditState
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.ui.components.SignatureDialog
import com.example.yakultscanner.ui.components.SignatureView
import com.example.yakultscanner.ui.components.ConfirmSensitiveActionSheet
import com.example.yakultscanner.ui.components.Footer
import com.example.yakultscanner.ui.components.ItemStatusChip
import com.example.yakultscanner.ui.components.ReportIssueDialog
import com.example.yakultscanner.ui.components.FullScreenLoadingOverlay
import com.example.yakultscanner.ui.components.DeploymentConfirmationDialog
import com.example.yakultscanner.api.SetConfirmationRequest
import com.example.yakultscanner.api.SetImageDto
import com.example.yakultscanner.api.SetImageUploadRequest
import com.example.yakultscanner.api.getSetImages
import com.example.yakultscanner.api.uploadSetImage
import com.google.mlkit.vision.documentscanner.GmsDocumentScannerOptions
import com.google.mlkit.vision.documentscanner.GmsDocumentScanning
import com.google.mlkit.vision.documentscanner.GmsDocumentScanningResult
import com.example.yakultscanner.updateScanHistoryStatus
import com.example.yakultscanner.utils.generateAndOpenPdf
import com.example.yakultscanner.network.ConnectivityMonitor
import com.example.yakultscanner.utils.normalizeSerial
import com.example.yakultscanner.viewmodels.DispatchDetailsUiState
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel
import com.example.yakultscanner.viewmodels.UploadEvent
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private sealed interface PendingConfirmation {
    data class Deploy(val set: DispatchSet) : PendingConfirmation
    data class Upload(val set: DispatchSet) : PendingConfirmation
}

private enum class FailedAction {
    Deploy,
    Upload
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DispatchDetailsScreen(
    navController: NavController,
    scannedString: String,
    readOnly: Boolean = false,
    token: String? = null,
    viewModel: DispatchDetailsViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val uiState by viewModel.uiState.collectAsState()
    val itemEdits by viewModel.itemEdits.collectAsState()
    val updatesBySerial by viewModel.updatesBySerial.collectAsState()
    val uploadEvent by viewModel.uploadEvent.collectAsState()
    val isUploading by viewModel.isUploading.collectAsState()
    val isSyncing by viewModel.isSyncing.collectAsState()
    val pendingCount by viewModel.pendingCount.collectAsState()

    var dispatcherSignatureView by remember { mutableStateOf<SignatureView?>(null) }
    var requesterSignatureView by remember { mutableStateOf<SignatureView?>(null) }
    var deployed by remember { mutableStateOf(false) }
    var pendingConfirmation by remember { mutableStateOf<PendingConfirmation?>(null) }
    var actionErrorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var lastFailedAction by remember { mutableStateOf<FailedAction?>(null) }
    var isDeploying by remember { mutableStateOf(false) }
    var showDeploymentConfirmation by remember { mutableStateOf(false) }
    val isConnected by viewModel.connectivityMonitor.isConnected.collectAsState()
    val isOfflineMode = remember { ApiSettings.isOfflineMode }

    LaunchedEffect(scannedString) {
        viewModel.loadDispatchSet(scannedString)
    }

    fun startDeploy(setCode: String?) {
        scope.launch {
            actionErrorUi = null
            lastFailedAction = null
            isDeploying = true

            val deployToken = token?.trim().orEmpty()
            if (deployToken.isBlank()) {
                isDeploying = false
                Toast.makeText(context, "Deploy requires a token-based scan.", Toast.LENGTH_LONG).show()
                return@launch
            }

            val setId = when (val resolve = safeApiCall { ApiClient.service.resolveToken(deployToken) }) {
                is ApiResult.Success -> resolve.data.setId
                is ApiResult.HttpError -> {
                    val message = resolve.userMessageOr("We couldn't process the scanned QR code. Please try again.")
                    val details = buildString {
                        appendLine("Endpoint: ${resolve.endpoint ?: "(unknown)"}")
                        appendLine("HTTP: ${resolve.code}")
                        if (!resolve.message.isNullOrBlank()) append("Message: ${resolve.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                    isDeploying = false
                    return@launch
                }
                is ApiResult.NetworkError -> {
                    val message = resolve.userMessageOr(SCANNER_NETWORK_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${resolve.endpoint ?: "(unknown)"}")
                        appendLine("Type: Network error")
                        if (!resolve.message.isNullOrBlank()) append("Message: ${resolve.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                    isDeploying = false
                    return@launch
                }
                is ApiResult.UnknownError -> {
                    val message = resolve.userMessageOr(SCANNER_GENERIC_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${resolve.endpoint ?: "(unknown)"}")
                        appendLine("Type: Unexpected error")
                        if (!resolve.message.isNullOrBlank()) append("Message: ${resolve.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                    isDeploying = false
                    return@launch
                }
            }

            when (val deploy = safeApiCall { ApiClient.service.deploySet(setId) }) {
                is ApiResult.Success -> {
                    if (deploy.data.success) {
                        Toast.makeText(context, "Set deployed: ${deploy.data.message}", Toast.LENGTH_LONG).show()
                        deployed = true
                        updateScanHistoryStatus(context, setCode, "Dispatched")
                        actionErrorUi = null
                        lastFailedAction = null
                    } else {
                        val details = buildString {
                            appendLine("Endpoint: ${deploy.endpoint ?: "(unknown)"}")
                            if (deploy.httpCode != null) appendLine("HTTP: ${deploy.httpCode}")
                            append("Message: ${deploy.data.message}")
                        }.trim()
                        actionErrorUi = "Deploy failed." to details
                        lastFailedAction = FailedAction.Deploy
                    }
                }
                is ApiResult.HttpError -> {
                    val message = deploy.userMessageOr("We couldn't update the dispatch status right now. Please try again.")
                    val details = buildString {
                        appendLine("Endpoint: ${deploy.endpoint ?: "(unknown)"}")
                        appendLine("HTTP: ${deploy.code}")
                        if (!deploy.message.isNullOrBlank()) append("Message: ${deploy.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
                is ApiResult.NetworkError -> {
                    val message = deploy.userMessageOr(SCANNER_NETWORK_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${deploy.endpoint ?: "(unknown)"}")
                        appendLine("Type: Network error")
                        if (!deploy.message.isNullOrBlank()) append("Message: ${deploy.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
                is ApiResult.UnknownError -> {
                    val message = deploy.userMessageOr(SCANNER_GENERIC_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${deploy.endpoint ?: "(unknown)"}")
                        appendLine("Type: Unexpected error")
                        if (!deploy.message.isNullOrBlank()) append("Message: ${deploy.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
            }

            isDeploying = false
        }
    }

    fun handleDeploymentConfirmation(request: SetConfirmationRequest, setCode: String?) {
        scope.launch {
            isDeploying = true
            actionErrorUi = null

            val deployToken = token?.trim().orEmpty()
            if (deployToken.isBlank()) {
                isDeploying = false
                Toast.makeText(context, "Deploy requires a token-based scan.", Toast.LENGTH_LONG).show()
                return@launch
            }

            when (val result = safeApiCall { ApiClient.service.confirmSetDeployment(deployToken, request) }) {
                is ApiResult.Success -> {
                    if (result.data.success) {
                        Toast.makeText(context, result.data.message ?: "Set deployed successfully", Toast.LENGTH_LONG).show()
                        deployed = true
                        updateScanHistoryStatus(context, setCode, "Dispatched")
                        actionErrorUi = null
                        lastFailedAction = null
                        showDeploymentConfirmation = false
                    } else {
                        val details = "Deployment confirmation failed: ${result.data.message}"
                        actionErrorUi = "Deploy failed." to details
                        lastFailedAction = FailedAction.Deploy
                    }
                }
                is ApiResult.HttpError -> {
                    val message = result.userMessageOr("We couldn't confirm the deployment. Please try again.")
                    val details = buildString {
                        appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                        appendLine("HTTP: ${result.code}")
                        if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
                is ApiResult.NetworkError -> {
                    val message = result.userMessageOr(SCANNER_NETWORK_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                        appendLine("Type: Network error")
                        if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
                is ApiResult.UnknownError -> {
                    val message = result.userMessageOr(SCANNER_GENERIC_MESSAGE)
                    val details = buildString {
                        appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                        appendLine("Type: Unexpected error")
                        if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                    }.trim()
                    actionErrorUi = message to details
                    lastFailedAction = FailedAction.Deploy
                }
            }

            isDeploying = false
        }
    }

    LaunchedEffect(uploadEvent) {
        uploadEvent?.let { event ->
            when (event) {
                is UploadEvent.Success -> {
                    Toast.makeText(context, "Uploaded ${event.count} updates.", Toast.LENGTH_LONG).show()
                    actionErrorUi = null
                    lastFailedAction = null
                }
                is UploadEvent.Error -> {
                    actionErrorUi = event.message to event.details
                    lastFailedAction = FailedAction.Upload
                }
            }
            viewModel.clearUploadEvent()
        }
    }
    
    val headerBrush = Brush.verticalGradient(
        colors = listOf(
            com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
            Color(0xFFA36A72)
        )
    )

    Scaffold(
        topBar = {
             TopAppBar(
                title = { 
                    Text(
                        stringResource(id = R.string.dispatch_details_title),
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
                    ) 
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = stringResource(id = R.string.back),
                            tint = Color.White
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = Color.White,
                    navigationIconContentColor = Color.White
                ),
                actions = {
                    // Offline/Online Indicator
                    if (!isConnected || isOfflineMode) {
                        Surface(
                            color = if (isOfflineMode) 
                                MaterialTheme.colorScheme.error.copy(alpha = 0.9f)
                            else 
                                MaterialTheme.colorScheme.tertiary.copy(alpha = 0.9f),
                            shape = RoundedCornerShape(50),
                            modifier = Modifier.padding(end = 8.dp)
                        ) {
                            Row(
                                horizontalArrangement = Arrangement.spacedBy(4.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
                            ) {
                                Icon(
                                    imageVector = if (isOfflineMode) 
                                        Icons.Filled.CloudOff 
                                    else 
                                        Icons.Filled.WifiOff,
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.onError,
                                    modifier = Modifier.size(14.dp)
                                )
                                Text(
                                    text = if (isOfflineMode) "OFFLINE" else "NO NETWORK",
                                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                                    color = MaterialTheme.colorScheme.onError
                                )
                            }
                        }
                    }
                }
             )
        },
        containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
        modifier = Modifier.fillMaxSize()
    ) { paddingValues ->
        Box(
             modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
        ) {
            // Header Background
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(220.dp)
                    .background(headerBrush)
            )
            
            // Content
            when (val state = uiState) {
                is DispatchDetailsUiState.Loading -> {
                    Box(modifier = Modifier.fillMaxSize().padding(paddingValues), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                    }
                }
                is DispatchDetailsUiState.Error -> {
                    Box(modifier = Modifier.fillMaxSize().padding(paddingValues), contentAlignment = Alignment.Center) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(16.dp),
                            modifier = Modifier.padding(40.dp)
                        ) {
                            Surface(
                                color = MaterialTheme.colorScheme.errorContainer,
                                shape = RoundedCornerShape(50),
                                modifier = Modifier.size(72.dp)
                            ) {
                                Box(contentAlignment = Alignment.Center) {
                                    Icon(
                                        imageVector = Icons.Filled.Warning,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.error,
                                        modifier = Modifier.size(36.dp)
                                    )
                                }
                            }
                            Text(
                                text = "Unable to Load",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = state.message,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                textAlign = TextAlign.Center,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    }
                }
                is DispatchDetailsUiState.Success -> {
                    val dispatchSet = state.dispatchSet
                    val displaySet = if (deployed) {
                        dispatchSet.copy(status = "Dispatched")
                    } else {
                        dispatchSet
                    }

                    if (showDeploymentConfirmation) {
                        DeploymentConfirmationDialog(
                            setCode = displaySet.setCode ?: "Unknown",
                            employeeName = displaySet.employee ?: "Unknown",
                            onDismiss = { showDeploymentConfirmation = false },
                            onConfirm = { request ->
                                handleDeploymentConfirmation(request, displaySet.setCode)
                            }
                        )
                    } else if (pendingConfirmation is PendingConfirmation.Deploy) {
                        ConfirmSensitiveActionSheet(
                            title = "Confirm deploy",
                            confirmLabel = "Confirm",
                            setCode = displaySet.setCode,
                            employee = displaySet.employee,
                            branch = displaySet.branch,
                            onConfirm = {
                                pendingConfirmation = null
                                showDeploymentConfirmation = true
                            },
                            onDismiss = { pendingConfirmation = null }
                        )
                    } else if (pendingConfirmation is PendingConfirmation.Upload) {
                        ConfirmSensitiveActionSheet(
                            title = "Confirm upload",
                            confirmLabel = "Confirm",
                            setCode = displaySet.setCode,
                            employee = displaySet.employee,
                            branch = displaySet.branch,
                            onConfirm = {
                                pendingConfirmation = null
                                actionErrorUi = null
                                lastFailedAction = null
                                viewModel.uploadChanges(displaySet)
                            },
                            onDismiss = { pendingConfirmation = null }
                        )
                    }

                    DispatchDetailsContent(
                        dispatchSet = displaySet,
                        itemEdits = itemEdits,
                        onItemEditChange = viewModel::updateItemEdit,
                        onOpenItemDetails = { item ->
                            val serial = item.serialNumber?.trim().orEmpty()
                            if (serial.isNotEmpty()) {
                                val setCode = displaySet.setCode?.trim().orEmpty()
                                val status = displaySet.status?.trim().orEmpty()
                                val employee = displaySet.employee?.trim().orEmpty()
                                navController.navigate(
                                    "dispatch_item_details/${android.net.Uri.encode(serial)}?setCode=${android.net.Uri.encode(setCode)}&dispatchStatus=${android.net.Uri.encode(status)}&employee=${android.net.Uri.encode(employee)}"
                                )
                            }
                        },
                        modifier = Modifier.padding(paddingValues),
                        registerDispatcherSignatureView = { dispatcherSignatureView = it },
                        registerRequesterSignatureView = { requesterSignatureView = it },
                        onGeneratePdf = { set ->
                            handlePdfGeneration(context, set, dispatcherSignatureView, requesterSignatureView)
                        },
                        onUpload = { pendingConfirmation = PendingConfirmation.Upload(displaySet) },
                        onViewUploads = {
                            val setCode = displaySet.setCode
                            if (!setCode.isNullOrBlank()) {
                                GlobalNav.navController?.navigate("uploaded/$setCode")
                            }
                        },
                        onDeploy = {
                            pendingConfirmation = PendingConfirmation.Deploy(displaySet)
                        },
                        onSyncNow = { viewModel.syncNow() },
                        canDeploy = !readOnly && !token.isNullOrBlank(),
                        isUploading = isUploading,
                        isSyncing = isSyncing,
                        pendingCount = pendingCount,
                        readOnly = readOnly,
                        updatesBySerial = updatesBySerial,
                        token = token,
                        actionErrorUi = actionErrorUi,
                        onRetryLastAction = {
                            when (lastFailedAction) {
                                FailedAction.Deploy -> startDeploy(displaySet.setCode)
                                FailedAction.Upload -> viewModel.uploadChanges(displaySet)
                                null -> Unit
                            }
                        },
                        deployed = deployed
                    )
                }
            }

            FullScreenLoadingOverlay(
                visible = isUploading || isDeploying,
                message = when {
                    isDeploying -> "Deploying..."
                    isUploading -> "Uploading..."
                    else -> ""
                }
            )
        }
    }
}

fun handlePdfGeneration(
    context: Context, 
    set: DispatchSet, 
    dispatcherView: SignatureView?, 
    requesterView: SignatureView?
) {
    val hasDispatcherSignature = dispatcherView?.hasSignature == true
    val hasRequesterSignature = requesterView?.hasSignature == true

    if (!hasDispatcherSignature && !hasRequesterSignature) {
        Toast.makeText(context, "Please capture at least one signature.", Toast.LENGTH_LONG).show()
        return
    }

    val dispatcherBitmap = if (hasDispatcherSignature) dispatcherView?.getSignatureBitmap() else null
    val requesterBitmap = if (hasRequesterSignature) requesterView?.getSignatureBitmap() else null
    
    generateAndOpenPdf(context, set, dispatcherBitmap, requesterBitmap)
}

@Composable
fun DispatchDetailsContent(
    dispatchSet: DispatchSet,
    itemEdits: List<ItemEditState>,
    onItemEditChange: (ItemEditState) -> Unit,
    onOpenItemDetails: (DispatchItem) -> Unit,
    modifier: Modifier = Modifier,
    registerDispatcherSignatureView: (SignatureView) -> Unit,
    registerRequesterSignatureView: (SignatureView) -> Unit,
    onGeneratePdf: (DispatchSet) -> Unit,
    onUpload: () -> Unit,
    onViewUploads: () -> Unit,
    onDeploy: () -> Unit,
    onSyncNow: () -> Unit,
    canDeploy: Boolean,
    isUploading: Boolean,
    isSyncing: Boolean,
    pendingCount: Int,
    readOnly: Boolean,
    updatesBySerial: Map<String, SetItemUpdateDto>,
    token: String?,
    actionErrorUi: Pair<String, String>?,
    onRetryLastAction: () -> Unit,
    deployed: Boolean = false
) {
    var showRequesterSignatureDialog by remember { mutableStateOf(false) }
    var showDispatcherSignatureDialog by remember { mutableStateOf(false) }
    var activeIssueEditState by remember { mutableStateOf<ItemEditState?>(null) }
    var isSignaturesExpanded by remember { mutableStateOf(false) }
    var isFieldActionsExpanded by remember { mutableStateOf(false) }
    
    // Image upload state
    var selectedImageBitmap by remember { mutableStateOf<Bitmap?>(null) }
    var isUploadingImage by remember { mutableStateOf(false) }
    var showImagePreview by remember { mutableStateOf(false) }
    var showImageSourceDialog by remember { mutableStateOf(false) }
    var tempCameraUri by remember { mutableStateOf<android.net.Uri?>(null) }
    var isAttachPhotoExpanded by remember { mutableStateOf(false) } // Toggle for collapsible section
    var showViewImagesDialog by remember { mutableStateOf(false) } // Toggle for view images dialog
    
    // Server-side image count (fetched when screen loads)
    var serverImageCount by remember { mutableStateOf<Int?>(null) }
    var isLoadingImageCount by remember { mutableStateOf(false) }
    
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    // Fetch image count from server when screen loads
    LaunchedEffect(token, dispatchSet.setCode) {
        scope.launch {
            isLoadingImageCount = true
            android.util.Log.d("DispatchDetails", "Fetching images for token=$token, setCode=${dispatchSet.setCode}")
            when (val result = fetchSetImages(token, dispatchSet.setCode ?: "")) {
                is ApiResult.Success -> {
                    serverImageCount = result.data.size
                    android.util.Log.d("DispatchDetails", "Server returned ${result.data.size} images")
                }
                is ApiResult.HttpError -> {
                    android.util.Log.e("DispatchDetails", "HTTP error ${result.code}: ${result.message}")
                    serverImageCount = null
                }
                is ApiResult.NetworkError -> {
                    android.util.Log.e("DispatchDetails", "Network error: ${result.message}")
                    serverImageCount = null
                }
                is ApiResult.UnknownError -> {
                    android.util.Log.e("DispatchDetails", "Unknown error: ${result.message}")
                    serverImageCount = null
                }
            }
            isLoadingImageCount = false
        }
    }

    // Camera launcher
    val cameraLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.TakePicture()
    ) { success ->
        if (success) {
            tempCameraUri?.let { uri ->
                val bitmap = try {
                    context.contentResolver.openInputStream(uri)?.use { stream ->
                        BitmapFactory.decodeStream(stream)
                    }
                } catch (e: Exception) { null }
                selectedImageBitmap = bitmap
                showImagePreview = bitmap != null
            }
        }
        tempCameraUri = null
    }

    // Photo picker launcher
    val photoPicker = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.PickVisualMedia()
    ) { uri ->
        uri?.let {
            val bitmap = try {
                context.contentResolver.openInputStream(uri)?.use { stream ->
                    BitmapFactory.decodeStream(stream)
                }
            } catch (e: Exception) { null }
            selectedImageBitmap = bitmap
            showImagePreview = bitmap != null
        }
    }

    // ML Kit Document Scanner — CamScanner-style multi-page capture with auto edge-detect/
    // crop/perspective-correction. Lets one continuous scanning session queue up several
    // pages (e.g. multiple angles of a Set) for a single batch upload, instead of repeating
    // the take-picture-then-confirm loop per photo.
    var scannedPages by remember { mutableStateOf<List<Bitmap>>(emptyList()) }
    var isBulkUploading by remember { mutableStateOf(false) }

    val documentScannerOptions = remember {
        GmsDocumentScannerOptions.Builder()
            .setGalleryImportAllowed(false)
            .setPageLimit(20)
            .setResultFormats(GmsDocumentScannerOptions.RESULT_FORMAT_JPEG)
            .setScannerMode(GmsDocumentScannerOptions.SCANNER_MODE_FULL)
            .build()
    }

    val documentScanLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartIntentSenderForResult()
    ) { activityResult ->
        if (activityResult.resultCode == android.app.Activity.RESULT_OK) {
            val scanResult = GmsDocumentScanningResult.fromActivityResultIntent(activityResult.data)
            val pageUris = scanResult?.pages.orEmpty().map { it.imageUri }
            // Decoding full-resolution scanned pages is real work -- doing it synchronously on
            // this ActivityResultCallback (which runs on the main thread) froze the UI for
            // multi-page/full-res scans. Moved to Dispatchers.IO.
            scope.launch {
                val bitmaps = withContext(Dispatchers.IO) {
                    pageUris.mapNotNull { uri ->
                        try {
                            context.contentResolver.openInputStream(uri)?.use { stream ->
                                BitmapFactory.decodeStream(stream)
                            }
                        } catch (e: Exception) { null }
                    }
                }
                if (bitmaps.isNotEmpty()) {
                    scannedPages = bitmaps
                    selectedImageBitmap = null
                    showImagePreview = false
                } else {
                    Toast.makeText(context, "No pages captured.", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    fun launchDocumentScanner() {
        val activity = context as? android.app.Activity
        if (activity == null) {
            Toast.makeText(context, "Unable to start scanner.", Toast.LENGTH_SHORT).show()
            return
        }
        GmsDocumentScanning.getClient(documentScannerOptions)
            .getStartScanIntent(activity)
            .addOnSuccessListener { intentSender ->
                documentScanLauncher.launch(IntentSenderRequest.Builder(intentSender).build())
            }
            .addOnFailureListener { e ->
                Toast.makeText(context, "Unable to start scanner: ${e.message}", Toast.LENGTH_LONG).show()
            }
    }

    fun uploadScannedPages(setCode: String) {
        if (scannedPages.isEmpty()) return
        scope.launch {
            val uploadToken = token?.trim().orEmpty()
            val effectiveToken = uploadToken.takeIf { it.isNotBlank() }
            val effectiveSetCode = setCode.takeIf { it.isNotBlank() && effectiveToken == null }

            if (effectiveToken == null && effectiveSetCode == null) {
                Toast.makeText(context, "Set code is required for upload.", Toast.LENGTH_LONG).show()
                return@launch
            }

            isBulkUploading = true
            val currentUser = UserSession.currentUser?.displayName ?: "Mobile User"
            var successCount = 0
            var failCount = 0

            // Bitmap compression is real CPU work -- scope.launch runs on the Main dispatcher by
            // default, so without this the compress() calls below would block the UI thread.
            withContext(Dispatchers.IO) {
                for (bitmap in scannedPages) {
                    try {
                        val stream = ByteArrayOutputStream()
                        bitmap.compress(Bitmap.CompressFormat.JPEG, 85, stream)
                        val base64Image = Base64.encodeToString(stream.toByteArray(), Base64.NO_WRAP)
                        val request = SetImageUploadRequest(
                            imageBase64 = base64Image,
                            imageType = "MobileUpload",
                            uploadedBy = currentUser
                        )
                        when (val result = safeApiCall { uploadSetImage(effectiveToken, effectiveSetCode, request) }) {
                            is ApiResult.Success -> if (result.data.success) successCount++ else failCount++
                            else -> failCount++
                        }
                    } catch (e: Exception) {
                        failCount++
                    }
                }
            }

            isBulkUploading = false
            scannedPages = emptyList()
            Toast.makeText(
                context,
                if (failCount == 0) "Uploaded $successCount page(s)." else "Uploaded $successCount page(s), $failCount failed.",
                Toast.LENGTH_LONG
            ).show()
        }
    }

    fun uploadSelectedImage(setCode: String) {
        scope.launch {
            val bitmap = selectedImageBitmap ?: return@launch
            val uploadToken = token?.trim().orEmpty()
            
            // Use token if available, otherwise use set_code
            val effectiveToken = uploadToken.takeIf { it.isNotBlank() }
            val effectiveSetCode = setCode.takeIf { it.isNotBlank() && effectiveToken == null }
            
            if (effectiveToken == null && effectiveSetCode == null) {
                Toast.makeText(context, "Set code is required for upload.", Toast.LENGTH_LONG).show()
                return@launch
            }

            isUploadingImage = true
            try {
                // Convert bitmap to base64
                val stream = ByteArrayOutputStream()
                bitmap.compress(Bitmap.CompressFormat.JPEG, 85, stream)
                val imageBytes = stream.toByteArray()
                val base64Image = Base64.encodeToString(imageBytes, Base64.NO_WRAP)
                
                val currentUser = UserSession.currentUser?.displayName ?: "Mobile User"
                val request = SetImageUploadRequest(
                    imageBase64 = base64Image,
                    imageType = "MobileUpload",
                    uploadedBy = currentUser
                )

                when (val result = safeApiCall { uploadSetImage(effectiveToken, effectiveSetCode, request) }) {
                    is ApiResult.Success -> {
                        if (result.data.success) {
                            Toast.makeText(context, context.getString(R.string.image_upload_success), Toast.LENGTH_LONG).show()
                            selectedImageBitmap = null
                            showImagePreview = false
                        } else {
                            Toast.makeText(context, context.getString(R.string.image_upload_failed) + ": ${result.data.message}", Toast.LENGTH_LONG).show()
                        }
                    }
                    is ApiResult.HttpError -> {
                        // Map HTTP status codes to upload-specific messages so
                        // scanner-related server errors (e.g. "invalid QR code") don't
                        // bleed through and confuse the user during an image upload.
                        val msg = when (result.code) {
                            400 -> "Upload failed: the set could not be identified. Try scanning the QR code again before uploading."
                            404 -> "Upload failed: the dispatch set was not found on the server."
                            500, 502, 503, 504 -> "Upload failed: the server encountered an error. Please try again."
                            else -> "Image upload failed (code ${result.code}). Please try again."
                        }
                        Toast.makeText(context, msg, Toast.LENGTH_LONG).show()
                    }
                    is ApiResult.NetworkError -> {
                        Toast.makeText(context, "Network error. Please check your connection.", Toast.LENGTH_LONG).show()
                    }
                    is ApiResult.UnknownError -> {
                        Toast.makeText(context, "An unexpected error occurred.", Toast.LENGTH_LONG).show()
                    }
                }
            } catch (e: Exception) {
                Toast.makeText(context, "Error preparing image: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                isUploadingImage = false
            }
        }
    }

    // Image Source Selection Dialog
    if (showImageSourceDialog) {
        AlertDialog(
            onDismissRequest = { showImageSourceDialog = false },
            title = { Text(stringResource(id = R.string.select_image_source_title)) },
            text = {
                Column(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(
                        onClick = {
                            showImageSourceDialog = false
                            launchDocumentScanner()
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(
                            imageVector = Icons.Default.PhotoCamera,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(stringResource(id = R.string.scan_document_button))
                    }

                    OutlinedButton(
                        onClick = {
                            showImageSourceDialog = false
                            // Create temp file URI for camera
                            val tempFile = java.io.File.createTempFile("camera_", ".jpg", context.cacheDir)
                            val uri = androidx.core.content.FileProvider.getUriForFile(
                                context,
                                "${context.packageName}.fileprovider",
                                tempFile
                            )
                            tempCameraUri = uri
                            cameraLauncher.launch(uri)
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(
                            imageVector = Icons.Default.PhotoCamera,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(stringResource(id = R.string.take_picture_button))
                    }
                    
                    OutlinedButton(
                        onClick = {
                            showImageSourceDialog = false
                            photoPicker.launch(
                                PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)
                            )
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(
                            imageVector = Icons.Default.PhotoLibrary,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(stringResource(id = R.string.select_photo_button))
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { showImageSourceDialog = false }) {
                    Text(stringResource(id = R.string.cancel))
                }
            }
        )
    }

    if (showRequesterSignatureDialog) {
        SignatureDialog(
            label = stringResource(id = R.string.requester_sig_label),
            onDismiss = { showRequesterSignatureDialog = false },
            registerSignatureView = registerRequesterSignatureView
        )
    }

    if (showDispatcherSignatureDialog) {
        SignatureDialog(
            label = stringResource(id = R.string.handler_sig_label),
            onDismiss = { showDispatcherSignatureDialog = false },
            registerSignatureView = registerDispatcherSignatureView
        )
    }

    if (!readOnly && activeIssueEditState != null) {
        ReportIssueDialog(
            editState = activeIssueEditState!!,
            onDismiss = { activeIssueEditState = null },
            onSave = { updated ->
                onItemEditChange(updated)
                activeIssueEditState = null
            }
        )
    }

    // View Images Dialog
    if (showViewImagesDialog) {
        ViewImagesDialog(
            token = token,
            setCode = dispatchSet.setCode ?: "",
            onDismiss = { showViewImagesDialog = false }
        )
    }

    val hasPendingEdits = !readOnly && itemEdits.any { edit ->
        val serialKey = normalizeSerial(edit.base.serialNumber)
        val hasUploadedRecord = serialKey.isNotEmpty() && updatesBySerial[serialKey] != null
        !hasUploadedRecord && (edit.remarkInput.isNotBlank() || edit.statusInput != (edit.base.status ?: ""))
    }

    val allItems = dispatchSet.items ?: emptyList()
    val (activeItems, sparedItems) = allItems.partition { item ->
        val serialKey = normalizeSerial(item.serialNumber)
        val processedUpdate = if (serialKey.isNotEmpty()) updatesBySerial[serialKey] else null
        val isSparedToInventory = processedUpdate?.processed == true && isSpareToInventoryAction(processedUpdate.lastRepairAction)
        !isSparedToInventory
    }
    val hasEditableItems = !readOnly && activeItems.any { item ->
        val serialKey = normalizeSerial(item.serialNumber)
        serialKey.isEmpty() || updatesBySerial[serialKey] == null
    }

    Box(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(190.dp)
                .background(
                    Brush.verticalGradient(
                        colors = listOf(Color(0xFF5F201F), Color(0xFF8C2F2C), Color(0xFFC68182))
                    )
                )
        )

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 16.dp),
            contentPadding = PaddingValues(top = 28.dp, bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
        if (actionErrorUi != null) {
            item {
                ApiFailurePanel(
                    message = actionErrorUi.first,
                    details = actionErrorUi.second,
                    retryLabel = "Retry",
                    onRetry = onRetryLastAction
                )
            }
        }

        item {
            DispatchSummaryHero(
                set = dispatchSet,
                activeItemCount = activeItems.size,
                sparedItemCount = sparedItems.size,
                pendingCount = pendingCount,
                readOnly = readOnly,
                deployed = deployed
            )
        }

        item {
            SetInformationCard(dispatchSet)
        }

        if (hasPendingEdits) {
            item {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    color = MaterialTheme.colorScheme.tertiaryContainer.copy(alpha = 0.55f),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.tertiary.copy(alpha = 0.24f))
                ) {
                    Text(
                        text = stringResource(id = R.string.pending_edits_label),
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 10.dp),
                        color = MaterialTheme.colorScheme.onTertiaryContainer,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }
        }

        item {
            DispatchDetailsSectionHeader(
                title = "Items in this set (${activeItems.size})",
                subtitle = if (hasPendingEdits) "Item updates are waiting to upload" else "Tap an item to view its inventory details",
                expanded = true,
                onToggle = {},
                collapsible = false
            )
        }

        if (hasEditableItems) {
            item {
                ReportIssueHintBanner()
            }
        }

        items(activeItems) { item ->
            val editState = itemEdits.firstOrNull { it.id == item.id } ?: ItemEditState(
                id = item.id,
                base = item,
                statusInput = item.status ?: "",
                remarkInput = "",
                isEditing = false
            )
            val serialKey = normalizeSerial(item.serialNumber)
            val processedUpdate = if (serialKey.isNotEmpty()) {
                updatesBySerial[serialKey]
            } else {
                null
            }
            val hasUploadedRecord = processedUpdate != null
            ItemCard(
                item = item,
                editState = editState,
                onReportIssueClick = { selected -> activeIssueEditState = selected },
                onOpenDetails = if (item.serialNumber.isNullOrBlank()) null else { { onOpenItemDetails(item) } },
                readOnly = readOnly || hasUploadedRecord,
                processedUpdate = processedUpdate
            )
        }

        if (sparedItems.isNotEmpty()) {
            item {
                Text(
                    text = "Spared to inventory",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold
                )
            }

            items(sparedItems) { item ->
                val editState = itemEdits.firstOrNull { it.id == item.id } ?: ItemEditState(
                    id = item.id,
                    base = item,
                    statusInput = item.status ?: "",
                    remarkInput = "",
                    isEditing = false
                )
                val serialKey = normalizeSerial(item.serialNumber)
                val processedUpdate = if (serialKey.isNotEmpty()) {
                    updatesBySerial[serialKey]
                } else {
                    null
                }
                ItemCard(
                    item = item,
                    editState = editState,
                    onReportIssueClick = { },
                    onOpenDetails = if (item.serialNumber.isNullOrBlank()) null else { { onOpenItemDetails(item) } },
                    readOnly = true,
                    processedUpdate = processedUpdate
                )
            }
        }

        item {
            DispatchDetailsSectionHeader(
                title = "Signatures",
                subtitle = "Requester and handler acknowledgement",
                expanded = isSignaturesExpanded,
                onToggle = { isSignaturesExpanded = !isSignaturesExpanded }
            )
        }

        if (isSignaturesExpanded) {
            item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedButton(
                    onClick = { showRequesterSignatureDialog = true },
                    modifier = Modifier
                        .weight(1f)
                        .heightIn(min = 56.dp),
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                ) {
                    Text(
                        text = stringResource(id = R.string.requester_sig_label),
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }

                OutlinedButton(
                    onClick = { showDispatcherSignatureDialog = true },
                    modifier = Modifier
                        .weight(1f)
                        .heightIn(min = 56.dp),
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                ) {
                    Text(
                        text = stringResource(id = R.string.handler_sig_label),
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }
            }
        }

        }

        // Image Upload Section - Collapsible
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(20.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f))
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    // Toggle header row
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = stringResource(id = R.string.attach_photo_title),
                            fontWeight = FontWeight.Medium,
                            fontSize = 16.sp,
                            color = MaterialTheme.colorScheme.primary
                        )
                        
                        // Toggle button for attach photo section
                        TextButton(
                            onClick = { isAttachPhotoExpanded = !isAttachPhotoExpanded }
                        ) {
                            Icon(
                                imageVector = if (isAttachPhotoExpanded) Icons.Default.KeyboardArrowUp else Icons.Default.KeyboardArrowDown,
                                contentDescription = if (isAttachPhotoExpanded) "Collapse" else "Expand",
                                modifier = Modifier.size(20.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = if (isAttachPhotoExpanded) "Hide" else "Show",
                                fontSize = 12.sp
                            )
                        }
                    }
                    
                    // Collapsible content
                    AnimatedVisibility(
                        visible = isAttachPhotoExpanded,
                        enter = fadeIn() + expandVertically(),
                        exit = fadeOut() + shrinkVertically()
                    ) {
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            if (scannedPages.isNotEmpty()) {
                        // Show scanned-page thumbnails (multi-page batch from the document scanner)
                        Text(
                            text = "${scannedPages.size} page(s) scanned",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        LazyRow(
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            items(scannedPages) { page ->
                                Image(
                                    bitmap = page.asImageBitmap(),
                                    contentDescription = "Scanned page",
                                    modifier = Modifier
                                        .size(100.dp)
                                        .clip(RoundedCornerShape(8.dp)),
                                    contentScale = ContentScale.Crop
                                )
                            }
                        }

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            OutlinedButton(
                                onClick = { scannedPages = emptyList() },
                                modifier = Modifier.weight(1f),
                                enabled = !isBulkUploading
                            ) {
                                Text(stringResource(id = R.string.cancel))
                            }

                            Button(
                                onClick = { dispatchSet?.setCode?.let { uploadScannedPages(it) } },
                                modifier = Modifier.weight(1f),
                                enabled = !isBulkUploading
                            ) {
                                if (isBulkUploading) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(20.dp),
                                        strokeWidth = 2.dp,
                                        color = MaterialTheme.colorScheme.onPrimary
                                    )
                                } else {
                                    Text("Upload All (${scannedPages.size})")
                                }
                            }
                        }
                    } else if (selectedImageBitmap != null && showImagePreview) {
                        // Show selected image preview
                        Image(
                            bitmap = selectedImageBitmap!!.asImageBitmap(),
                            contentDescription = "Selected image",
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(200.dp)
                                .clip(RoundedCornerShape(8.dp)),
                            contentScale = ContentScale.Crop
                        )
                        
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            OutlinedButton(
                                onClick = { 
                                    selectedImageBitmap = null
                                    showImagePreview = false
                                },
                                modifier = Modifier.weight(1f)
                            ) {
                                Text(stringResource(id = R.string.cancel))
                            }
                            
                            Button(
                                onClick = { dispatchSet?.setCode?.let { uploadSelectedImage(it) } },
                                modifier = Modifier.weight(1f),
                                enabled = !isUploadingImage
                            ) {
                                if (isUploadingImage) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(20.dp),
                                        strokeWidth = 2.dp,
                                        color = MaterialTheme.colorScheme.onPrimary
                                    )
                                } else {
                                    Text(stringResource(id = R.string.upload_image_button))
                                }
                            }
                        }
                    } else {
                        // Show select photo button
                        OutlinedButton(
                            onClick = { 
                                showImageSourceDialog = true
                            },
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Icon(
                                imageVector = Icons.Default.PhotoLibrary,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp)
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(stringResource(id = R.string.add_photo_button))
                        }
                        
                        Text(
                            text = stringResource(id = R.string.upload_photo_hint),
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(top = 4.dp)
                        )
                    }
                        }
                    }

                    // View Images Button moved inside the Attach Photo card
                    val effectiveImageCount = serverImageCount ?: dispatchSet.imageCount ?: 0
                    val hasSetCode = !dispatchSet.setCode.isNullOrBlank()
                    if (isAttachPhotoExpanded && hasSetCode) {
                        OutlinedButton(
                            onClick = { showViewImagesDialog = true },
                            modifier = Modifier.fillMaxWidth(),
                            colors = ButtonDefaults.outlinedButtonColors(
                                contentColor = MaterialTheme.colorScheme.primary
                            )
                        ) {
                            Icon(
                                imageVector = Icons.Default.Image,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp)
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(if (effectiveImageCount > 0) "View Images ($effectiveImageCount)" else "View Images")
                        }
                    }
                }
            }
        }

        item {
            DispatchDetailsSectionHeader(
                title = "Field actions",
                subtitle = when {
                    deployed -> "This set has been deployed"
                    hasPendingEdits -> "Item changes are ready to upload"
                    pendingCount > 0 -> "$pendingCount update(s) are waiting to sync"
                    else -> "Deploy, upload changes, or create a PDF copy"
                },
                expanded = isFieldActionsExpanded,
                onToggle = { isFieldActionsExpanded = !isFieldActionsExpanded }
            )
        }

        if (isFieldActionsExpanded) {
            item {
                Footer(
                onGeneratePdf = { onGeneratePdf(dispatchSet) },
                onUpload = onUpload,
                onViewUploads = onViewUploads,
                onDeploy = onDeploy,
                onSyncNow = onSyncNow,
                canUpload = hasPendingEdits,
                canViewUploads = !dispatchSet.setCode.isNullOrBlank(),
                canDeploy = canDeploy,
                canSync = pendingCount > 0,
                isUploading = isUploading,
                isSyncing = isSyncing,
                pendingCount = pendingCount,
                deployed = deployed
            )
            }
        }
        }
    }
}

@Composable
private fun DispatchDetailsSectionHeader(
    title: String,
    subtitle: String,
    expanded: Boolean,
    onToggle: () -> Unit,
    collapsible: Boolean = true
) {
    val accent = Color(0xFF8C2F2C)
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .then(if (collapsible) Modifier.clickable(onClick = onToggle) else Modifier),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 15.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Surface(
                modifier = Modifier.size(36.dp),
                shape = RoundedCornerShape(11.dp),
                color = accent.copy(alpha = 0.12f)
            ) {
                Icon(
                    imageVector = Icons.Filled.CheckCircle,
                    contentDescription = null,
                    modifier = Modifier.padding(9.dp),
                    tint = accent
                )
            }
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.ExtraBold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            if (collapsible) {
                Surface(
                    modifier = Modifier.size(32.dp),
                    shape = CircleShape,
                    color = accent.copy(alpha = 0.10f)
                ) {
                    Icon(
                        imageVector = if (expanded) Icons.Filled.KeyboardArrowUp else Icons.Filled.KeyboardArrowDown,
                        contentDescription = if (expanded) "Collapse $title" else "Expand $title",
                        modifier = Modifier.padding(7.dp),
                        tint = accent
                    )
                }
            }
        }
    }
}

@Composable
private fun DispatchSummaryHero(
    set: DispatchSet,
    activeItemCount: Int,
    sparedItemCount: Int,
    pendingCount: Int,
    readOnly: Boolean,
    deployed: Boolean
) {
    val statusText = if (deployed) "DISPATCHED" else (set.status ?: "UNKNOWN").uppercase()
    val statusColor = dispatchStatusColor(statusText)
    val recipient = set.employee.displayOr(
        if (set.department.isNullOrBlank() && set.branch.isNullOrBlank()) "Unassigned recipient" else "Department request"
    )

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(26.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        border = BorderStroke(1.dp, Color.White.copy(alpha = 0.35f))
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.Top
            ) {
                Surface(
                    modifier = Modifier.size(48.dp),
                    shape = RoundedCornerShape(16.dp),
                    color = Color(0xFF8C2F2C).copy(alpha = 0.12f)
                ) {
                    Icon(
                        imageVector = Icons.Filled.CheckCircle,
                        contentDescription = null,
                        modifier = Modifier.padding(12.dp),
                        tint = Color(0xFF8C2F2C)
                    )
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(3.dp)
                ) {
                    Text(
                        text = "DISPATCH SET",
                        style = MaterialTheme.typography.labelSmall.copy(
                            fontWeight = FontWeight.ExtraBold,
                            letterSpacing = 0.8.sp
                        ),
                        color = Color(0xFF8C2F2C)
                    )
                    Text(
                        text = set.setCode ?: "No set code",
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.ExtraBold),
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        text = recipient,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                StatusPill(
                    label = statusText,
                    icon = if (statusText == "DISPATCHED") Icons.Filled.CheckCircle else null,
                    containerColor = statusColor.copy(alpha = 0.12f),
                    borderColor = statusColor.copy(alpha = 0.34f),
                    contentColor = statusColor
                )
            }

            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                DispatchMetricChip("Items", activeItemCount.toString(), Modifier.weight(1f))
                DispatchMetricChip("Spared", sparedItemCount.toString(), Modifier.weight(1f))
                DispatchMetricChip(
                    if (readOnly) "Mode" else "Pending",
                    if (readOnly) "View" else pendingCount.toString(),
                    Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun DispatchMetricChip(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        color = Color(0xFF8C2F2C).copy(alpha = 0.075f),
        border = BorderStroke(1.dp, Color(0xFF8C2F2C).copy(alpha = 0.16f))
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = value,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.ExtraBold,
                color = Color(0xFF8C2F2C)
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1
            )
        }
    }
}
private fun dispatchStatusColor(status: String?): Color = when (status?.uppercase()) {
    "DISPATCHED" -> Color(0xFF2E7D32)
    "PENDING" -> Color(0xFFE65100)
    "ACTIVE" -> Color(0xFFA36A72)
    else -> Color(0xFF546E7A)
}

@Composable
fun SetInformationCard(set: DispatchSet) {
    val statusColor = dispatchStatusColor(set.status)
    val department = set.department.displayOr("Not assigned")
    val branch = set.branch.displayOr("Not assigned")
    val employee = set.employee.displayOr(
        if (set.department.isNullOrBlank() && set.branch.isNullOrBlank()) "Not assigned" else "Department request"
    )
    val createdBy = set.createdBy.displayOr("Not recorded")
    val released = set.dispatchDate.displayOr("Pending release")
    val created = set.createdDate.displayOr("Not recorded")

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.35f))
    ) {
        Column(modifier = Modifier.fillMaxWidth()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(4.dp)
                    .background(
                        Brush.horizontalGradient(
                            colors = listOf(statusColor, statusColor.copy(alpha = 0.3f))
                        )
                    )
            )
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 20.dp, vertical = 18.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(3.dp), modifier = Modifier.weight(1f)) {
                        Text(
                            text = "DISPATCH CONTEXT",
                            style = MaterialTheme.typography.labelSmall.copy(
                                fontWeight = FontWeight.ExtraBold,
                                letterSpacing = 0.8.sp
                            ),
                            color = Color(0xFF8C2F2C)
                        )
                        Text(
                            text = "Recipient, branch, release, and mobile user",
                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.ExtraBold),
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                    Surface(
                        color = statusColor.copy(alpha = 0.10f),
                        shape = RoundedCornerShape(50),
                        border = BorderStroke(1.dp, statusColor.copy(alpha = 0.35f))
                    ) {
                        Text(
                            text = (set.status ?: "N/A").uppercase(),
                            modifier = Modifier.padding(horizontal = 14.dp, vertical = 6.dp),
                            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                            color = statusColor
                        )
                    }
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(20.dp)
                ) {
                    Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                        InfoField(stringResource(id = R.string.department_label), department)
                        InfoField(stringResource(id = R.string.created_by_label), createdBy)
                        val currentUser = UserSession.currentUser
                        InfoField(stringResource(id = R.string.mobile_user_label), currentUser?.displayName.displayOr("Not signed in"))
                    }
                    Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                        InfoField(stringResource(id = R.string.employee_label), employee)
                        InfoField(stringResource(id = R.string.branch_label), branch)
                        InfoField(stringResource(id = R.string.released_label), released)
                        InfoField(stringResource(id = R.string.created_label), created)
                    }
                }
            }
        }
    }
}

private fun String?.displayOr(fallback: String): String =
    this?.trim()?.takeIf { it.isNotEmpty() } ?: fallback

@Composable
fun InfoRow(label: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier = modifier) {
        Text(text = label, color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 14.sp)
        Spacer(modifier = Modifier.width(4.dp))
        Text(text = value, color = MaterialTheme.colorScheme.onSurface, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun InfoField(label: String, value: String) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(
            text = label.uppercase(Locale.US),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.75f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)

@Composable
fun ItemCard(
    item: DispatchItem,
    editState: ItemEditState,
    onReportIssueClick: (ItemEditState) -> Unit,
    onOpenDetails: (() -> Unit)? = null,
    readOnly: Boolean = false,
    processedUpdate: SetItemUpdateDto? = null
) {
    val accentColor = when (editState.statusInput.uppercase()) {
        "GOOD", "WORKING" -> Color(0xFF2E7D32)
        "DEFECTIVE", "BROKEN", "DAMAGED" -> Color(0xFFC62828)
        "FOR REPAIR" -> Color(0xFFE65100)
        "RETURNED", "RETURNED TO STOCK" -> Color(0xFFA36A72)
        else -> MaterialTheme.colorScheme.primary
    }

    val readOnlyCardModifier = if (onOpenDetails != null) {
        Modifier.fillMaxWidth().clickable(onClick = onOpenDetails)
    } else {
        Modifier.fillMaxWidth()
    }
    val editableCardModifier = Modifier.fillMaxWidth()

    if (readOnly) {
        Card(
            modifier = readOnlyCardModifier,
            shape = RoundedCornerShape(22.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
        ) {
            ItemCardContent(
                item = item,
                editState = editState,
                processedUpdate = processedUpdate,
                showReportIssueHint = false
            )
        }
    } else {
        val dismissState = androidx.compose.material3.rememberSwipeToDismissBoxState(
            confirmValueChange = { value ->
                if (value == androidx.compose.material3.SwipeToDismissBoxValue.EndToStart) {
                    onReportIssueClick(editState)
                }
                false // Don't actually dismiss visually
            }
        )

        androidx.compose.material3.SwipeToDismissBox(
            state = dismissState,
            enableDismissFromStartToEnd = false,
            backgroundContent = {
                val color = MaterialTheme.colorScheme.errorContainer
                val iconColor = MaterialTheme.colorScheme.error
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(color = color, shape = RoundedCornerShape(16.dp))
                        .padding(horizontal = 24.dp),
                    contentAlignment = Alignment.CenterEnd
                ) {
                    Icon(
                        imageVector = Icons.Filled.Warning,
                        contentDescription = stringResource(id = R.string.report_issue_button),
                        tint = iconColor,
                        modifier = Modifier.size(28.dp)
                    )
                }
            },
            content = {
                Card(
                    modifier = editableCardModifier,
                    shape = RoundedCornerShape(22.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
                ) {
                    ItemCardContent(
                        item = item,
                        editState = editState,
                        processedUpdate = processedUpdate,
                        showReportIssueHint = true,
                        onOpenDetails = onOpenDetails
                    )
                }
            }
        )
    }
}

@Composable
private fun ItemCardContent(
    item: DispatchItem,
    editState: ItemEditState,
    processedUpdate: SetItemUpdateDto?,
    showReportIssueHint: Boolean,
    onOpenDetails: (() -> Unit)? = null
) {

    val repairCount = processedUpdate?.repairCount ?: 0
    val isMarkedForProcessing = processedUpdate != null && !processedUpdate.processed
    val isProcessed = processedUpdate?.processed == true
    val dispositionLabel = normalizeRepairActionLabel(processedUpdate?.lastRepairAction)
    val isSparedToInventory = isProcessed && isSpareToInventoryAction(processedUpdate?.lastRepairAction)
    Column(modifier = Modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.Top
        ) {
            Row(
                modifier = Modifier.weight(1f),
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (repairCount > 0) {
                    Text(
                        text = "R",
                        color = MaterialTheme.colorScheme.primary,
                        fontWeight = FontWeight.ExtraBold,
                        fontSize = 12.sp,
                        modifier = Modifier
                            .border(width = 1.dp, color = MaterialTheme.colorScheme.primary, shape = RoundedCornerShape(8.dp))
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                }
                Column(modifier = Modifier.weight(1f, fill = false), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    val itemName = item.description?.trim()?.takeIf { it.isNotEmpty() }
                        ?: item.type?.trim()?.takeIf { it.isNotEmpty() }
                        ?: "Unknown Item"
                    val itemType = item.type?.trim()?.takeIf {
                        it.isNotEmpty() && !it.equals(itemName, ignoreCase = true)
                    }
                    Text(
                        text = itemName,
                        fontWeight = FontWeight.Bold,
                        fontSize = 16.sp,
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                    itemType?.let {
                        Text(
                            text = it,
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                }
            }
            Column(
                modifier = Modifier.widthIn(max = 116.dp),
                horizontalAlignment = Alignment.End,
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                if (!isSparedToInventory) {
                    ItemStatusChip(
                        status = editState.statusInput.ifBlank { "Unknown" },
                        modifier = Modifier.widthIn(max = 116.dp)
                    )
                }

                if (isMarkedForProcessing) {
                    StatusPill(
                        label = "Pending",
                        icon = Icons.Filled.Warning,
                        containerColor = MaterialTheme.colorScheme.errorContainer,
                        borderColor = MaterialTheme.colorScheme.error.copy(alpha = 0.4f),
                        contentColor = MaterialTheme.colorScheme.error
                    )
                } else if (isProcessed) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        StatusPill(
                            label = "Processed",
                            icon = Icons.Filled.CheckCircle,
                            containerColor = Color(0xFFE8F5E9),
                            borderColor = Color(0xFFA5D6A7),
                            contentColor = Color(0xFF2E7D32)
                        )

                        if (!dispositionLabel.isNullOrBlank()) {
                            StatusPill(
                                label = if (isSparedToInventory) "Spared" else "Returned",
                                icon = null,
                                containerColor = MaterialTheme.colorScheme.surfaceVariant,
                                borderColor = MaterialTheme.colorScheme.outlineVariant,
                                contentColor = MaterialTheme.colorScheme.onSurfaceVariant
                            )

                        }
                    }
                }
            }
        }
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            DispatchItemFact("SERIAL", item.serialNumber ?: "N/A", Modifier.weight(1f))
            DispatchItemFact("MODEL", item.modelNumber ?: "N/A", Modifier.weight(1f))
            if (item.quantity > 1) {
                DispatchItemFact("QTY", item.quantity.toString(), Modifier.weight(0.65f))
            }
        }
        val pcName = item.computerName?.trim().takeIf { !it.isNullOrEmpty() }
        val ipAddress = item.ipAddress?.trim().takeIf { !it.isNullOrEmpty() }
        if (pcName != null || ipAddress != null) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                DispatchItemFact(
                    label = stringResource(id = R.string.dispatch_item_pc_label),
                    value = pcName ?: "N/A",
                    modifier = Modifier.weight(1f)
                )
                DispatchItemFact(
                    label = stringResource(id = R.string.dispatch_item_ip_label),
                    value = ipAddress ?: "N/A",
                    modifier = Modifier.weight(1f)
                )
            }
        }

        if (!editState.remarkInput.isBlank()) {
            Text(
                text = "Remark: ${editState.remarkInput}",
                color = MaterialTheme.colorScheme.error,
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium
            )
        }

        if (editState.remarkInput.isBlank() && processedUpdate != null) {
            val serverRemark = processedUpdate.remark?.trim()
            if (!serverRemark.isNullOrBlank()) {
                Text(
                    text = "Reported: $serverRemark",
                    color = if (isMarkedForProcessing) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
                    fontSize = 13.sp,
                    fontWeight = if (isMarkedForProcessing) FontWeight.Medium else FontWeight.Normal
                )
            }

        }

        onOpenDetails?.let { openDetails ->
            TextButton(
                onClick = openDetails,
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.textButtonColors(contentColor = Color(0xFF8C2F2C))
            ) {
                Text(
                    text = "VIEW ITEM DETAILS",
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold)
                )
            }
        }

        if (showReportIssueHint) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                modifier = Modifier.padding(top = 4.dp)
            ) {
                Icon(
                    imageVector = Icons.Filled.Warning,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.error,
                    modifier = Modifier.size(16.dp)
                )
                Text(
                    text = stringResource(id = R.string.report_issue_swipe_card_hint),
                    color = MaterialTheme.colorScheme.error,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium
                )
            }
        }
    }
}

@Composable
private fun DispatchItemFact(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(14.dp),
        color = Color(0xFF8C2F2C).copy(alpha = 0.075f),
        border = BorderStroke(1.dp, Color(0xFF8C2F2C).copy(alpha = 0.14f))
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 9.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun ReportIssueHintBanner() {
    Surface(
        color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.45f),
        shape = RoundedCornerShape(14.dp),
        border = androidx.compose.foundation.BorderStroke(
            1.dp,
            MaterialTheme.colorScheme.error.copy(alpha = 0.18f)
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Icon(
                imageVector = Icons.Filled.Warning,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.error,
                modifier = Modifier.size(18.dp)
            )
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    text = stringResource(id = R.string.report_issue_button),
                    color = MaterialTheme.colorScheme.error,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = stringResource(id = R.string.report_issue_swipe_hint),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    fontSize = 12.sp
                )
            }
        }
    }
}

@Composable
private fun StatusPill(
    label: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector?,
    containerColor: Color,
    borderColor: Color,
    contentColor: Color
) {
    Surface(
        color = containerColor,
        shape = RoundedCornerShape(16.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, borderColor)
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp),
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
        ) {
            if (icon != null) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = contentColor,
                    modifier = Modifier.size(14.dp)
                )
            }
            Text(
                text = label,
                fontSize = 11.sp,
                fontWeight = FontWeight.Bold,
                color = contentColor,
                maxLines = 1,
                softWrap = false,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

private fun normalizeRepairActionLabel(raw: String?): String? {
    val action = raw?.trim().orEmpty()
    if (action.isBlank()) return null

    return when {
        action.equals("Repaired - Spare inventory", ignoreCase = true) -> "Spared to inventory"
        action.equals("Repaired - Returned to requester", ignoreCase = true) -> "Returned to requester"
        action.contains("spare", ignoreCase = true) && action.contains("inventory", ignoreCase = true) -> "Spared to inventory"
        action.contains("return", ignoreCase = true) && action.contains("requester", ignoreCase = true) -> "Returned to requester"
        else -> action
    }
}

private fun isSpareToInventoryAction(raw: String?): Boolean {
    val action = raw?.trim().orEmpty()
    if (action.isBlank()) return false
    return action.equals("Repaired - Spare inventory", ignoreCase = true) ||
        (action.contains("spare", ignoreCase = true) && action.contains("inventory", ignoreCase = true))
}

@Composable
private fun ViewImagesDialog(
    token: String?,
    setCode: String,
    onDismiss: () -> Unit
) {
    var isLoading by remember { mutableStateOf(true) }
    var images by remember { mutableStateOf<List<SetImageDto>>(emptyList()) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(token, setCode) {
        scope.launch {
            when (val result = fetchSetImages(token, setCode)) {
                is ApiResult.Success -> {
                    images = result.data
                    isLoading = false
                }
                is ApiResult.HttpError -> {
                    error = when (result.code) {
                        404 -> "This set was not found on the server."
                        500, 502, 503, 504 -> "The server encountered an error loading images (${result.code}). Please try again."
                        else -> "Could not load images (HTTP ${result.code})."
                    }
                    isLoading = false
                }
                is ApiResult.NetworkError -> {
                    error = "Network error. Please check your connection."
                    isLoading = false
                }
                is ApiResult.UnknownError -> {
                    error = "An unexpected error occurred. Please try again."
                    isLoading = false
                }
            }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Set Images") },
        text = {
            when {
                isLoading -> {
                    Box(
                        modifier = Modifier.fillMaxWidth().height(200.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        CircularProgressIndicator()
                    }
                }
                error != null -> {
                    Text("Error loading images: $error", color = MaterialTheme.colorScheme.error)
                }
                images.isEmpty() -> {
                    Text("No images found for this set.")
                }
                else -> {
                    LazyColumn(
                        modifier = Modifier.fillMaxWidth().height(400.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        items(images) { image ->
                            Card(
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(8.dp)
                            ) {
                                Column(
                                    modifier = Modifier.padding(12.dp),
                                    verticalArrangement = Arrangement.spacedBy(4.dp)
                                ) {
                                    val imageUrl = "${ApiSettings.apiBaseUrl}get-image.ashx?id=${image.imageId}"
                                    
                                    AsyncImage(
                                        model = ImageRequest.Builder(LocalContext.current)
                                            .data(imageUrl)
                                            .crossfade(true)
                                            .build(),
                                        contentDescription = "Set Image",
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .height(200.dp)
                                            .clip(RoundedCornerShape(8.dp)),
                                        contentScale = ContentScale.Crop
                                    )
                                    
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Text(
                                        text = "Type: ${image.imageType ?: "Unknown"}",
                                        fontWeight = FontWeight.Medium
                                    )
                                    Text(
                                        text = "By: ${image.uploadedBy ?: "Unknown"}",
                                        fontSize = 12.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                    Text(
                                        text = "Date: ${image.uploadDate?.let { formatDate(it) } ?: "Unknown"}",
                                        fontSize = 12.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("Close")
            }
        }
    )
}

private fun formatDate(dateString: String): String {
    return try {
        val inputFormat = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", java.util.Locale.getDefault())
        val outputFormat = java.text.SimpleDateFormat("MMM dd, yyyy HH:mm", java.util.Locale.getDefault())
        val date = inputFormat.parse(dateString)
        outputFormat.format(date)
    } catch (e: Exception) {
        dateString
    }
}

// API call wrapper to get set images using safeApiCall
private suspend fun fetchSetImages(token: String?, setCode: String?): ApiResult<List<SetImageDto>> {
    val result = safeApiCall { getSetImages(token, setCode) }
    return when (result) {
        is ApiResult.Success -> ApiResult.Success(result.data.images)
        is ApiResult.HttpError -> ApiResult.HttpError(result.code, result.message, result.endpoint)
        is ApiResult.NetworkError -> ApiResult.NetworkError(result.message, result.endpoint)
        is ApiResult.UnknownError -> ApiResult.UnknownError(result.message, result.endpoint)
    }
}