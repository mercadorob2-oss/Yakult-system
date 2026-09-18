package com.example.yakultscanner.ui.screens

import android.app.DatePickerDialog
import android.app.TimePickerDialog
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PersonSearch
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.BorrowBranchDto
import com.example.yakultscanner.api.BorrowCompanyDto
import com.example.yakultscanner.api.BorrowCreateRequest
import com.example.yakultscanner.api.BorrowDepartmentDto
import com.example.yakultscanner.api.BorrowEmployeeDto
import com.example.yakultscanner.api.BorrowEmployeeCreateRequest
import com.example.yakultscanner.api.BorrowItemDto
import com.example.yakultscanner.api.BorrowLogDto
import com.example.yakultscanner.api.BorrowResolveResponse
import com.example.yakultscanner.api.BorrowReturnRequest
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.ui.components.BatchDropdownField
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Calendar
import java.util.Locale
import java.util.UUID

private data class BorrowConfirmationPreview(
    val itemName: String,
    val itemDescription: String,
    val modelNumber: String,
    val serialNumber: String,
    val borrowerName: String,
    val companyName: String,
    val borrowerDepartment: String,
    val encodedBy: String,
    val timestampLabel: String,
    val borrowedAtLabel: String,
    val note: String? = null
)

private data class ReturnConfirmationPreview(
    val itemName: String,
    val itemDescription: String,
    val modelNumber: String,
    val serialNumber: String,
    val borrowedBy: String,
    val borrowedAtLabel: String,
    val returnedBy: String,
    val department: String,
    val encodedBy: String,
    val returnedAtLabel: String
)

private enum class EmployeeEntryMode
{
    LISTED,
    DEPARTMENT_ONLY,
    ADD_NEW
}

private data class BorrowSuccessNotice(
    val title: String,
    val message: String
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BorrowItemsScreen(navController: NavController) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    var selectedTab by rememberSaveable { mutableIntStateOf(0) }

    var borrowSerial by rememberSaveable { mutableStateOf("") }
    var borrowModelQuery by rememberSaveable { mutableStateOf("") }
    val borrowModelResults = remember { mutableStateListOf<BorrowItemDto>() }
    var borrowModelSearchError by rememberSaveable { mutableStateOf<String?>(null) }
    var returnSerial by rememberSaveable { mutableStateOf("") }
    var borrowResolveResult by remember { mutableStateOf<BorrowResolveResponse?>(null) }
    var returnResolveResult by remember { mutableStateOf<BorrowResolveResponse?>(null) }

    var borrowEmployeeQuery by rememberSaveable { mutableStateOf("") }
    val borrowEmployeeResults = remember { mutableStateListOf<BorrowEmployeeDto>() }
    var selectedBorrowEmployee by remember { mutableStateOf<BorrowEmployeeDto?>(null) }
    var borrowEmployeeMode by rememberSaveable { mutableStateOf(EmployeeEntryMode.LISTED.name) }
    var borrowEmployeeSearchAttempted by rememberSaveable { mutableStateOf(false) }
    val borrowCompanies = remember { mutableStateListOf<BorrowCompanyDto>() }
    val borrowFilterDepartments = remember { mutableStateListOf<BorrowDepartmentDto>() }
    val borrowAddBranches = remember { mutableStateListOf<BorrowBranchDto>() }
    val borrowAddDepartments = remember { mutableStateListOf<BorrowDepartmentDto>() }
    var selectedBorrowCompanyFilter by remember { mutableStateOf<BorrowCompanyDto?>(null) }
    var selectedBorrowDepartmentFilter by remember { mutableStateOf<BorrowDepartmentDto?>(null) }
    var selectedBorrowAddCompany by remember { mutableStateOf<BorrowCompanyDto?>(null) }
    var selectedBorrowAddBranch by remember { mutableStateOf<BorrowBranchDto?>(null) }
    var selectedBorrowAddDepartment by remember { mutableStateOf<BorrowDepartmentDto?>(null) }
    var borrowNewEmployeeName by rememberSaveable { mutableStateOf("") }
    var borrowNewEmployeeNumber by rememberSaveable { mutableStateOf("") }
    var borrowNewEmployeePosition by rememberSaveable { mutableStateOf("") }

    var returnEmployeeQuery by rememberSaveable { mutableStateOf("") }
    val returnEmployeeResults = remember { mutableStateListOf<BorrowEmployeeDto>() }
    var selectedReturnEmployee by remember { mutableStateOf<BorrowEmployeeDto?>(null) }
    var returnEmployeeMode by rememberSaveable { mutableStateOf(EmployeeEntryMode.LISTED.name) }
    var returnEmployeeSearchAttempted by rememberSaveable { mutableStateOf(false) }
    val returnCompanies = remember { mutableStateListOf<BorrowCompanyDto>() }
    val returnFilterDepartments = remember { mutableStateListOf<BorrowDepartmentDto>() }
    val returnAddBranches = remember { mutableStateListOf<BorrowBranchDto>() }
    val returnAddDepartments = remember { mutableStateListOf<BorrowDepartmentDto>() }
    var selectedReturnCompanyFilter by remember { mutableStateOf<BorrowCompanyDto?>(null) }
    var selectedReturnDepartmentFilter by remember { mutableStateOf<BorrowDepartmentDto?>(null) }
    var selectedReturnAddCompany by remember { mutableStateOf<BorrowCompanyDto?>(null) }
    var selectedReturnAddBranch by remember { mutableStateOf<BorrowBranchDto?>(null) }
    var selectedReturnAddDepartment by remember { mutableStateOf<BorrowDepartmentDto?>(null) }
    var returnNewEmployeeName by rememberSaveable { mutableStateOf("") }
    var returnNewEmployeeNumber by rememberSaveable { mutableStateOf("") }
    var returnNewEmployeePosition by rememberSaveable { mutableStateOf("") }

    var isResolvingBorrow by remember { mutableStateOf(false) }
    var isSearchingBorrowModels by remember { mutableStateOf(false) }
    var isResolvingReturn by remember { mutableStateOf(false) }
    var isSearchingBorrowEmployees by remember { mutableStateOf(false) }
    var isSearchingReturnEmployees by remember { mutableStateOf(false) }
    var isBorrowSubmitting by remember { mutableStateOf(false) }
    var isReturnSubmitting by remember { mutableStateOf(false) }
    var isCreatingBorrowEmployee by remember { mutableStateOf(false) }
    var isCreatingReturnEmployee by remember { mutableStateOf(false) }

    var borrowBackdateEnabled by rememberSaveable { mutableStateOf(false) }
    var borrowedAtMillis by rememberSaveable { mutableStateOf(System.currentTimeMillis()) }
    var borrowConfirmation by remember { mutableStateOf<BorrowConfirmationPreview?>(null) }
    var returnConfirmation by remember { mutableStateOf<ReturnConfirmationPreview?>(null) }
    var successNotice by remember { mutableStateOf<BorrowSuccessNotice?>(null) }
    var borrowInlineError by remember { mutableStateOf<String?>(null) }
    var returnInlineError by remember { mutableStateOf<String?>(null) }
    var borrowInlineSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var returnInlineSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }

    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val scannedSerial = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }?.collectAsState(initial = null)?.value
    val createdBorrowSerial = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>("created_borrow_serial", null)
    }?.collectAsState(initial = null)?.value

    fun showMessage(message: String) {
        scope.launch { snackbarHostState.showSnackbar(message) }
    }

    fun showBorrowInlineError(
        message: String,
        alsoSnackbar: Boolean = false,
        supportDetails: Pair<String, String>? = null
    ) {
        borrowInlineError = message
        borrowInlineSupportDetails = supportDetails
        if (alsoSnackbar) showMessage(message)
    }

    fun showReturnInlineError(
        message: String,
        alsoSnackbar: Boolean = false,
        supportDetails: Pair<String, String>? = null
    ) {
        returnInlineError = message
        returnInlineSupportDetails = supportDetails
        if (alsoSnackbar) showMessage(message)
    }

    fun resetBorrowDetails() {
        borrowResolveResult = null
        borrowModelQuery = ""
        borrowModelResults.clear()
        borrowModelSearchError = null
        selectedBorrowEmployee = null
        selectedBorrowDepartmentFilter = null
        borrowEmployeeMode = EmployeeEntryMode.LISTED.name
        borrowEmployeeResults.clear()
        borrowEmployeeQuery = ""
        borrowEmployeeSearchAttempted = false
        borrowBackdateEnabled = false
        borrowedAtMillis = System.currentTimeMillis()
        borrowInlineError = null
        borrowInlineSupportDetails = null
    }

    fun resetReturnDetails() {
        returnResolveResult = null
        selectedReturnEmployee = null
        selectedReturnDepartmentFilter = null
        returnEmployeeMode = EmployeeEntryMode.LISTED.name
        returnEmployeeResults.clear()
        returnEmployeeQuery = ""
        returnEmployeeSearchAttempted = false
        returnInlineError = null
        returnInlineSupportDetails = null
    }

    fun resetBorrowNewEmployeeDraft() {
        borrowNewEmployeeName = ""
        borrowNewEmployeeNumber = ""
        borrowNewEmployeePosition = ""
        selectedBorrowAddBranch = null
        selectedBorrowAddDepartment = null
    }

    fun resetReturnNewEmployeeDraft() {
        returnNewEmployeeName = ""
        returnNewEmployeeNumber = ""
        returnNewEmployeePosition = ""
        selectedReturnAddBranch = null
        selectedReturnAddDepartment = null
    }

    fun loadBorrowFilterDepartments(company: BorrowCompanyDto?) {
        borrowFilterDepartments.clear()
        selectedBorrowDepartmentFilter = null
        if (company == null || company.comId <= 0) return
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowDepartments(company.comId) }) {
                is ApiResult.Success -> borrowFilterDepartments.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load departments right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }
    }

    fun loadBorrowAddLookups(company: BorrowCompanyDto?) {
        borrowAddBranches.clear()
        borrowAddDepartments.clear()
        selectedBorrowAddBranch = null
        selectedBorrowAddDepartment = null
        if (company == null || company.comId <= 0) return
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowBranches(company.comId) }) {
                is ApiResult.Success -> borrowAddBranches.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load branches right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }

        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowDepartments(company.comId) }) {
                is ApiResult.Success -> borrowAddDepartments.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load departments right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }
    }

    fun loadReturnFilterDepartments(company: BorrowCompanyDto?) {
        returnFilterDepartments.clear()
        selectedReturnDepartmentFilter = null
        if (company == null || company.comId <= 0) return
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowDepartments(company.comId) }) {
                is ApiResult.Success -> returnFilterDepartments.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load departments right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }
    }

    fun loadReturnAddLookups(company: BorrowCompanyDto?) {
        returnAddBranches.clear()
        returnAddDepartments.clear()
        selectedReturnAddBranch = null
        selectedReturnAddDepartment = null
        if (company == null || company.comId <= 0) return
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowBranches(company.comId) }) {
                is ApiResult.Success -> returnAddBranches.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load branches right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }

        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowDepartments(company.comId) }) {
                is ApiResult.Success -> returnAddDepartments.addAll(result.data)
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load departments right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }
    }

    fun loadBorrowCompanies() {
        borrowCompanies.clear()
        returnCompanies.clear()

        scope.launch {
            when (val result = safeApiCall { ApiClient.service.getBorrowCompanies() }) {
                is ApiResult.Success -> {
                    borrowCompanies.clear()
                    returnCompanies.clear()
                    borrowCompanies.addAll(result.data)
                    returnCompanies.addAll(result.data)
                    selectedBorrowCompanyFilter = null
                    selectedReturnCompanyFilter = null
                    selectedBorrowAddCompany = result.data.firstOrNull()
                    selectedReturnAddCompany = result.data.firstOrNull()
                    loadBorrowAddLookups(selectedBorrowAddCompany)
                    loadReturnAddLookups(selectedReturnAddCompany)
                }
                is ApiResult.HttpError -> showMessage(result.userMessageOr("We couldn't load companies right now."))
                is ApiResult.NetworkError -> showMessage(result.userMessageOr())
                is ApiResult.UnknownError -> showMessage(result.userMessageOr())
            }
        }
    }

    fun applyBorrowEmployeeSelection(employee: BorrowEmployeeDto) {
        selectedBorrowEmployee = employee
        borrowInlineError = null
        borrowInlineSupportDetails = null
        borrowEmployeeQuery = employee.employeeName.orEmpty()
        borrowEmployeeSearchAttempted = false
        selectedBorrowCompanyFilter = borrowCompanies.firstOrNull { it.comId == employee.comId }
        loadBorrowFilterDepartments(selectedBorrowCompanyFilter)
        selectedBorrowDepartmentFilter = borrowFilterDepartments.firstOrNull { it.deptId == employee.deptId }
        borrowEmployeeMode = EmployeeEntryMode.LISTED.name
        successNotice = BorrowSuccessNotice(
            title = "Borrower Selected",
            message = "${employee.employeeName.orEmpty()} is ready for this borrow."
        )
    }

    fun applyReturnEmployeeSelection(employee: BorrowEmployeeDto) {
        selectedReturnEmployee = employee
        returnInlineError = null
        returnInlineSupportDetails = null
        returnEmployeeQuery = employee.employeeName.orEmpty()
        returnEmployeeSearchAttempted = false
        selectedReturnCompanyFilter = returnCompanies.firstOrNull { it.comId == employee.comId }
        loadReturnFilterDepartments(selectedReturnCompanyFilter)
        selectedReturnDepartmentFilter = returnFilterDepartments.firstOrNull { it.deptId == employee.deptId }
        returnEmployeeMode = EmployeeEntryMode.LISTED.name
        successNotice = BorrowSuccessNotice(
            title = "Returner Selected",
            message = "${employee.employeeName.orEmpty()} is ready for this return."
        )
    }

    fun createBorrowEmployee() {
        val company = selectedBorrowAddCompany
        val branch = selectedBorrowAddBranch
        val department = selectedBorrowAddDepartment
        borrowInlineError = null
        borrowInlineSupportDetails = null
        if (borrowNewEmployeeName.trim().isBlank()) return showBorrowInlineError("Enter the employee name first.", alsoSnackbar = true)
        if (company == null || company.comId <= 0) return showBorrowInlineError("Select a company first.", alsoSnackbar = true)
        if (branch == null || branch.branchId <= 0) return showBorrowInlineError("Select a branch first.", alsoSnackbar = true)
        if (department == null || department.deptId <= 0) return showBorrowInlineError("Select a department first.", alsoSnackbar = true)

        isCreatingBorrowEmployee = true
        scope.launch {
            val request = BorrowEmployeeCreateRequest(
                employeeName = borrowNewEmployeeName.trim(),
                employeeNumber = borrowNewEmployeeNumber.trim().takeIf { it.isNotBlank() },
                position = borrowNewEmployeePosition.trim().takeIf { it.isNotBlank() },
                comId = company.comId,
                branchId = branch.branchId,
                deptId = department.deptId
            )
            when (val result = safeApiCall { ApiClient.service.createBorrowEmployee(request) }) {
                is ApiResult.Success -> {
                    borrowEmployeeResults.add(0, result.data)
                    applyBorrowEmployeeSelection(result.data)
                    resetBorrowNewEmployeeDraft()
                    borrowInlineError = null
                    borrowInlineSupportDetails = null
                    successNotice = BorrowSuccessNotice(
                        title = "Employee Ready",
                        message = "${result.data.employeeName.orEmpty()} was added and selected for this borrow."
                    )
                    showMessage("Employee saved and selected.")
                }
                is ApiResult.HttpError -> showBorrowInlineError(
                    result.userMessageOr("We couldn't create the employee right now."),
                    supportDetails = buildHttpApiError("Borrow employee details", result)
                )
                is ApiResult.NetworkError -> showBorrowInlineError(
                    result.userMessageOr("Can't create the employee right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Borrow employee details", result)
                )
                is ApiResult.UnknownError -> showBorrowInlineError(
                    result.userMessageOr("Something went wrong while creating the employee. Please try again."),
                    supportDetails = buildUnknownApiError("Borrow employee details", result)
                )
            }
            isCreatingBorrowEmployee = false
        }
    }

    fun createReturnEmployee() {
        val company = selectedReturnAddCompany
        val branch = selectedReturnAddBranch
        val department = selectedReturnAddDepartment
        returnInlineError = null
        returnInlineSupportDetails = null
        if (returnNewEmployeeName.trim().isBlank()) return showReturnInlineError("Enter the employee name first.", alsoSnackbar = true)
        if (company == null || company.comId <= 0) return showReturnInlineError("Select a company first.", alsoSnackbar = true)
        if (branch == null || branch.branchId <= 0) return showReturnInlineError("Select a branch first.", alsoSnackbar = true)
        if (department == null || department.deptId <= 0) return showReturnInlineError("Select a department first.", alsoSnackbar = true)

        isCreatingReturnEmployee = true
        scope.launch {
            val request = BorrowEmployeeCreateRequest(
                employeeName = returnNewEmployeeName.trim(),
                employeeNumber = returnNewEmployeeNumber.trim().takeIf { it.isNotBlank() },
                position = returnNewEmployeePosition.trim().takeIf { it.isNotBlank() },
                comId = company.comId,
                branchId = branch.branchId,
                deptId = department.deptId
            )
            when (val result = safeApiCall { ApiClient.service.createBorrowEmployee(request) }) {
                is ApiResult.Success -> {
                    returnEmployeeResults.add(0, result.data)
                    applyReturnEmployeeSelection(result.data)
                    resetReturnNewEmployeeDraft()
                    returnInlineError = null
                    returnInlineSupportDetails = null
                    successNotice = BorrowSuccessNotice(
                        title = "Employee Ready",
                        message = "${result.data.employeeName.orEmpty()} was added and selected for this return."
                    )
                    showMessage("Employee saved and selected.")
                }
                is ApiResult.HttpError -> showReturnInlineError(
                    result.userMessageOr("We couldn't create the employee right now."),
                    supportDetails = buildHttpApiError("Return employee details", result)
                )
                is ApiResult.NetworkError -> showReturnInlineError(
                    result.userMessageOr("Can't create the employee right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Return employee details", result)
                )
                is ApiResult.UnknownError -> showReturnInlineError(
                    result.userMessageOr("Something went wrong while creating the employee. Please try again."),
                    supportDetails = buildUnknownApiError("Return employee details", result)
                )
            }
            isCreatingReturnEmployee = false
        }
    }

    fun resolveBorrowSerial() {
        val serial = borrowSerial.trim()
        successNotice = null
        borrowInlineError = null
        borrowInlineSupportDetails = null
        if (serial.isBlank()) {
            showBorrowInlineError("Enter or scan a serial number first.", alsoSnackbar = true)
            return
        }
        isResolvingBorrow = true
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.resolveBorrow(serial) }) {
                is ApiResult.Success -> {
                    borrowResolveResult = result.data
                    borrowInlineError = when {
                        result.data.item == null -> result.data.eligibilityMessage ?: "Item not found for that serial."
                        result.data.openBorrow != null -> "This item already has an active borrow record."
                        result.data.canBorrow == false -> result.data.eligibilityMessage ?: "This item is not currently eligible for borrowing."
                        else -> null
                    }
                    borrowInlineSupportDetails = null
                    successNotice = if (result.data.item != null && result.data.openBorrow == null && result.data.canBorrow != false) {
                        BorrowSuccessNotice(
                            title = "Item Ready",
                            message = "${result.data.item?.itemName.orEmpty().ifBlank { serial }} resolved successfully."
                        )
                    } else {
                        null
                    }
                    if (!borrowInlineError.isNullOrBlank()) showMessage(borrowInlineError!!)
                }
                is ApiResult.HttpError -> {
                    borrowResolveResult = null
                    showBorrowInlineError(
                        result.userMessageOr("We couldn't find that serial right now. Please try again."),
                        supportDetails = buildHttpApiError("Borrow resolve details", result)
                    )
                }
                is ApiResult.NetworkError -> showBorrowInlineError(
                    result.userMessageOr("Can't reach the borrow service right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Borrow resolve details", result)
                )
                is ApiResult.UnknownError -> showBorrowInlineError(
                    result.userMessageOr("Something went wrong while checking that serial. Please try again."),
                    supportDetails = buildUnknownApiError("Borrow resolve details", result)
                )
            }
            isResolvingBorrow = false
        }
    }

    fun selectBorrowModelSuggestion(suggestion: BorrowItemDto) {
        val selectedSerial = suggestion.serialNumber?.trim().orEmpty()
        if (selectedSerial.isBlank()) {
            showBorrowInlineError("That model match does not have a usable serial number.", alsoSnackbar = true)
            return
        }
        borrowModelQuery = ""
        borrowModelResults.clear()
        borrowModelSearchError = null
        borrowSerial = selectedSerial
        borrowResolveResult = null
        borrowInlineError = null
        borrowInlineSupportDetails = null
        resolveBorrowSerial()
    }

    fun resolveReturnSerial() {
        val serial = returnSerial.trim()
        successNotice = null
        returnInlineError = null
        returnInlineSupportDetails = null
        if (serial.isBlank()) {
            showReturnInlineError("Enter or scan a serial number first.", alsoSnackbar = true)
            return
        }
        isResolvingReturn = true
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.resolveBorrow(serial) }) {
                is ApiResult.Success -> {
                    returnResolveResult = result.data
                    returnInlineError = if (result.data.openBorrow == null) {
                        "No active borrow was found for that serial."
                    } else {
                        null
                    }
                    returnInlineSupportDetails = null
                    successNotice = if (result.data.openBorrow != null) {
                        BorrowSuccessNotice(
                            title = "Open Borrow Found",
                            message = "${result.data.openBorrow?.serialNumber.orEmpty().ifBlank { serial }} is ready for return review."
                        )
                        } else {
                            null
                        }
                        if (result.data.openBorrow == null) showMessage("No active borrow was found for that serial.")
                    }
                is ApiResult.HttpError -> {
                    returnResolveResult = null
                    showReturnInlineError(
                        result.userMessageOr("We couldn't find that serial right now. Please try again."),
                        supportDetails = buildHttpApiError("Return resolve details", result)
                    )
                }
                is ApiResult.NetworkError -> showReturnInlineError(
                    result.userMessageOr("Can't reach the borrow service right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Return resolve details", result)
                )
                is ApiResult.UnknownError -> showReturnInlineError(
                    result.userMessageOr("Something went wrong while checking that serial. Please try again."),
                    supportDetails = buildUnknownApiError("Return resolve details", result)
                )
            }
            isResolvingReturn = false
        }
    }

    fun searchBorrowEmployees() {
        val query = borrowEmployeeQuery.trim()
        val companyId = selectedBorrowCompanyFilter?.comId?.takeIf { it > 0 }
        val departmentId = selectedBorrowDepartmentFilter?.deptId?.takeIf { it > 0 }
        borrowInlineError = null
        borrowInlineSupportDetails = null
        if (query.length < 2 && companyId == null && departmentId == null) {
            showBorrowInlineError("Search at least 2 characters or choose a company or department filter.", alsoSnackbar = true)
            return
        }
        borrowEmployeeSearchAttempted = true
        isSearchingBorrowEmployees = true
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.searchBorrowEmployees(query.takeIf { it.isNotBlank() }, companyId, departmentId) }) {
                is ApiResult.Success -> {
                    borrowEmployeeResults.clear()
                    borrowEmployeeResults.addAll(result.data)
                    borrowInlineError = if (result.data.isEmpty()) "No employees matched that search." else null
                    borrowInlineSupportDetails = null
                }
                is ApiResult.HttpError -> showBorrowInlineError(
                    result.userMessageOr("We couldn't search employees right now."),
                    supportDetails = buildHttpApiError("Borrow employee search details", result)
                )
                is ApiResult.NetworkError -> showBorrowInlineError(
                    result.userMessageOr("Can't reach employee search right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Borrow employee search details", result)
                )
                is ApiResult.UnknownError -> showBorrowInlineError(
                    result.userMessageOr("Something went wrong while searching employees. Please try again."),
                    supportDetails = buildUnknownApiError("Borrow employee search details", result)
                )
            }
            isSearchingBorrowEmployees = false
        }
    }

    fun searchReturnEmployees() {
        val query = returnEmployeeQuery.trim()
        val companyId = selectedReturnCompanyFilter?.comId?.takeIf { it > 0 }
        val departmentId = selectedReturnDepartmentFilter?.deptId?.takeIf { it > 0 }
        returnInlineError = null
        returnInlineSupportDetails = null
        if (query.length < 2 && companyId == null && departmentId == null) {
            showReturnInlineError("Search at least 2 characters or choose a company or department filter.", alsoSnackbar = true)
            return
        }
        returnEmployeeSearchAttempted = true
        isSearchingReturnEmployees = true
        scope.launch {
            when (val result = safeApiCall { ApiClient.service.searchBorrowEmployees(query.takeIf { it.isNotBlank() }, companyId, departmentId) }) {
                is ApiResult.Success -> {
                    returnEmployeeResults.clear()
                    returnEmployeeResults.addAll(result.data)
                    returnInlineError = if (result.data.isEmpty()) "No employees matched that search." else null
                    returnInlineSupportDetails = null
                }
                is ApiResult.HttpError -> showReturnInlineError(
                    result.userMessageOr("We couldn't search employees right now."),
                    supportDetails = buildHttpApiError("Return employee search details", result)
                )
                is ApiResult.NetworkError -> showReturnInlineError(
                    result.userMessageOr("Can't reach employee search right now. Check your connection and try again."),
                    supportDetails = buildNetworkApiError("Return employee search details", result)
                )
                is ApiResult.UnknownError -> showReturnInlineError(
                    result.userMessageOr("Something went wrong while searching employees. Please try again."),
                    supportDetails = buildUnknownApiError("Return employee search details", result)
                )
            }
            isSearchingReturnEmployees = false
        }
    }

    fun performBorrowSubmit() {
        val item = borrowResolveResult?.item
        val openBorrow = borrowResolveResult?.openBorrow
        val departmentOnly = EmployeeEntryMode.valueOf(borrowEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY
        val borrower = selectedBorrowEmployee
        val department = selectedBorrowDepartmentFilter
        if (item == null || item.itemId <= 0) return showBorrowInlineError("Resolve an item first.", alsoSnackbar = true)
        if (openBorrow != null) return showBorrowInlineError("This item is already borrowed.", alsoSnackbar = true)
        if (borrowResolveResult?.canBorrow == false) return showBorrowInlineError(
            borrowResolveResult?.eligibilityMessage ?: "This item is not currently eligible for borrowing.",
            alsoSnackbar = true
        )
        if (departmentOnly && (department == null || department.deptId <= 0)) return showBorrowInlineError("Select the department that borrowed the item.", alsoSnackbar = true)
        if (!departmentOnly && (borrower == null || borrower.empId <= 0)) return showBorrowInlineError("Select the employee who borrowed the item.", alsoSnackbar = true)
        if (borrowBackdateEnabled && borrowedAtMillis > System.currentTimeMillis() + 60_000L) return showBorrowInlineError("Borrowed date and time cannot be in the future.", alsoSnackbar = true)
        isBorrowSubmitting = true
        scope.launch {
            val request = BorrowCreateRequest(
                clientRequestId = UUID.randomUUID().toString(),
                serialNumber = item.serialNumber?.trim().orEmpty(),
                borrowedByEmpId = if (departmentOnly) null else borrower?.empId,
                borrowedByDeptId = if (departmentOnly) department?.deptId else null,
                borrowedByDeptName = if (departmentOnly) department?.departmentName else null,
                borrowedAtUtc = if (borrowBackdateEnabled) Instant.ofEpochMilli(borrowedAtMillis).toString() else null
            )
            when (val result = safeApiCall { ApiClient.service.borrowItem(request) }) {
                is ApiResult.Success -> {
                    showMessage(result.data.message?.ifBlank { "Borrow entry saved successfully." } ?: "Borrow entry saved successfully.")
                    borrowSerial = ""; resetBorrowDetails()
                    successNotice = BorrowSuccessNotice("Borrow Saved", result.data.message?.ifBlank { "The borrow record was saved successfully." } ?: "The borrow record was saved successfully.")
                }
                is ApiResult.HttpError -> showBorrowInlineError(result.userMessageOr("We couldn't save the borrow entry right now."), supportDetails = buildHttpApiError("Borrow save details", result))
                is ApiResult.NetworkError -> showBorrowInlineError(result.userMessageOr("Can't save the borrow entry right now. Check your connection and try again."), supportDetails = buildNetworkApiError("Borrow save details", result))
                is ApiResult.UnknownError -> showBorrowInlineError(result.userMessageOr("Something went wrong while saving the borrow entry. Please try again."), supportDetails = buildUnknownApiError("Borrow save details", result))
            }
            isBorrowSubmitting = false
        }
    }

    fun requestBorrowConfirmation() {
        val item = borrowResolveResult?.item
        val openBorrow = borrowResolveResult?.openBorrow
        val departmentOnly = EmployeeEntryMode.valueOf(borrowEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY
        val borrower = selectedBorrowEmployee
        val department = selectedBorrowDepartmentFilter
        if (item == null || item.itemId <= 0) return showBorrowInlineError("Resolve an item first.", alsoSnackbar = true)
        if (openBorrow != null) return showBorrowInlineError("This item is already borrowed.", alsoSnackbar = true)
        if (borrowResolveResult?.canBorrow == false) return showBorrowInlineError(
            borrowResolveResult?.eligibilityMessage ?: "This item is not currently eligible for borrowing.",
            alsoSnackbar = true
        )
        if (departmentOnly && (department == null || department.deptId <= 0)) return showBorrowInlineError("Select the department that borrowed the item.", alsoSnackbar = true)
        if (!departmentOnly && (borrower == null || borrower.empId <= 0)) return showBorrowInlineError("Select the employee who borrowed the item.", alsoSnackbar = true)
        if (borrowBackdateEnabled && borrowedAtMillis > System.currentTimeMillis() + 60_000L) return showBorrowInlineError("Borrowed date and time cannot be in the future.", alsoSnackbar = true)
        borrowConfirmation = BorrowConfirmationPreview(
            itemName = item.itemName?.ifBlank { item.serialNumber.orEmpty() } ?: item.serialNumber.orEmpty(),
            itemDescription = item.itemDescription.orEmpty(), modelNumber = item.modelNumber.orEmpty(), serialNumber = item.serialNumber.orEmpty(),
            borrowerName = if (departmentOnly) "${department?.departmentName.orEmpty()} (no specific employee)" else borrower?.employeeName.orEmpty(),
            companyName = if (departmentOnly) selectedBorrowCompanyFilter?.companyName.orEmpty() else borrower?.companyName.orEmpty(),
            borrowerDepartment = if (departmentOnly) department?.departmentName.orEmpty() else borrower?.departmentName.orEmpty(),
            encodedBy = UserSession.currentUser?.displayName?.ifBlank { UserSession.currentUser?.username.orEmpty() } ?: UserSession.currentUser?.username.orEmpty().ifBlank { "Current user" },
            timestampLabel = "Borrowed At",
            borrowedAtLabel = if (borrowBackdateEnabled) "${formatLocalDate(borrowedAtMillis)} ${formatLocalTime(borrowedAtMillis)}" else formatApiTimestamp(Instant.now().toString()),
            note = if (borrowBackdateEnabled) "Encoded from logbook using the actual borrow start time." else null
        )
    }

    fun performReturnSubmit() {
        val openBorrow = returnResolveResult?.openBorrow
        val departmentOnly = EmployeeEntryMode.valueOf(returnEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY
        val returner = selectedReturnEmployee
        val department = selectedReturnDepartmentFilter
        if (openBorrow == null || openBorrow.borrowId <= 0 || !openBorrow.isOpen) return showReturnInlineError("Find an open borrow first.", alsoSnackbar = true)
        if (departmentOnly && (department == null || department.deptId <= 0)) return showReturnInlineError("Select the department returning the item.", alsoSnackbar = true)
        if (!departmentOnly && (returner == null || returner.empId <= 0)) return showReturnInlineError("Select the employee returning the item.", alsoSnackbar = true)
        isReturnSubmitting = true
        scope.launch {
            val request = BorrowReturnRequest(
                clientRequestId = UUID.randomUUID().toString(), borrowId = openBorrow.borrowId,
                returnedByEmpId = if (departmentOnly) null else returner?.empId,
                returnedByDeptId = if (departmentOnly) department?.deptId else null,
                returnedByDeptName = if (departmentOnly) department?.departmentName else null
            )
            when (val result = safeApiCall { ApiClient.service.returnBorrow(request) }) {
                is ApiResult.Success -> {
                    showMessage(result.data.message?.ifBlank { "Return entry saved successfully." } ?: "Return entry saved successfully.")
                    returnSerial = ""; resetReturnDetails()
                    successNotice = BorrowSuccessNotice("Return Saved", result.data.message?.ifBlank { "The return record was saved successfully." } ?: "The return record was saved successfully.")
                }
                is ApiResult.HttpError -> showReturnInlineError(result.userMessageOr("We couldn't save the return entry right now."), supportDetails = buildHttpApiError("Return save details", result))
                is ApiResult.NetworkError -> showReturnInlineError(result.userMessageOr("Can't save the return entry right now. Check your connection and try again."), supportDetails = buildNetworkApiError("Return save details", result))
                is ApiResult.UnknownError -> showReturnInlineError(result.userMessageOr("Something went wrong while saving the return entry. Please try again."), supportDetails = buildUnknownApiError("Return save details", result))
            }
            isReturnSubmitting = false
        }
    }

    fun requestReturnConfirmation() {
        val openBorrow = returnResolveResult?.openBorrow
        val departmentOnly = EmployeeEntryMode.valueOf(returnEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY
        val returner = selectedReturnEmployee
        val department = selectedReturnDepartmentFilter
        if (openBorrow == null || openBorrow.borrowId <= 0 || !openBorrow.isOpen) return showReturnInlineError("Find an open borrow first.", alsoSnackbar = true)
        if (departmentOnly && (department == null || department.deptId <= 0)) return showReturnInlineError("Select the department returning the item.", alsoSnackbar = true)
        if (!departmentOnly && (returner == null || returner.empId <= 0)) return showReturnInlineError("Select the employee returning the item.", alsoSnackbar = true)
        returnConfirmation = ReturnConfirmationPreview(
            itemName = openBorrow.itemName?.ifBlank { openBorrow.serialNumber.orEmpty() } ?: openBorrow.serialNumber.orEmpty(),
            itemDescription = openBorrow.itemDescription.orEmpty(), modelNumber = openBorrow.modelNumber.orEmpty(), serialNumber = openBorrow.serialNumber.orEmpty(),
            borrowedBy = openBorrow.borrowedByEmpName.orEmpty(), borrowedAtLabel = formatApiTimestamp(openBorrow.borrowedAtUtc),
            returnedBy = if (departmentOnly) "${department?.departmentName.orEmpty()} (no specific employee)" else returner?.employeeName.orEmpty(),
            department = if (departmentOnly) department?.departmentName.orEmpty() else returner?.departmentName.orEmpty(),
            encodedBy = UserSession.currentUser?.displayName?.ifBlank { UserSession.currentUser?.username.orEmpty() } ?: UserSession.currentUser?.username.orEmpty().ifBlank { "Current user" },
            returnedAtLabel = formatApiTimestamp(Instant.now().toString())
        )
    }

    LaunchedEffect(scannedSerial) {
        val scanned = scannedSerial?.trim().orEmpty()
        if (scanned.isBlank()) return@LaunchedEffect
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
        if (selectedTab == 0) {
            borrowSerial = scanned
            resolveBorrowSerial()
        } else {
            returnSerial = scanned
            resolveReturnSerial()
        }
    }

    LaunchedEffect(createdBorrowSerial) {
        val created = createdBorrowSerial?.trim().orEmpty()
        if (created.isBlank()) return@LaunchedEffect
        navBackStackEntry?.savedStateHandle?.remove<String>("created_borrow_serial")
        selectedTab = 0
        borrowSerial = created
        resolveBorrowSerial()
        successNotice = BorrowSuccessNotice(
            title = "Item Ready",
            message = "The new item was created. Review the resolved item and choose who borrowed it."
        )
        showMessage("Item created successfully. Continue by selecting who borrowed it.")
    }

    LaunchedEffect(borrowModelQuery) {
        val query = borrowModelQuery.trim()
        borrowModelSearchError = null
        if (query.isBlank()) {
            borrowModelResults.clear()
            isSearchingBorrowModels = false
            return@LaunchedEffect
        }

        delay(300)
        if (query != borrowModelQuery.trim()) return@LaunchedEffect
        isSearchingBorrowModels = true
        when (val result = safeApiCall { ApiClient.service.searchBorrowItemsByModel(query) }) {
            is ApiResult.Success -> {
                if (query == borrowModelQuery.trim()) {
                    borrowModelResults.clear()
                    borrowModelResults.addAll(result.data)
                    borrowModelSearchError = null
                }
            }
            is ApiResult.HttpError -> {
                if (query == borrowModelQuery.trim()) {
                    borrowModelResults.clear()
                    borrowModelSearchError = result.userMessageOr("We couldn't search model numbers right now.")
                }
            }
            is ApiResult.NetworkError -> {
                if (query == borrowModelQuery.trim()) {
                    borrowModelResults.clear()
                    borrowModelSearchError = result.userMessageOr("Can't reach model search right now. Check your connection and try again.")
                }
            }
            is ApiResult.UnknownError -> {
                if (query == borrowModelQuery.trim()) {
                    borrowModelResults.clear()
                    borrowModelSearchError = result.userMessageOr("Something went wrong while searching model numbers. Please try again.")
                }
            }
        }
        if (query == borrowModelQuery.trim()) isSearchingBorrowModels = false
    }

    LaunchedEffect(Unit) {
        loadBorrowCompanies()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text(
                            text = "Borrow / Return",
                            color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "Equipment handover workspace",
                            style = MaterialTheme.typography.labelSmall,
                            color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Muted
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back",
                            tint = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink
                        )
                    }
                },
                actions = {
                    IconButton(onClick = { navController.navigate("borrow_records") }) {
                        Icon(
                            Icons.Filled.History,
                            contentDescription = "Borrow records",
                            tint = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface,
                    titleContentColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
                    navigationIconContentColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
                    actionIconContentColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)

                .padding(paddingValues)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 16.dp, vertical = 10.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                BorrowWorkspaceOverview(
                    selectedTab = selectedTab,
                    borrowResolveResult = borrowResolveResult,
                    returnResolveResult = returnResolveResult,
                    borrowBackdateEnabled = borrowBackdateEnabled,
                    borrowerReady = if (EmployeeEntryMode.valueOf(borrowEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY) {
                        selectedBorrowDepartmentFilter?.deptId?.let { it > 0 } == true
                    } else {
                        selectedBorrowEmployee != null
                    },
                    returnerReady = if (EmployeeEntryMode.valueOf(returnEmployeeMode) == EmployeeEntryMode.DEPARTMENT_ONLY) {
                        selectedReturnDepartmentFilter?.deptId?.let { it > 0 } == true
                    } else {
                        selectedReturnEmployee != null
                    },
                    onOpenRecords = { navController.navigate("borrow_records") }
                )
                AnimatedVisibility(visible = successNotice != null) {
                    successNotice?.let { notice ->
                        SuccessStateCard(
                            title = notice.title,
                            message = notice.message
                        )
                    }
                }
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    com.example.yakultscanner.ui.components.ScannerSectionHeader(
                        title = if (selectedTab == 0) "Borrow transaction" else "Return transaction",
                        subtitle = if (selectedTab == 0) {
                            "Resolve the equipment, choose who receives it, then review the handover."
                        } else {
                            "Find the open borrow, choose who returns it, then review the handover."
                        }
                    )
                    BorrowModeTabs(
                        selectedTab = selectedTab,
                        onTabSelected = {
                            selectedTab = it
                            successNotice = null
                            borrowInlineError = null
                            returnInlineError = null
                            borrowInlineSupportDetails = null
                            returnInlineSupportDetails = null
                        }
                    )
                    if (selectedTab == 0) {
                        BorrowForm(
                                    serial = borrowSerial,
                                    modelQuery = borrowModelQuery,
                                    onModelQueryChange = {
                                        borrowModelSearchError = null
                                        borrowModelQuery = it
                                    },
                                    modelResults = borrowModelResults,
                                    modelSearchError = borrowModelSearchError,
                                    modelSearching = isSearchingBorrowModels,
                                    onModelSuggestionSelected = ::selectBorrowModelSuggestion,
                                    onSerialChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowSerial = it
                                        if (borrowResolveResult != null && it.trim() != borrowResolveResult?.item?.serialNumber?.trim()) borrowResolveResult = null
                                    },
                                    resolveResult = borrowResolveResult,
                                    onAddNewItem = { navController.navigate("borrow_add_item") },
                                    employeeMode = EmployeeEntryMode.valueOf(borrowEmployeeMode),
                                    onEmployeeModeChange = {
                                        borrowEmployeeMode = it.name
                                        if (it != EmployeeEntryMode.LISTED) selectedBorrowEmployee = null
                                        if (it == EmployeeEntryMode.ADD_NEW) borrowEmployeeSearchAttempted = false
                                    },
                                    filterCompanies = borrowCompanies,
                                    selectedFilterCompany = selectedBorrowCompanyFilter,
                                    onFilterCompanySelected = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        selectedBorrowCompanyFilter = it
                                        loadBorrowFilterDepartments(it)
                                    },
                                    filterDepartments = borrowFilterDepartments,
                                    selectedFilterDepartment = selectedBorrowDepartmentFilter,
                                    onFilterDepartmentSelected = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        selectedBorrowDepartmentFilter = it
                                    },
                                    borrowerQuery = borrowEmployeeQuery,
                                    onBorrowerQueryChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowEmployeeQuery = it
                                        borrowEmployeeSearchAttempted = false
                                    },
                                    borrowerResults = borrowEmployeeResults,
                                    searchAttempted = borrowEmployeeSearchAttempted,
                                    selectedBorrower = selectedBorrowEmployee,
                                    onBorrowerSelected = ::applyBorrowEmployeeSelection,
                                    onResolveClick = ::resolveBorrowSerial,
                                    onSearchBorrower = ::searchBorrowEmployees,
                                    newEmployeeName = borrowNewEmployeeName,
                                    onNewEmployeeNameChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowNewEmployeeName = it
                                    },
                                    newEmployeeNumber = borrowNewEmployeeNumber,
                                    onNewEmployeeNumberChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowNewEmployeeNumber = it
                                    },
                                    newEmployeePosition = borrowNewEmployeePosition,
                                    onNewEmployeePositionChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowNewEmployeePosition = it
                                    },
                                    addEmployeeCompanies = borrowCompanies,
                                    selectedAddEmployeeCompany = selectedBorrowAddCompany,
                                    onAddEmployeeCompanySelected = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        selectedBorrowAddCompany = it
                                        loadBorrowAddLookups(it)
                                    },
                                    addEmployeeBranches = borrowAddBranches,
                                    selectedAddEmployeeBranch = selectedBorrowAddBranch,
                                    onAddEmployeeBranchSelected = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        selectedBorrowAddBranch = it
                                    },
                                    addEmployeeDepartments = borrowAddDepartments,
                                    selectedAddEmployeeDepartment = selectedBorrowAddDepartment,
                                    onAddEmployeeDepartmentSelected = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        selectedBorrowAddDepartment = it
                                    },
                                    onCreateEmployee = ::createBorrowEmployee,
                                    onScanClick = {
                                        navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                        navController.navigate("scanner_camera")
                                    },
                                    onBorrowClick = ::requestBorrowConfirmation,
                                    backdateEnabled = borrowBackdateEnabled,
                                    onBackdateChange = {
                                        borrowInlineError = null
                                        borrowInlineSupportDetails = null
                                        borrowBackdateEnabled = it
                                        if (it) borrowedAtMillis = System.currentTimeMillis()
                                    },
                                    borrowedAtMillis = borrowedAtMillis,
                                    onPickDate = { showDatePicker(context, borrowedAtMillis) { borrowedAtMillis = it } },
                                    onPickTime = { showTimePicker(context, borrowedAtMillis) { borrowedAtMillis = it } },
                                    resolving = isResolvingBorrow,
                                    searchingEmployees = isSearchingBorrowEmployees,
                                    creatingEmployee = isCreatingBorrowEmployee,
                                    submitting = isBorrowSubmitting,
                                    errorMessage = borrowInlineError,
                                    errorSupportDetails = borrowInlineSupportDetails
                                )
                            } else {
                                ReturnForm(
                                    serial = returnSerial,
                                    onSerialChange = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        returnSerial = it
                                        if (returnResolveResult != null && it.trim() != returnResolveResult?.item?.serialNumber?.trim()) returnResolveResult = null
                                    },
                                    resolveResult = returnResolveResult,
                                    employeeMode = EmployeeEntryMode.valueOf(returnEmployeeMode),
                                    onEmployeeModeChange = {
                                        returnEmployeeMode = it.name
                                        if (it != EmployeeEntryMode.LISTED) selectedReturnEmployee = null
                                        if (it == EmployeeEntryMode.ADD_NEW) returnEmployeeSearchAttempted = false
                                    },
                                    filterCompanies = returnCompanies,
                                    selectedFilterCompany = selectedReturnCompanyFilter,
                                    onFilterCompanySelected = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        selectedReturnCompanyFilter = it
                                        loadReturnFilterDepartments(it)
                                    },
                                    filterDepartments = returnFilterDepartments,
                                    selectedFilterDepartment = selectedReturnDepartmentFilter,
                                    onFilterDepartmentSelected = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        selectedReturnDepartmentFilter = it
                                    },
                                    returnerQuery = returnEmployeeQuery,
                                    onReturnerQueryChange = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        returnEmployeeQuery = it
                                        returnEmployeeSearchAttempted = false
                                    },
                                    returnerResults = returnEmployeeResults,
                                    searchAttempted = returnEmployeeSearchAttempted,
                                    selectedReturner = selectedReturnEmployee,
                                    onReturnerSelected = ::applyReturnEmployeeSelection,
                                    onResolveClick = ::resolveReturnSerial,
                                    onSearchReturner = ::searchReturnEmployees,
                                    newEmployeeName = returnNewEmployeeName,
                                    onNewEmployeeNameChange = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        returnNewEmployeeName = it
                                    },
                                    newEmployeeNumber = returnNewEmployeeNumber,
                                    onNewEmployeeNumberChange = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        returnNewEmployeeNumber = it
                                    },
                                    newEmployeePosition = returnNewEmployeePosition,
                                    onNewEmployeePositionChange = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        returnNewEmployeePosition = it
                                    },
                                    addEmployeeCompanies = returnCompanies,
                                    selectedAddEmployeeCompany = selectedReturnAddCompany,
                                    onAddEmployeeCompanySelected = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        selectedReturnAddCompany = it
                                        loadReturnAddLookups(it)
                                    },
                                    addEmployeeBranches = returnAddBranches,
                                    selectedAddEmployeeBranch = selectedReturnAddBranch,
                                    onAddEmployeeBranchSelected = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        selectedReturnAddBranch = it
                                    },
                                    addEmployeeDepartments = returnAddDepartments,
                                    selectedAddEmployeeDepartment = selectedReturnAddDepartment,
                                    onAddEmployeeDepartmentSelected = {
                                        returnInlineError = null
                                        returnInlineSupportDetails = null
                                        selectedReturnAddDepartment = it
                                    },
                                    onCreateEmployee = ::createReturnEmployee,
                                    onScanClick = {
                                        navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                        navController.navigate("scanner_camera")
                                    },
                                    onReturnClick = ::requestReturnConfirmation,
                                    resolving = isResolvingReturn,
                                    searchingEmployees = isSearchingReturnEmployees,
                                    creatingEmployee = isCreatingReturnEmployee,
                                    submitting = isReturnSubmitting,
                                    errorMessage = returnInlineError,
                                    errorSupportDetails = returnInlineSupportDetails
                                )
                    }
                }
                Spacer(modifier = Modifier.height(96.dp))
            }
        }
    }

    borrowConfirmation?.let { pending ->
        AlertDialog(
            onDismissRequest = { borrowConfirmation = null },
            title = { Text("Confirm Borrow") },
            text = {
                Column(
                    modifier = Modifier.verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Text(
                        text = "Review the details below. This will create a new borrow log entry for the selected item.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    ConfirmationDetailRow("Item", pending.itemName)
                    ConfirmationDetailRow("Description", pending.itemDescription.ifBlank { "--" })
                    ConfirmationDetailRow("Model", pending.modelNumber.ifBlank { "--" })
                    ConfirmationDetailRow("Serial", pending.serialNumber)
                    ConfirmationDetailRow("Borrowed By", pending.borrowerName)
                    ConfirmationDetailRow("Company", pending.companyName.ifBlank { "--" })
                    ConfirmationDetailRow("Department", pending.borrowerDepartment.ifBlank { "--" })
                    ConfirmationDetailRow("Encoded By", pending.encodedBy.ifBlank { "--" })
                    ConfirmationDetailRow(pending.timestampLabel, pending.borrowedAtLabel)
                    if (!pending.note.isNullOrBlank()) {
                        ConfirmationDetailRow("Note", pending.note)
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    borrowConfirmation = null
                    performBorrowSubmit()
                }) {
                    Text("Confirm")
                }
            },
            dismissButton = {
                TextButton(onClick = { borrowConfirmation = null }) {
                    Text("Cancel")
                }
            }
        )
    }

    returnConfirmation?.let { pending ->
        AlertDialog(
            onDismissRequest = { returnConfirmation = null },
            title = { Text("Confirm Return") },
            text = {
                Column(
                    modifier = Modifier.verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Text(
                        text = "Review the details below. This will close the open borrow log entry for the selected item.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    ConfirmationDetailRow("Item", pending.itemName)
                    ConfirmationDetailRow("Description", pending.itemDescription.ifBlank { "--" })
                    ConfirmationDetailRow("Model", pending.modelNumber.ifBlank { "--" })
                    ConfirmationDetailRow("Serial", pending.serialNumber)
                    ConfirmationDetailRow("Borrowed By", pending.borrowedBy.ifBlank { "--" })
                    ConfirmationDetailRow("Borrowed At", pending.borrowedAtLabel)
                    ConfirmationDetailRow("Returned By", pending.returnedBy.ifBlank { "--" })
                    ConfirmationDetailRow("Department", pending.department.ifBlank { "--" })
                    ConfirmationDetailRow("Encoded By", pending.encodedBy.ifBlank { "--" })
                    ConfirmationDetailRow("Date/Time", pending.returnedAtLabel)
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    returnConfirmation = null
                    performReturnSubmit()
                }) {
                    Text("Confirm")
                }
            },
            dismissButton = {
                TextButton(onClick = { returnConfirmation = null }) {
                    Text("Cancel")
                }
            }
        )
    }
}

@Composable
private fun ConfirmationDetailRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.Top
    ) {
        Text(
            text = label,
            modifier = Modifier.weight(0.42f),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = value,
            modifier = Modifier.weight(0.58f),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}


@Composable
private fun WorkflowSummaryCard(
    selectedTab: Int,
    borrowResolveResult: BorrowResolveResponse?,
    returnResolveResult: BorrowResolveResponse?,
    borrowBackdateEnabled: Boolean
) {
    val isBorrow = selectedTab == 0
    val item = borrowResolveResult?.item
    val borrowOpen = borrowResolveResult?.openBorrow
    val returnOpen = returnResolveResult?.openBorrow

    val title: String
    val text: String
    if (isBorrow && borrowOpen != null) {
        title = "Borrow Blocked"
        text = "This item already has an open borrow record, so the scanner should prevent a duplicate checkout."
    } else if (isBorrow && item != null) {
        title = if (borrowBackdateEnabled) "Ready for Logbook Encoding" else "Ready to Borrow"
        text = if (borrowBackdateEnabled) {
            "The preview is using an actual borrowed date/time so elapsed time starts from the logbook entry."
        } else {
            "Serial resolved successfully. Pick the borrower and submit the new borrow record."
        }
    } else if (!isBorrow && returnOpen != null) {
        title = "Return Record Found"
        text = "The scanner has enough context to show who currently holds the item before completing the return."
    } else {
        title = if (isBorrow) "Borrow Flow" else "Return Flow"
        text = "Scan a serial number to load a real item and continue with the workflow."
    }

    BorrowStateCard(
        title = title,
        message = text,
        tone = when {
            title.contains("Blocked") -> BorrowCardTone.Error
            title.contains("Ready") || title.contains("Found") -> BorrowCardTone.Success
            else -> BorrowCardTone.Neutral
        }
    )
}


@Composable
private fun BorrowWorkspaceOverview(
    selectedTab: Int,
    borrowResolveResult: BorrowResolveResponse?,
    returnResolveResult: BorrowResolveResponse?,
    borrowBackdateEnabled: Boolean,
    borrowerReady: Boolean,
    returnerReady: Boolean,
    onOpenRecords: () -> Unit
) {
    val isBorrow = selectedTab == 0
    val resolvedItem = if (isBorrow) borrowResolveResult?.item else returnResolveResult?.openBorrow
    val blocked = isBorrow && borrowResolveResult?.openBorrow != null
    val actorReady = if (isBorrow) borrowerReady else returnerReady
    val ready = !blocked && resolvedItem != null && actorReady
    val summary = when {
        blocked -> "This item already has an active borrow. Open the record to review or return it."
        ready -> "All required details are ready. Review the handover, then confirm it."
        resolvedItem != null -> if (isBorrow) "Item found. Choose the responsible borrower." else "Open borrow found. Choose who is returning it."
        else -> if (isBorrow) "Start by scanning a QR code or entering the item serial." else "Find the currently open borrow before recording a return."
    }
    val state = when { blocked -> "Blocked"; ready -> "Ready"; resolvedItem != null -> "In progress"; else -> "Step 1" }
    val stateColors = when {
        blocked -> MaterialTheme.colorScheme.errorContainer to MaterialTheme.colorScheme.onErrorContainer
        ready -> MaterialTheme.colorScheme.secondaryContainer to MaterialTheme.colorScheme.onSecondaryContainer
        else -> MaterialTheme.colorScheme.primaryContainer to MaterialTheme.colorScheme.onPrimaryContainer
    }
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(
            containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = BorderStroke(
            1.dp,
            com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Divider
        )
    ) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(if (isBorrow) "Borrow workspace" else "Return workspace", fontWeight = FontWeight.Bold)
                    Text("Complete the handover one step at a time.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                StatusPillCompact(state, stateColors.first, stateColors.second)
            }
            Text(summary, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                WorkflowStepChip("1", if (isBorrow) "Item" else "Find open", resolvedItem != null && !blocked)
                WorkflowStepChip("2", if (isBorrow) "Borrower" else "Returner", actorReady)
                if (isBorrow) WorkflowStepChip("3", if (borrowBackdateEnabled) "Logbook time" else "Time", !borrowBackdateEnabled || resolvedItem != null)
                WorkflowStepChip(if (isBorrow) "4" else "3", "Review", ready)
            }
            OutlinedButton(onClick = onOpenRecords, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.History, contentDescription = null, modifier = Modifier.size(17.dp))
                Spacer(modifier = Modifier.size(8.dp))
                Text("View Open Borrow Records")
            }
        }
    }
}

@Composable
private fun WorkflowStepChip(number: String, label: String, complete: Boolean) {
    val container = if (complete) MaterialTheme.colorScheme.secondaryContainer else MaterialTheme.colorScheme.surfaceVariant
    val content = if (complete) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.onSurfaceVariant
    Surface(shape = RoundedCornerShape(12.dp), color = container, contentColor = content) {
        Row(modifier = Modifier.padding(horizontal = 10.dp, vertical = 7.dp), horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(number, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
            Text(label, style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Medium)
        }
    }
}

@Composable
private fun StatusPill(label: String) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.18f)),
        shape = RoundedCornerShape(999.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.18f))
    ) {
        Text(
            text = label,
            color = MaterialTheme.colorScheme.onPrimary,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 7.dp),
            style = MaterialTheme.typography.labelLarge
        )
    }
}

@Composable
private fun StatusPillCompact(label: String, containerColor: Color, contentColor: Color) {
    Surface(
        color = containerColor,
        contentColor = contentColor,
        shape = RoundedCornerShape(999.dp)
    ) {
        Text(
            text = label,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Bold
        )
    }
}

@Composable
private fun BorrowModeTabs(selectedTab: Int, onTabSelected: (Int) -> Unit) {
    com.example.yakultscanner.ui.components.ScannerModeSegment(
        firstLabel = "Borrow",
        secondLabel = "Return",
        firstSelected = selectedTab == 0,
        onFirstSelected = { onTabSelected(0) },
        onSecondSelected = { onTabSelected(1) }
    )
}
@Composable
private fun BorrowModeTabCard(
    title: String,
    subtitle: String,
    selected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    val containerColor = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
    val contentColor = if (selected) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurface
    Card(
        modifier = modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = containerColor),
        border = BorderStroke(
            1.dp,
            if (selected) MaterialTheme.colorScheme.primary.copy(alpha = 0.35f) else MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.28f)
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(2.dp)
        ) {
            Text(title, fontWeight = FontWeight.Bold, color = contentColor)
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = contentColor.copy(alpha = 0.82f))
        }
    }
}

@Composable
private fun SuccessStateCard(title: String, message: String) {
    BorrowStateCard(title = title, message = message, tone = BorrowCardTone.Success, badgeText = "Ready")
}

@Composable
private fun ResolvedItemCard(title: String, subtitle: String, lines: List<String>) {
    BorrowSummaryCard(
        title = title,
        subtitle = subtitle,
        lines = lines,
        tone = BorrowCardTone.Accent,
        badgeText = "Resolved"
    )
}

@Composable
private fun BackdateSectionCard(
    enabled: Boolean,
    borrowedAtMillis: Long,
    onEnabledChange: (Boolean) -> Unit,
    onPickDate: () -> Unit,
    onPickTime: () -> Unit,
    validationMessage: String? = null
) {
    Card(
        modifier = Modifier.animateContentSize(),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.38f))
    ) {
        Column(modifier = Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text("3 · Timing (optional)", fontWeight = FontWeight.Bold)
                    Text(
                        if (enabled) "Using the actual borrowed date and time."
                        else "Off by default. Turn this on only for older manual logbook entries.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Switch(checked = enabled, onCheckedChange = onEnabledChange)
            }
            if (enabled) {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedButton(onClick = onPickDate, modifier = Modifier.weight(1f)) {
                        Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.size(6.dp))
                        Text(formatLocalDate(borrowedAtMillis))
                    }
                    OutlinedButton(onClick = onPickTime, modifier = Modifier.weight(1f)) {
                        Icon(Icons.Filled.Schedule, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.size(6.dp))
                        Text(formatLocalTime(borrowedAtMillis))
                    }
                }
                if (!validationMessage.isNullOrBlank()) {
                    Text(
                        text = validationMessage,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error
                    )
                }
            }
        }
    }
}

@Composable
private fun SubmitSectionCard(
    title: String,
    helper: String,
    actionText: String,
    loading: Boolean,
    enabled: Boolean,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier.animateContentSize(),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.32f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(title, fontWeight = FontWeight.Bold)
            Text(helper, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            ActionButton(actionText, loading, enabled, onClick)
        }
    }
}

@Composable
private fun BorrowForm(
    serial: String,
    modelQuery: String,
    onModelQueryChange: (String) -> Unit,
    modelResults: List<BorrowItemDto>,
    modelSearchError: String?,
    modelSearching: Boolean,
    onModelSuggestionSelected: (BorrowItemDto) -> Unit,
    onSerialChange: (String) -> Unit,
    resolveResult: BorrowResolveResponse?,
    onAddNewItem: () -> Unit,
    employeeMode: EmployeeEntryMode,
    onEmployeeModeChange: (EmployeeEntryMode) -> Unit,
    filterCompanies: List<BorrowCompanyDto>,
    selectedFilterCompany: BorrowCompanyDto?,
    onFilterCompanySelected: (BorrowCompanyDto?) -> Unit,
    filterDepartments: List<BorrowDepartmentDto>,
    selectedFilterDepartment: BorrowDepartmentDto?,
    onFilterDepartmentSelected: (BorrowDepartmentDto?) -> Unit,
    borrowerQuery: String,
    onBorrowerQueryChange: (String) -> Unit,
    borrowerResults: List<BorrowEmployeeDto>,
    searchAttempted: Boolean,
    selectedBorrower: BorrowEmployeeDto?,
    onBorrowerSelected: (BorrowEmployeeDto) -> Unit,
    onResolveClick: () -> Unit,
    onSearchBorrower: () -> Unit,
    newEmployeeName: String,
    onNewEmployeeNameChange: (String) -> Unit,
    newEmployeeNumber: String,
    onNewEmployeeNumberChange: (String) -> Unit,
    newEmployeePosition: String,
    onNewEmployeePositionChange: (String) -> Unit,
    addEmployeeCompanies: List<BorrowCompanyDto>,
    selectedAddEmployeeCompany: BorrowCompanyDto?,
    onAddEmployeeCompanySelected: (BorrowCompanyDto) -> Unit,
    addEmployeeBranches: List<BorrowBranchDto>,
    selectedAddEmployeeBranch: BorrowBranchDto?,
    onAddEmployeeBranchSelected: (BorrowBranchDto) -> Unit,
    addEmployeeDepartments: List<BorrowDepartmentDto>,
    selectedAddEmployeeDepartment: BorrowDepartmentDto?,
    onAddEmployeeDepartmentSelected: (BorrowDepartmentDto) -> Unit,
    onCreateEmployee: () -> Unit,
    onScanClick: () -> Unit,
    onBorrowClick: () -> Unit,
    backdateEnabled: Boolean,
    onBackdateChange: (Boolean) -> Unit,
    borrowedAtMillis: Long,
    onPickDate: () -> Unit,
    onPickTime: () -> Unit,
    resolving: Boolean,
    searchingEmployees: Boolean,
    creatingEmployee: Boolean,
    submitting: Boolean,
    errorMessage: String?,
    errorSupportDetails: Pair<String, String>?
) {
    val serialValidationMessage = when {
        errorMessage.containsValidationToken("serial", "resolve an item") -> "Scan or enter a serial number before continuing."
        else -> null
    }
    val backdateValidationMessage = if (errorMessage.containsValidationToken("future")) {
        "Pick a borrowed date and time that is not in the future."
    } else {
        null
    }
    val item = resolveResult?.item
    val openBorrow = resolveResult?.openBorrow
    val borrowEligibilityBlocked = resolveResult?.canBorrow == false && openBorrow == null
    val borrowerReady = if (employeeMode == EmployeeEntryMode.DEPARTMENT_ONLY) selectedFilterDepartment?.deptId?.let { it > 0 } == true else selectedBorrower != null
    // ── Item section ───────────────────────────────────────────────────────
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f),
        border = androidx.compose.foundation.BorderStroke(1.dp, com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Info)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            com.example.yakultscanner.ui.components.ScannerSectionHeader(
                title = "1 · Item",
                subtitle = "Scan a QR code, enter a serial, or search by model number."
            )
            BorrowModelLookupCard(
                query = modelQuery,
                onQueryChange = onModelQueryChange,
                results = modelResults,
                searching = modelSearching,
                errorMessage = modelSearchError,
                onSuggestionSelected = onModelSuggestionSelected
            )
            SerialEntryCard(
                serial = serial,
                onSerialChange = onSerialChange,
                onScanClick = onScanClick,
                onResolveClick = onResolveClick,
                resolveLabel = "Resolve",
                resolving = resolving,
                validationMessage = serialValidationMessage
            )
            if (!errorMessage.isNullOrBlank()) {
                InlineFormErrorCard(errorMessage, errorSupportDetails)
            }
            if (item != null) {
                ResolvedItemCard(
                    title = item.itemName?.ifBlank { item.serialNumber.orEmpty() } ?: item.serialNumber.orEmpty(),
                    subtitle = "Resolved and ready for borrow",
                    lines = listOf(
                        "Serial: ${item.serialNumber.orEmpty()}",
                        "Model: ${item.modelNumber?.ifBlank { "--" } ?: "--"}",
                        "Description: ${item.itemDescription?.ifBlank { "--" } ?: "--"}"
                    )
                )
            }
            OutlinedButton(onClick = onAddNewItem, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(modifier = Modifier.size(8.dp))
                Text("Add New Item and Continue Borrow")
            }
            if (openBorrow != null) WarningCard("Already Borrowed", "${openBorrow.borrowedByEmpName.orEmpty()} - ${openBorrow.borrowedByDeptName.orEmpty()} at ${formatApiTimestamp(openBorrow.borrowedAtUtc)}")
        }
    }

    // ── Borrowee section ───────────────────────────────────────────────────
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF4CAF50))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            com.example.yakultscanner.ui.components.ScannerSectionHeader(
                title = "2 · Borrower",
                subtitle = "Select a listed employee, a whole department, or add a new employee."
            )
            if (employeeMode == EmployeeEntryMode.DEPARTMENT_ONLY) {
                DepartmentOnlyAssignmentCard(
                    title = "Borrowed By",
                    onEmployeeModeChange = onEmployeeModeChange,
                    filterCompanies = filterCompanies,
                    selectedFilterCompany = selectedFilterCompany,
                    onFilterCompanySelected = onFilterCompanySelected,
                    filterDepartments = filterDepartments,
                    selectedFilterDepartment = selectedFilterDepartment,
                    onFilterDepartmentSelected = onFilterDepartmentSelected,
                    validationMessage = errorMessage
                )
            } else {            EmployeeAssignmentCard(
        title = "Borrowed By",
        employeeMode = employeeMode,
        onEmployeeModeChange = onEmployeeModeChange,
        filterCompanies = filterCompanies,
        selectedFilterCompany = selectedFilterCompany,
        onFilterCompanySelected = onFilterCompanySelected,
        filterDepartments = filterDepartments,
        selectedFilterDepartment = selectedFilterDepartment,
        onFilterDepartmentSelected = onFilterDepartmentSelected,
        query = borrowerQuery,
        onQueryChange = onBorrowerQueryChange,
        results = borrowerResults,
        searchAttempted = searchAttempted,
        selectedEmployee = selectedBorrower,
        onEmployeeSelected = onBorrowerSelected,
        onSearch = onSearchBorrower,
        searching = searchingEmployees,
        newEmployeeName = newEmployeeName,
        onNewEmployeeNameChange = onNewEmployeeNameChange,
        newEmployeeNumber = newEmployeeNumber,
        onNewEmployeeNumberChange = onNewEmployeeNumberChange,
        newEmployeePosition = newEmployeePosition,
        onNewEmployeePositionChange = onNewEmployeePositionChange,
        addEmployeeCompanies = addEmployeeCompanies,
        selectedAddEmployeeCompany = selectedAddEmployeeCompany,
        onAddEmployeeCompanySelected = onAddEmployeeCompanySelected,
        addEmployeeBranches = addEmployeeBranches,
        selectedAddEmployeeBranch = selectedAddEmployeeBranch,
        onAddEmployeeBranchSelected = onAddEmployeeBranchSelected,
        addEmployeeDepartments = addEmployeeDepartments,
        selectedAddEmployeeDepartment = selectedAddEmployeeDepartment,
        onAddEmployeeDepartmentSelected = onAddEmployeeDepartmentSelected,
        onCreateEmployee = onCreateEmployee,
        creatingEmployee = creatingEmployee,
        validationMessage = errorMessage
    )
            }
        } // end Column
    } // end Borrowee Surface

    BackdateSectionCard(
        enabled = backdateEnabled,
        borrowedAtMillis = borrowedAtMillis,
        onEnabledChange = onBackdateChange,
        onPickDate = onPickDate,
        onPickTime = onPickTime,
        validationMessage = backdateValidationMessage
    )
    SubmitSectionCard(
        title = "4 · Review & confirm",
        helper = if (openBorrow == null && item != null && borrowerReady && !borrowEligibilityBlocked) {
            "Resolved item and borrower confirmed. Review the details, then confirm the borrow."
        } else {
            "Resolve an item and choose an employee or whole department before continuing."
        },
        actionText = "Confirm Borrow",
        loading = submitting,
        enabled = !submitting && openBorrow == null && item != null && borrowerReady && !borrowEligibilityBlocked,
        onClick = onBorrowClick
    )
}

@Composable
private fun ReturnForm(
    serial: String,
    onSerialChange: (String) -> Unit,
    resolveResult: BorrowResolveResponse?,
    employeeMode: EmployeeEntryMode,
    onEmployeeModeChange: (EmployeeEntryMode) -> Unit,
    filterCompanies: List<BorrowCompanyDto>,
    selectedFilterCompany: BorrowCompanyDto?,
    onFilterCompanySelected: (BorrowCompanyDto?) -> Unit,
    filterDepartments: List<BorrowDepartmentDto>,
    selectedFilterDepartment: BorrowDepartmentDto?,
    onFilterDepartmentSelected: (BorrowDepartmentDto?) -> Unit,
    returnerQuery: String,
    onReturnerQueryChange: (String) -> Unit,
    returnerResults: List<BorrowEmployeeDto>,
    searchAttempted: Boolean,
    selectedReturner: BorrowEmployeeDto?,
    onReturnerSelected: (BorrowEmployeeDto) -> Unit,
    onResolveClick: () -> Unit,
    onSearchReturner: () -> Unit,
    newEmployeeName: String,
    onNewEmployeeNameChange: (String) -> Unit,
    newEmployeeNumber: String,
    onNewEmployeeNumberChange: (String) -> Unit,
    newEmployeePosition: String,
    onNewEmployeePositionChange: (String) -> Unit,
    addEmployeeCompanies: List<BorrowCompanyDto>,
    selectedAddEmployeeCompany: BorrowCompanyDto?,
    onAddEmployeeCompanySelected: (BorrowCompanyDto) -> Unit,
    addEmployeeBranches: List<BorrowBranchDto>,
    selectedAddEmployeeBranch: BorrowBranchDto?,
    onAddEmployeeBranchSelected: (BorrowBranchDto) -> Unit,
    addEmployeeDepartments: List<BorrowDepartmentDto>,
    selectedAddEmployeeDepartment: BorrowDepartmentDto?,
    onAddEmployeeDepartmentSelected: (BorrowDepartmentDto) -> Unit,
    onCreateEmployee: () -> Unit,
    onScanClick: () -> Unit,
    onReturnClick: () -> Unit,
    resolving: Boolean,
    searchingEmployees: Boolean,
    creatingEmployee: Boolean,
    submitting: Boolean,
    errorMessage: String?,
    errorSupportDetails: Pair<String, String>?
) {
    val serialValidationMessage = when {
        errorMessage.containsValidationToken("serial", "find an open borrow") -> "Scan or enter a serial number before continuing."
        else -> null
    }

    val openBorrow = resolveResult?.openBorrow
    val returnerReady = if (employeeMode == EmployeeEntryMode.DEPARTMENT_ONLY) selectedFilterDepartment?.deptId?.let { it > 0 } == true else selectedReturner != null

    // ── Item section ───────────────────────────────────────────────────────
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f),
        border = androidx.compose.foundation.BorderStroke(1.dp, com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Info)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            com.example.yakultscanner.ui.components.ScannerSectionHeader(
                title = "1 · Item",
                subtitle = "Scan a QR code, enter a serial, or search by model number."
            )
            SerialEntryCard(
                serial = serial,
                onSerialChange = onSerialChange,
                onScanClick = onScanClick,
                onResolveClick = onResolveClick,
                resolveLabel = "Find Open",
                resolving = resolving,
                validationMessage = serialValidationMessage
            )
            if (!errorMessage.isNullOrBlank()) {
                InlineFormErrorCard(errorMessage, errorSupportDetails)
            }
            if (openBorrow != null) {
                ResolvedItemCard(
                    title = openBorrow.itemName?.ifBlank { openBorrow.serialNumber.orEmpty() } ?: openBorrow.serialNumber.orEmpty(),
                    subtitle = "Open borrow ready for return",
                    lines = listOf(
                        "Borrowed by: ${openBorrow.borrowedByEmpName.orEmpty()}",
                        "Department: ${openBorrow.borrowedByDeptName.orEmpty()}",
                        "Borrowed at: ${formatApiTimestamp(openBorrow.borrowedAtUtc)}"
                    )
                )
            }
            if (resolveResult != null && openBorrow == null) WarningCard("No Open Borrow", "This item is not currently checked out, so there is nothing to return yet.")
        }
    }

    // ── Returnee section ───────────────────────────────────────────────────
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF4CAF50))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            com.example.yakultscanner.ui.components.ScannerSectionHeader(
                title = "2 · Returner",
                subtitle = "Select who is returning the equipment before reviewing the transaction."
            )
            if (employeeMode == EmployeeEntryMode.DEPARTMENT_ONLY) {
                DepartmentOnlyAssignmentCard(
                    title = "Returned By",
                    onEmployeeModeChange = onEmployeeModeChange,
                    filterCompanies = filterCompanies,
                    selectedFilterCompany = selectedFilterCompany,
                    onFilterCompanySelected = onFilterCompanySelected,
                    filterDepartments = filterDepartments,
                    selectedFilterDepartment = selectedFilterDepartment,
                    onFilterDepartmentSelected = onFilterDepartmentSelected,
                    validationMessage = errorMessage
                )
            } else {            EmployeeAssignmentCard(
        title = "Returned By",
        employeeMode = employeeMode,
        onEmployeeModeChange = onEmployeeModeChange,
        filterCompanies = filterCompanies,
        selectedFilterCompany = selectedFilterCompany,
        onFilterCompanySelected = onFilterCompanySelected,
        filterDepartments = filterDepartments,
        selectedFilterDepartment = selectedFilterDepartment,
        onFilterDepartmentSelected = onFilterDepartmentSelected,
        query = returnerQuery,
        onQueryChange = onReturnerQueryChange,
        results = returnerResults,
        searchAttempted = searchAttempted,
        selectedEmployee = selectedReturner,
        onEmployeeSelected = onReturnerSelected,
        onSearch = onSearchReturner,
        searching = searchingEmployees,
        newEmployeeName = newEmployeeName,
        onNewEmployeeNameChange = onNewEmployeeNameChange,
        newEmployeeNumber = newEmployeeNumber,
        onNewEmployeeNumberChange = onNewEmployeeNumberChange,
        newEmployeePosition = newEmployeePosition,
        onNewEmployeePositionChange = onNewEmployeePositionChange,
        addEmployeeCompanies = addEmployeeCompanies,
        selectedAddEmployeeCompany = selectedAddEmployeeCompany,
        onAddEmployeeCompanySelected = onAddEmployeeCompanySelected,
        addEmployeeBranches = addEmployeeBranches,
        selectedAddEmployeeBranch = selectedAddEmployeeBranch,
        onAddEmployeeBranchSelected = onAddEmployeeBranchSelected,
        addEmployeeDepartments = addEmployeeDepartments,
        selectedAddEmployeeDepartment = selectedAddEmployeeDepartment,
        onAddEmployeeDepartmentSelected = onAddEmployeeDepartmentSelected,
        onCreateEmployee = onCreateEmployee,
        creatingEmployee = creatingEmployee,
        validationMessage = errorMessage
    )
            }
        } // end Column
    } // end Returnee Surface

    SubmitSectionCard(
        title = "3 · Review & confirm",
        helper = if (openBorrow != null && returnerReady) {
            "Open borrow found and returner confirmed. Review the details, then confirm the return."
        } else {
            "Find an open borrow and choose an employee or whole department before continuing."
        },
        actionText = "Confirm Return",
        loading = submitting,
        enabled = !submitting && openBorrow != null && returnerReady,
        onClick = onReturnClick
    )
}

@Composable
private fun InlineFormErrorCard(
    message: String,
    supportDetails: Pair<String, String>? = null
) {
    BorrowStateCard(
        title = "Please check this step",
        message = message,
        tone = BorrowCardTone.Error,
        badgeText = "Action needed"
    ) {
        if (supportDetails != null) {
            Text(
                text = supportDetails.first,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onErrorContainer
            )
            Text(
                text = supportDetails.second,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.82f)
            )
        }
    }
}

@Composable
private fun BorrowModelLookupCard(
    query: String,
    onQueryChange: (String) -> Unit,
    results: List<BorrowItemDto>,
    searching: Boolean,
    errorMessage: String?,
    onSuggestionSelected: (BorrowItemDto) -> Unit
) {
    Card(
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.28f)),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f))
    ) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text("Model Number", fontWeight = FontWeight.Bold)
                Text(
                    "Optional · find a borrowable serial",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            OutlinedTextField(
                value = query,
                onValueChange = onQueryChange,
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                label = { Text("Search model number") },
                placeholder = { Text("e.g. Latitude 5420") },
                trailingIcon = if (query.isNotBlank()) {
                    {
                        TextButton(onClick = { onQueryChange("") }) { Text("Clear") }
                    }
                } else {
                    null
                }
            )
            if (searching) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.Center,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                    Spacer(modifier = Modifier.size(8.dp))
                    Text("Finding matching borrowable items…", style = MaterialTheme.typography.bodySmall)
                }
            }
            if (!errorMessage.isNullOrBlank()) {
                Text(errorMessage, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
            } else if (!searching && query.isNotBlank() && results.isEmpty()) {
                Text(
                    "No borrowable serialized items matched that model.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            if (results.isNotEmpty()) {
                Card(
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.45f))
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(max = 260.dp)
                            .verticalScroll(rememberScrollState())
                    ) {
                        results.forEach { suggestion ->
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { onSuggestionSelected(suggestion) }
                                    .padding(horizontal = 12.dp, vertical = 10.dp)
                            ) {
                                Text(
                                    suggestion.serialNumber.orEmpty(),
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.primary
                                )
                                Text(
                                    suggestion.itemName?.ifBlank { "Unnamed item" } ?: "Unnamed item",
                                    style = MaterialTheme.typography.bodySmall
                                )
                                Text(
                                    "Model: ${suggestion.modelNumber?.ifBlank { "--" } ?: "--"}",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            if (suggestion != results.last()) {
                                androidx.compose.material3.HorizontalDivider()
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SerialEntryCard(
    serial: String,
    onSerialChange: (String) -> Unit,
    onScanClick: () -> Unit,
    onResolveClick: () -> Unit,
    resolveLabel: String,
    resolving: Boolean,
    validationMessage: String? = null
) {
    Card(shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f))) {
        Column(modifier = Modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("Serial Number", fontWeight = FontWeight.Bold)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                OutlinedTextField(
                    value = serial,
                    onValueChange = onSerialChange,
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    label = { Text("Scan or type serial") },
                    isError = !validationMessage.isNullOrBlank(),
                    supportingText = validationMessage?.let { { Text(it) } }
                )
                IconButton(onClick = onScanClick, modifier = Modifier.size(52.dp).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f), CircleShape)) {
                    Icon(Icons.Filled.QrCodeScanner, contentDescription = "Scan serial")
                }
            }
            Button(onClick = onResolveClick, enabled = !resolving, modifier = Modifier.fillMaxWidth()) {
                if (resolving) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(18.dp),
                        strokeWidth = 2.dp,
                        color = LocalContentColor.current
                    )
                } else {
                    Text(resolveLabel)
                }
            }
        }
    }
}

@Composable
private fun DepartmentOnlyAssignmentCard(
    title: String,
    onEmployeeModeChange: (EmployeeEntryMode) -> Unit,
    filterCompanies: List<BorrowCompanyDto>,
    selectedFilterCompany: BorrowCompanyDto?,
    onFilterCompanySelected: (BorrowCompanyDto?) -> Unit,
    filterDepartments: List<BorrowDepartmentDto>,
    selectedFilterDepartment: BorrowDepartmentDto?,
    onFilterDepartmentSelected: (BorrowDepartmentDto?) -> Unit,
    validationMessage: String? = null
) {
    var companyExpanded by rememberSaveable(title) { mutableStateOf(false) }
    var departmentExpanded by rememberSaveable("${title}_department") { mutableStateOf(false) }
    val companyOptions = listOf(BorrowCompanyDto(comId = 0, companyName = "Select company")) + filterCompanies
    val departmentOptions = listOf(BorrowDepartmentDto(deptId = 0, comId = null, departmentName = "Select department")) + filterDepartments
    val departmentValidation = if (validationMessage.containsValidationToken("department")) "Choose the department responsible for this handover." else null
    Card(modifier = Modifier.animateContentSize(), shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.32f))) {
        Column(modifier = Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(title, fontWeight = FontWeight.Bold)
            Row(modifier = Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(selected = false, onClick = { onEmployeeModeChange(EmployeeEntryMode.LISTED) }, label = { Text("Listed employee") })
                FilterChip(selected = true, onClick = {}, label = { Text("Whole department") })
                FilterChip(selected = false, onClick = { onEmployeeModeChange(EmployeeEntryMode.ADD_NEW) }, label = { Text("Add employee") })
            }
            Text("Use this when the item is handed to or returned by a department, not a specific employee.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            BatchDropdownField(
                label = "Company", selectedValue = selectedFilterCompany?.companyName ?: "Select company", options = companyOptions, optionLabel = { it.companyName.orEmpty() },
                onOptionSelected = { onFilterCompanySelected(if (it.comId > 0) it else null) }, expanded = companyExpanded, onExpandedChange = { companyExpanded = it }, modifier = Modifier.fillMaxWidth(),
                isError = !departmentValidation.isNullOrBlank() && selectedFilterCompany == null, supportingText = if (selectedFilterCompany == null) "Choose a company to load its departments." else null
            )
            BatchDropdownField(
                label = "Department", selectedValue = selectedFilterDepartment?.departmentName ?: "Select department", options = departmentOptions, optionLabel = { it.departmentName.orEmpty() },
                onOptionSelected = { onFilterDepartmentSelected(if (it.deptId > 0) it else null) }, expanded = departmentExpanded, onExpandedChange = { departmentExpanded = it }, modifier = Modifier.fillMaxWidth(),
                isError = !departmentValidation.isNullOrBlank(), supportingText = departmentValidation
            )
            if (selectedFilterDepartment != null) {
                BorrowSummaryCard(title = selectedFilterDepartment.departmentName.orEmpty(), subtitle = "$title confirmed for the whole department", lines = listOf(selectedFilterCompany?.companyName.orEmpty().ifBlank { "--" }), tone = BorrowCardTone.Success, badgeText = "Ready")
            } else {
                BorrowStateCard(title = "Department required", message = "Select the company, then choose the department responsible for this transaction.", tone = BorrowCardTone.Neutral)
            }
        }
    }
}
@Composable
private fun EmployeeAssignmentCard(
    title: String,
    employeeMode: EmployeeEntryMode,
    onEmployeeModeChange: (EmployeeEntryMode) -> Unit,
    filterCompanies: List<BorrowCompanyDto>,
    selectedFilterCompany: BorrowCompanyDto?,
    onFilterCompanySelected: (BorrowCompanyDto?) -> Unit,
    filterDepartments: List<BorrowDepartmentDto>,
    selectedFilterDepartment: BorrowDepartmentDto?,
    onFilterDepartmentSelected: (BorrowDepartmentDto?) -> Unit,
    query: String,
    onQueryChange: (String) -> Unit,
    results: List<BorrowEmployeeDto>,
    searchAttempted: Boolean,
    selectedEmployee: BorrowEmployeeDto?,
    onEmployeeSelected: (BorrowEmployeeDto) -> Unit,
    onSearch: () -> Unit,
    searching: Boolean,
    newEmployeeName: String,
    onNewEmployeeNameChange: (String) -> Unit,
    newEmployeeNumber: String,
    onNewEmployeeNumberChange: (String) -> Unit,
    newEmployeePosition: String,
    onNewEmployeePositionChange: (String) -> Unit,
    addEmployeeCompanies: List<BorrowCompanyDto>,
    selectedAddEmployeeCompany: BorrowCompanyDto?,
    onAddEmployeeCompanySelected: (BorrowCompanyDto) -> Unit,
    addEmployeeBranches: List<BorrowBranchDto>,
    selectedAddEmployeeBranch: BorrowBranchDto?,
    onAddEmployeeBranchSelected: (BorrowBranchDto) -> Unit,
    addEmployeeDepartments: List<BorrowDepartmentDto>,
    selectedAddEmployeeDepartment: BorrowDepartmentDto?,
    onAddEmployeeDepartmentSelected: (BorrowDepartmentDto) -> Unit,
    onCreateEmployee: () -> Unit,
    creatingEmployee: Boolean,
    validationMessage: String? = null
) {
    var filtersExpanded by rememberSaveable(title) { mutableStateOf(false) }
    var addFlowExpanded by rememberSaveable(title) { mutableStateOf(false) }

    Card(
        modifier = Modifier.animateContentSize(),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.32f))
    ) {
        Column(modifier = Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(title, fontWeight = FontWeight.Bold)

            Row(modifier = Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(selected = employeeMode == EmployeeEntryMode.LISTED, onClick = { onEmployeeModeChange(EmployeeEntryMode.LISTED) }, label = { Text("Listed employee") })
                FilterChip(selected = employeeMode == EmployeeEntryMode.DEPARTMENT_ONLY, onClick = { onEmployeeModeChange(EmployeeEntryMode.DEPARTMENT_ONLY) }, label = { Text("Whole department") })
                FilterChip(selected = employeeMode == EmployeeEntryMode.ADD_NEW, onClick = { onEmployeeModeChange(EmployeeEntryMode.ADD_NEW) }, label = { Text("Add employee") })
            }

            if (selectedEmployee != null) {
                SelectedEmployeeSummaryCard(
                    title = title,
                    employee = selectedEmployee
                )
            }

            if (employeeMode == EmployeeEntryMode.LISTED) {
                var companyExpanded by remember { mutableStateOf(false) }
                var departmentExpanded by remember { mutableStateOf(false) }
                val companyOptions = listOf(BorrowCompanyDto(comId = 0, companyName = "All Companies")) + filterCompanies
                val departmentOptions = listOf(BorrowDepartmentDto(deptId = 0, comId = null, departmentName = "All Departments")) + filterDepartments
                val listedSearchValidationMessage = when {
                    validationMessage.containsValidationToken("select the employee", "select the employee returning", "search at least 2 characters") -> {
                        if (validationMessage.containsValidationToken("search at least 2 characters")) {
                            "Type at least 2 characters or choose a company or department filter."
                        } else {
                            "Choose the employee for this transaction before continuing."
                        }
                    }
                    else -> null
                }

                OutlinedButton(
                    onClick = { filtersExpanded = !filtersExpanded },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        if (filtersExpanded) {
                            "Hide Filters"
                        } else {
                            "Filters: ${formatEmployeeFilterSummary(selectedFilterCompany, selectedFilterDepartment)}"
                        }
                    )
                }

                if (filtersExpanded) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        BatchDropdownField(
                            label = "Company",
                            selectedValue = selectedFilterCompany?.companyName ?: "All Companies",
                            options = companyOptions,
                            optionLabel = { it.companyName.orEmpty() },
                            onOptionSelected = { onFilterCompanySelected(if (it.comId > 0) it else null) },
                            expanded = companyExpanded,
                            onExpandedChange = { companyExpanded = it },
                            modifier = Modifier.weight(1f)
                        )
                        BatchDropdownField(
                            label = "Department",
                            selectedValue = selectedFilterDepartment?.departmentName ?: "All Departments",
                            options = departmentOptions,
                            optionLabel = { it.departmentName.orEmpty() },
                            onOptionSelected = { onFilterDepartmentSelected(if (it.deptId > 0) it else null) },
                            expanded = departmentExpanded,
                            onExpandedChange = { departmentExpanded = it },
                            modifier = Modifier.weight(1f)
                        )
                    }
                }

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        value = query,
                        onValueChange = onQueryChange,
                        modifier = Modifier.weight(1f),
                        singleLine = true,
                        label = { Text("Search employee") },
                        isError = !listedSearchValidationMessage.isNullOrBlank(),
                        supportingText = listedSearchValidationMessage?.let { { Text(it) } }
                    )
                    IconButton(onClick = onSearch, modifier = Modifier.size(48.dp).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f), CircleShape)) {
                        if (searching) CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp) else Icon(Icons.Filled.PersonSearch, contentDescription = "Search employee")
                    }
                }

                val searchStateMessage = when {
                    selectedEmployee != null -> "Employee locked in for this transaction."
                    searching -> "Searching employees..."
                    searchAttempted && results.isEmpty() -> "No match found. You can add the employee instead."
                    searchAttempted && results.isNotEmpty() -> "Choose the correct employee from the results below."
                    else -> "No employee selected yet."
                }

                EmployeeSearchStateCard(
                    message = searchStateMessage,
                    showAddEmployeeAction = !searching && searchAttempted && results.isEmpty(),
                    onAddEmployee = {
                        addFlowExpanded = true
                        onEmployeeModeChange(EmployeeEntryMode.ADD_NEW)
                    }
                )

                if (results.isNotEmpty()) {
                    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        results.take(5).forEach { employee ->
                            Card(
                                modifier = Modifier.fillMaxWidth().clickable { onEmployeeSelected(employee) },
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                                shape = RoundedCornerShape(14.dp)
                            ) {
                                Column(modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp)) {
                                    Text(employee.employeeName.orEmpty(), fontWeight = FontWeight.SemiBold)
                                    Text(
                                        "${employee.departmentName.orEmpty()} - ${employee.companyName.orEmpty()}",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }
            } else {
                var addCompanyExpanded by remember { mutableStateOf(false) }
                var addBranchExpanded by remember { mutableStateOf(false) }
                var addDepartmentExpanded by remember { mutableStateOf(false) }
                val nameValidationMessage = if (validationMessage.containsValidationToken("employee name")) {
                    "Enter the employee's full name before saving."
                } else {
                    null
                }
                val companyValidationMessage = if (validationMessage.containsValidationToken("company first")) {
                    "Choose a company before saving the employee."
                } else {
                    null
                }
                val branchValidationMessage = if (validationMessage.containsValidationToken("branch first")) {
                    "Choose a branch before saving the employee."
                } else {
                    null
                }
                val departmentValidationMessage = if (validationMessage.containsValidationToken("department first")) {
                    "Choose a department before saving the employee."
                } else {
                    null
                }

                EmployeeSearchStateCard(
                    message = "Add a new employee only when they are not yet searchable in the listed employee flow.",
                    showAddEmployeeAction = false,
                    onAddEmployee = {}
                )

                OutlinedButton(
                    onClick = { addFlowExpanded = !addFlowExpanded },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(if (addFlowExpanded) "Hide Add Employee Form" else "Open Add Employee Form")
                }

                if (addFlowExpanded) {
                    EmployeeStepCard(
                        stepLabel = "Step 1",
                        title = "Basic details",
                        subtitle = "Enter the employee profile first."
                    ) {
                        OutlinedTextField(
                            value = newEmployeeName,
                            onValueChange = onNewEmployeeNameChange,
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            label = { Text("Full name") },
                            isError = !nameValidationMessage.isNullOrBlank(),
                            supportingText = nameValidationMessage?.let { { Text(it) } }
                        )

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            OutlinedTextField(
                                value = newEmployeeNumber,
                                onValueChange = onNewEmployeeNumberChange,
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                label = { Text("Employee no.") }
                            )
                            OutlinedTextField(
                                value = newEmployeePosition,
                                onValueChange = onNewEmployeePositionChange,
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                label = { Text("Position") }
                            )
                        }
                    }

                    EmployeeStepCard(
                        stepLabel = "Step 2",
                        title = "Assign company and department",
                        subtitle = "Choose where the employee belongs before saving."
                    ) {
                        BatchDropdownField(
                            label = "Company",
                            selectedValue = selectedAddEmployeeCompany?.companyName.orEmpty(),
                            options = addEmployeeCompanies,
                            optionLabel = { it.companyName.orEmpty() },
                            onOptionSelected = onAddEmployeeCompanySelected,
                            expanded = addCompanyExpanded,
                            onExpandedChange = { addCompanyExpanded = it },
                            modifier = Modifier.fillMaxWidth(),
                            placeholder = "Select company",
                            isError = !companyValidationMessage.isNullOrBlank(),
                            supportingText = companyValidationMessage
                        )

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            BatchDropdownField(
                                label = "Branch",
                                selectedValue = selectedAddEmployeeBranch?.branchName.orEmpty(),
                                options = addEmployeeBranches,
                                optionLabel = { it.branchName.orEmpty() },
                                onOptionSelected = onAddEmployeeBranchSelected,
                                expanded = addBranchExpanded,
                                onExpandedChange = { addBranchExpanded = it },
                                modifier = Modifier.weight(1f),
                                placeholder = "Select branch",
                                isError = !branchValidationMessage.isNullOrBlank(),
                                supportingText = branchValidationMessage
                            )
                            BatchDropdownField(
                                label = "Department",
                                selectedValue = selectedAddEmployeeDepartment?.departmentName.orEmpty(),
                                options = addEmployeeDepartments,
                                optionLabel = { it.departmentName.orEmpty() },
                                onOptionSelected = onAddEmployeeDepartmentSelected,
                                expanded = addDepartmentExpanded,
                                onExpandedChange = { addDepartmentExpanded = it },
                                modifier = Modifier.weight(1f),
                                placeholder = "Select department",
                                isError = !departmentValidationMessage.isNullOrBlank(),
                                supportingText = departmentValidationMessage
                            )
                        }

                        Text(
                            text = "Current assignment: ${
                                selectedAddEmployeeCompany?.companyName?.ifBlank { "--" } ?: "--"
                            } / ${
                                selectedAddEmployeeDepartment?.departmentName?.ifBlank { "--" } ?: "--"
                            }",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    Button(
                        onClick = onCreateEmployee,
                        enabled = !creatingEmployee,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        if (creatingEmployee) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(18.dp),
                                strokeWidth = 2.dp,
                                color = LocalContentColor.current
                            )
                        } else {
                            Text("Save Employee and Continue")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SelectedEmployeeSummaryCard(title: String, employee: BorrowEmployeeDto) {
    BorrowSummaryCard(
        title = employee.employeeName.orEmpty().ifBlank { "--" },
        subtitle = "$title confirmed",
        lines = listOf(
            employee.departmentName.orEmpty().ifBlank { "--" },
            employee.companyName.orEmpty().ifBlank { "--" }
        ),
        tone = BorrowCardTone.Success,
        badgeText = "Ready"
    )
}

private fun formatEmployeeFilterSummary(
    company: BorrowCompanyDto?,
    department: BorrowDepartmentDto?
): String {
    val companyLabel = company?.companyName?.takeIf { it.isNotBlank() } ?: "All Companies"
    val departmentLabel = department?.departmentName?.takeIf { it.isNotBlank() } ?: "All Departments"
    return "$companyLabel / $departmentLabel"
}

private fun String?.containsValidationToken(vararg tokens: String): Boolean {
    val value = this ?: return false
    return tokens.any { token -> value.contains(token, ignoreCase = true) }
}

@Composable
private fun EmployeeStepCard(
    stepLabel: String,
    title: String,
    subtitle: String,
    content: @Composable () -> Unit
) {
    Card(
        modifier = Modifier.animateContentSize(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.78f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    text = stepLabel,
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary
                )
                Text(title, fontWeight = FontWeight.Bold)
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            content()
        }
    }
}

@Composable
private fun EmployeeSearchStateCard(
    message: String,
    showAddEmployeeAction: Boolean,
    onAddEmployee: () -> Unit
) {
    BorrowStateCard(
        title = "Employee Search",
        message = message,
        tone = BorrowCardTone.Neutral
    ) {
        if (showAddEmployeeAction) {
            OutlinedButton(
                onClick = onAddEmployee,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Switch to Add Employee")
            }
        }
    }
}

@Composable
private fun InfoCard(title: String, lines: List<String>) {
    BorrowStateCard(
        title = title,
        message = lines.joinToString("\n"),
        tone = BorrowCardTone.Warning
    )
}

@Composable
private fun WarningCard(title: String, text: String) {
    BorrowStateCard(title = title, message = text, tone = BorrowCardTone.Error)
}

@Composable
private fun ActionButton(text: String, loading: Boolean, enabled: Boolean, onClick: () -> Unit) {
    Button(onClick = onClick, enabled = enabled, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) {
        if (loading) {
            CircularProgressIndicator(
                modifier = Modifier.size(18.dp),
                strokeWidth = 2.dp,
                color = LocalContentColor.current
            )
        } else {
            Text(text)
        }
    }
}

private fun showDatePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    DatePickerDialog(context, { _, year, month, dayOfMonth ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.YEAR, year); updated.set(Calendar.MONTH, month); updated.set(Calendar.DAY_OF_MONTH, dayOfMonth)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH), calendar.get(Calendar.DAY_OF_MONTH)).show()
}

private fun showTimePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    TimePickerDialog(context, { _, hourOfDay, minute ->
        val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
        updated.set(Calendar.HOUR_OF_DAY, hourOfDay); updated.set(Calendar.MINUTE, minute); updated.set(Calendar.SECOND, 0); updated.set(Calendar.MILLISECOND, 0)
        onSelected(updated.timeInMillis)
    }, calendar.get(Calendar.HOUR_OF_DAY), calendar.get(Calendar.MINUTE), false).show()
}

private fun formatLocalDate(millis: Long): String = DateTimeFormatter.ofPattern("MMM d, yyyy", Locale.getDefault()).format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))
private fun formatLocalTime(millis: Long): String = DateTimeFormatter.ofPattern("h:mm a", Locale.getDefault()).format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))
private fun formatApiTimestamp(raw: String?): String = if (raw.isNullOrBlank()) "--" else try { DateTimeFormatter.ofPattern("MMM d, yyyy h:mm a", Locale.getDefault()).format(Instant.parse(raw).atZone(ZoneId.systemDefault())) } catch (_: Exception) { raw.replace('T', ' ').replace("Z", "").take(19) }