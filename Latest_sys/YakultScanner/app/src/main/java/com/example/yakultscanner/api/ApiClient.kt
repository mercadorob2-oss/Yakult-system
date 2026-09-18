package com.example.yakultscanner.api

import android.os.Handler
import android.os.Looper
import com.example.yakultscanner.BuildConfig
import com.example.yakultscanner.GlobalNav
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.data.model.DispatchSet
import com.example.yakultscanner.settings.ApiSettings
import com.google.gson.annotations.SerializedName
import okhttp3.OkHttpClient
import okhttp3.ResponseBody
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query
import retrofit2.http.Streaming
import java.util.concurrent.TimeUnit

interface YakultApiService {
    @GET("dbinfo.ashx")
    suspend fun getDatabaseInfo(): retrofit2.Response<DatabaseInfoResponse>

    @POST("mobile-auth-login.ashx")
    suspend fun login(@Body request: LoginRequest): retrofit2.Response<LoginResponse>

    @POST("api/auth/register")
    suspend fun register(@Body request: RegisterRequest): retrofit2.Response<RegisterResponse>

    @POST("api/SetUpdates/Upload")
    suspend fun uploadSetUpdates(@Body request: SetUpdatesRequest): retrofit2.Response<SetUpdatesResponse>

    @GET("api/SetUpdates")
    suspend fun getSetUpdates(@Query("setCode") setCode: String): retrofit2.Response<List<SetItemUpdateDto>>

    @GET("api/SetUpdates/PendingCount")
    suspend fun getPendingIssues(): retrofit2.Response<PendingIssuesResponse>

    @GET("api/SetUpdates/Processed")
    suspend fun getProcessedUpdates(): retrofit2.Response<List<SetItemUpdateDto>>

    @GET("api/SetUpdates/Pending")
    suspend fun getPendingUpdates(): retrofit2.Response<List<SetItemUpdateDto>>

    @POST("api/Items/CreateBatch")
    suspend fun createBatchItems(@Body request: BatchItemsRequest): retrofit2.Response<BatchItemsResponse>

    @POST("api.ashx")
    suspend fun sendSerialToWindows(
        @Body request: SerialFromMobileRequest,
        @Query("__route") route: String = "api/Items/ReceiveSerialFromMobile"
    ): retrofit2.Response<SerialFromMobileResponse>

    @GET("api/Items/Categories")
    suspend fun getItemCategories(): retrofit2.Response<List<ItemCategoryDto>>

    @GET("api/Items/Conditions")
    suspend fun getConditions(): retrofit2.Response<List<ConditionDto>>

    @GET("api/Items/Vendors")
    suspend fun getVendors(): retrofit2.Response<List<VendorDto>>

    @GET("api/sets/by-token/{token}")
    suspend fun getSetByToken(@Path("token") token: String): retrofit2.Response<DispatchSet>

    // NEW: Enhanced set endpoints
    @GET("set-history.ashx")
    suspend fun getSetDeploymentHistory(@Query("token") token: String): retrofit2.Response<SetDeploymentHistoryResponse>

    @GET("set-items-detail.ashx")
    suspend fun getSetItemsDetailed(@Query("token") token: String): retrofit2.Response<SetItemsDetailResponse>

    @POST("set-confirm.ashx")
    suspend fun confirmSetDeployment(@Query("token") token: String, @Body request: SetConfirmationRequest): retrofit2.Response<SetConfirmationResponse>

    @POST("set-image-upload.ashx")
    suspend fun uploadSetImage(@Query("token") token: String? = null, @Query("set_code") setCode: String? = null, @Body request: SetImageUploadRequest, @Query("document_number") documentNumber: String? = null): retrofit2.Response<SetImageUploadResponse>

    @GET("set-images.ashx")
    suspend fun getSetImages(@Query("token") token: String? = null, @Query("set_code") setCode: String? = null): retrofit2.Response<SetImagesResponse>

    @POST("receipt-upload.ashx")
    suspend fun uploadReceiptImage(@Query("token") token: String? = null, @Query("set_code") setCode: String? = null, @Query("document_number") documentNumber: String? = null, @Body request: ReceiptImageUploadRequest): retrofit2.Response<ReceiptImageUploadResponse>

    @PUT("api/dispatch/{setId}")
    suspend fun deploySet(@Path("setId") setId: Int): retrofit2.Response<DeployResponse>

    @GET("api/dispatch/resolve-token/{token}")
    suspend fun resolveToken(@Path("token") token: String): retrofit2.Response<ResolveTokenResponse>

    @GET("api/dispatch/status")
    suspend fun getSetStatuses(@Query("setCodes") setCodes: List<String>): retrofit2.Response<List<SetStatusDto>>

    @GET("mobile-dispatch-count.ashx")
    suspend fun getDispatchCount(): retrofit2.Response<DispatchCountResponse>

    // Standalone root handler: production IIS has a child /api application that intercepts
    // /api/Borrow before Global.asax. Keep all mobile Borrow calls beside mobile-borrow-home.
    @GET("mobile-borrow.ashx")
    suspend fun resolveBorrow(
        @Query("serial") serial: String,
        @Query("op") operation: String = "resolve"
    ): retrofit2.Response<BorrowResolveResponse>

    @GET("mobile-borrow.ashx")
    suspend fun searchBorrowItemsByModel(
        @Query("model") model: String,
        @Query("maxRows") maxRows: Int = 25,
        @Query("op") operation: String = "model"
    ): retrofit2.Response<List<BorrowItemDto>>

    @GET("mobile-borrow.ashx")
    suspend fun searchBorrowEmployees(
        @Query("q") query: String? = null,
        @Query("companyId") companyId: Int? = null,
        @Query("departmentId") departmentId: Int? = null,
        @Query("op") operation: String = "employees"
    ): retrofit2.Response<List<BorrowEmployeeDto>>

    @POST("mobile-borrow.ashx")
    suspend fun createBorrowEmployee(
        @Body request: BorrowEmployeeCreateRequest,
        @Query("op") operation: String = "employees"
    ): retrofit2.Response<BorrowEmployeeDto>

    @GET("mobile-borrow.ashx")
    suspend fun getBorrowCompanies(@Query("op") operation: String = "companies"): retrofit2.Response<List<BorrowCompanyDto>>

    @GET("mobile-borrow.ashx")
    suspend fun getBorrowBranches(
        @Query("companyId") companyId: Int,
        @Query("op") operation: String = "branches"
    ): retrofit2.Response<List<BorrowBranchDto>>

    @GET("mobile-borrow.ashx")
    suspend fun getBorrowDepartments(
        @Query("companyId") companyId: Int,
        @Query("op") operation: String = "departments"
    ): retrofit2.Response<List<BorrowDepartmentDto>>

    @POST("mobile-borrow.ashx")
    suspend fun borrowItem(
        @Body request: BorrowCreateRequest,
        @Query("op") operation: String = "borrow"
    ): retrofit2.Response<BorrowActionResponse>

    @POST("mobile-borrow.ashx")
    suspend fun returnBorrow(
        @Body request: BorrowReturnRequest,
        @Query("op") operation: String = "return"
    ): retrofit2.Response<BorrowActionResponse>

    @POST("mobile-borrow.ashx")
    suspend fun deleteBorrow(
        @Body request: BorrowDeleteRequest,
        @Query("op") operation: String = "delete"
    ): retrofit2.Response<BorrowActionResponse>

    @GET("mobile-borrow.ashx")
    suspend fun getOpenBorrows(
        @Query("serialContains") serialContains: String? = null,
        @Query("pageIndex") pageIndex: Int = 0,
        @Query("pageSize") pageSize: Int = 25,
        @Query("op") operation: String = "open"
    ): retrofit2.Response<BorrowLogPageResponse>

    @GET("mobile-borrow-home.ashx")
    suspend fun getBorrowHomeSummary(
        @Query("recentCount") recentCount: Int = 5
    ): retrofit2.Response<BorrowHomeSummaryResponse>

    @GET("mobile-borrow.ashx")
    suspend fun getBorrowHistory(
        @Query("serialContains") serialContains: String? = null,
        @Query("pageIndex") pageIndex: Int = 0,
        @Query("pageSize") pageSize: Int = 25,
        @Query("fromUtc") fromUtc: String? = null,
        @Query("toUtc") toUtc: String? = null,
        @Query("op") operation: String = "history"
    ): retrofit2.Response<BorrowLogPageResponse>

    // Repair Portal — mobile Part-photo upload. Standalone handlers (NOT routed through
    // "api/..." — that path is claimed by a separate IIS application on the deployed server and
    // never reaches api.ashx's router). JWT-authenticated (the Bearer interceptor below attaches
    // it automatically), unlike the earlier open/QR-token-gated read-only endpoints, since
    // UploadPartPhoto permanently writes files.
    @GET("repair-ticket-lookup.ashx")
    suspend fun lookupRepairTicket(
        @Query("ticketCode") ticketCode: String? = null,
        @Query("token") token: String? = null
    ): retrofit2.Response<RepairTicketLookupResponse>

    @POST("repair-part-photo-upload.ashx")
    suspend fun uploadRepairPartPhoto(@Body request: RepairPartPhotoUploadRequest): retrofit2.Response<RepairPartPhotoUploadResponse>

    // Repair Portal — shared desktop records, requester scope and technician queue.
    @GET("repair-tickets.ashx")
    suspend fun getRepairTickets(
        @Query("scope") scope: String = "mine",
        @Query("status") status: String? = null,
        @Query("priority") priority: String? = null,
        @Query("search") search: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 25
    ): retrofit2.Response<RepairTicketListResponse>

    @POST("repair-tickets.ashx")
    suspend fun createRepairTicket(@Body request: CreateRepairTicketRequest): retrofit2.Response<CreateRepairTicketResponse>

    @GET("repair-ticket-mobile-detail.ashx")
    suspend fun getRepairTicketDetail(@Query("ticketId") ticketId: Int): retrofit2.Response<RepairTicketDetailResponse>

    @POST("repair-ticket-action.ashx")
    suspend fun repairTicketAction(@Body request: RepairTicketActionRequest): retrofit2.Response<RepairActionResponse>

    @GET("repair-lookups.ashx")
    suspend fun getRepairLookups(
        @Query("kind") kind: String,
        @Query("query") query: String? = null,
        @Query("companyId") companyId: Int? = null,
        @Query("departmentId") departmentId: Int? = null,
        @Query("branchId") branchId: Int? = null
    ): retrofit2.Response<RepairLookupResponse>

    @GET("repair-forward-items.ashx")
    suspend fun getRepairForwardItems(
        @Query("ticketId") ticketId: Int,
        @Query("query") query: String? = null
    ): retrofit2.Response<RepairForwardItemsResponse>

    @POST("repair-forward-preview.ashx")
    suspend fun previewRepairForward(@Body request: RepairForwardRequest): retrofit2.Response<RepairForwardPreviewResponse>

    @POST("repair-forward-create.ashx")
    suspend fun createRepairForward(@Body request: RepairForwardRequest): retrofit2.Response<RepairForwardCreateResponse>

    @POST("repair-ticket-evidence-upload.ashx")
    suspend fun uploadRepairTicketEvidence(@Body request: RepairTicketEvidenceUploadRequest): retrofit2.Response<RepairTicketEvidenceUploadResponse>

    @GET("repair-attendance.ashx")
    suspend fun getRepairAttendance(): retrofit2.Response<RepairAttendanceResponse>

    @GET("repair-reports.ashx")
    suspend fun getRepairReports(
        @Query("pageSize") pageSize: Int = 20
    ): retrofit2.Response<RepairReportsResponse>

    @GET("repair-ticket-qr.ashx")
    suspend fun getRepairTicketQr(@Query("ticketId") ticketId: Int): retrofit2.Response<RepairTicketQrResponse>

    @GET("repair-report-signatures.ashx")
    suspend fun getRepairReportSignatures(@Query("ticketId") ticketId: Int): retrofit2.Response<RepairReportSignaturesResponse>

    @POST("repair-report-signatures.ashx")
    suspend fun postRepairReportSignature(@Body request: RepairReportSignatureRequest): retrofit2.Response<RepairReportSignaturesResponse>

    @GET("repair-report-pdf.ashx")
    suspend fun getRepairReportPreview(
        @Query("ticketIds") ticketIds: String,
        @Query("includeAttachments") includeAttachments: Boolean = true
    ): retrofit2.Response<RepairReportPreviewResponse>

    @POST("repair-report-pdf.ashx")
    suspend fun postRepairReportPreview(@Body request: RepairReportPreviewRequest): retrofit2.Response<RepairReportPreviewResponse>

    @Streaming
    @GET("repair-evidence-download.ashx")
    suspend fun downloadRepairEvidence(
        @Query("attachmentId") attachmentId: Int? = null,
        @Query("partAttachmentId") partAttachmentId: Int? = null
    ): retrofit2.Response<ResponseBody>

    @GET("api.ashx")
    suspend fun getReportsSummary(
        @Query("range") range: String,
        @Query("__route") route: String = "api/Reports/Summary"
    ): retrofit2.Response<ReportsSummaryResponse>

    @GET("api.ashx")
    suspend fun getReportModuleDetail(
        @Query("range") range: String,
        @Query("__route") route: String
    ): retrofit2.Response<ReportModuleDetailResponse>

    @GET("slookup.ashx")
    suspend fun serialLookup(@Query("serial") serial: String): retrofit2.Response<SerialLookupResponse>

    @GET("item-movement.ashx")
    suspend fun getItemMovement(@Query("itemId") itemId: Int): retrofit2.Response<ItemMovementResponse>

    // ── IT Call Monitoring ──
    @GET("call-tickets.ashx")
    suspend fun getCallTickets(
        @Query("status") status: String? = null,
        @Query("scope") scope: String? = null,
        @Query("search") search: String? = null,
        @Query("priority") priority: String? = null,
        @Query("issueType") issueType: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 25
    ): retrofit2.Response<CallTicketListResponse>

    @GET("call-ticket-detail.ashx")
    suspend fun getCallTicketDetail(@Query("ticketId") ticketId: Int): retrofit2.Response<CallTicketDetailResponse>

    @POST("call-tickets.ashx")
    suspend fun createCallTicket(@Body request: CreateTicketRequest): retrofit2.Response<CallTicketCreateResponse>

    @POST("call-ticket-action.ashx")
    suspend fun callTicketAction(@Body request: CallTicketActionRequest): retrofit2.Response<CallTicketActionResponse>

    @POST("call-ticket-action.ashx")
    suspend fun callTicketResolution(@Body request: ResolutionRequest): retrofit2.Response<CallTicketActionResponse>

    @GET("call-items-lookup.ashx")
    suspend fun getCallItemsLookup(
        @Query("type") type: String
    ): retrofit2.Response<CallItemLookupResponse>

    @GET("call-conditions.ashx")
    suspend fun getCallConditions(): retrofit2.Response<CallConditionResponse>

    // ── IT Call Monitoring Lookups ──
    @GET("call-companies.ashx")
    suspend fun getCallCompanies(): retrofit2.Response<CallLookupResponse>

    @GET("call-departments.ashx")
    suspend fun getCallDepartments(
        @Query("comId") comId: Int? = null
    ): retrofit2.Response<CallLookupResponse>

    @GET("call-branches.ashx")
    suspend fun getCallBranches(
        @Query("comId") comId: Int? = null,
        @Query("deptId") deptId: Int? = null
    ): retrofit2.Response<CallLookupResponse>

    @GET("call-employees.ashx")
    suspend fun getCallEmployees(
        @Query("deptId") deptId: Int,
        @Query("comId") comId: Int? = null,
        @Query("branchId") branchId: Int? = null
    ): retrofit2.Response<CallLookupResponse>

    @GET("call-employee-search.ashx")
    suspend fun searchCallEmployees(
        @Query("q") q: String,
        @Query("maxResults") maxResults: Int? = null
    ): retrofit2.Response<CallLookupResponse>

    @GET("call-employee-org.ashx")
    suspend fun getCallEmployeeOrg(
        @Query("empId") empId: Int
    ): retrofit2.Response<EmployeeOrgResponse>

    @GET("call-it-employees.ashx")
    suspend fun getCallItEmployees(): retrofit2.Response<CallLookupResponse>

    @GET("call-escalation-settings.ashx")
    suspend fun getEscalationSettings(): retrofit2.Response<EscalationSettingsResponse>

    @POST("call-set-escalation-override.ashx")
    suspend fun setEscalationOverride(@Body request: SetEscalationOverrideRequest): retrofit2.Response<SetEscalationOverrideResponse>

    @GET("dispatch-set.ashx")
    suspend fun getDispatchSet(
        @Query("setCode") setCode: String? = null,
        @Query("token") token: String? = null
    ): retrofit2.Response<DispatchSetResponse>

    @GET("call-dashboard.ashx")
    suspend fun getCallDashboard(): retrofit2.Response<CallDashboardResponse>

    // ── Call Field Work ──
    @GET("call-field-visits.ashx")
    suspend fun getCallFieldVisits(@Query("ticketId") ticketId: Int): retrofit2.Response<CallFieldVisitsResponse>

    @GET("call-field-visits.ashx")
    suspend fun getCallFieldVisitList(
        @Query("status") status: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 25
    ): retrofit2.Response<CallFieldVisitsResponse>

    @POST("call-field-visits.ashx")
    suspend fun scheduleCallFieldVisit(@Body request: ScheduleFieldVisitRequest): retrofit2.Response<CallFieldVisitsResponse>

    @POST("call-field-visits.ashx")
    suspend fun fieldVisitAction(@Body request: FieldVisitActionRequest): retrofit2.Response<CallFieldVisitsResponse>

    @POST("call-field-visit-photo-upload.ashx")
    suspend fun uploadFieldVisitPhoto(@Body request: FieldVisitPhotoUploadRequest): retrofit2.Response<CallFieldVisitsResponse>

    @GET("call-field-visit-photo.ashx")
    suspend fun getFieldVisitPhoto(@Query("attachmentId") attachmentId: Int, @Query("thumb") thumb: Int? = null): retrofit2.Response<CallFieldVisitPhotoResponse>

    @GET("call-field-visit-signature.ashx")
    suspend fun getFieldVisitSignature(@Query("fieldVisitId") fieldVisitId: Int): retrofit2.Response<CallFieldVisitSignatureResponse>
}

data class LoginRequest(
    @SerializedName("username") val username: String,
    @SerializedName("password") val password: String
)

data class RegisterRequest(
    @SerializedName("username") val username: String,
    @SerializedName("email") val email: String? = null,
    @SerializedName("password") val password: String
)

data class LoginResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String,
    @SerializedName("token") val token: String? = null,
    @SerializedName("expiresUtc") val expiresUtc: String? = null,
    @SerializedName("userId") val userId: Int = 0,
    @SerializedName("name") val name: String? = null,
    @SerializedName("email") val email: String? = null
)

data class ReceiptImageUploadRequest(
    @SerializedName("doc_type") val docType: String,
    @SerializedName("supplier") val supplier: String? = null,
    @SerializedName("si_number") val siNumber: String? = null,
    @SerializedName("dr_number") val drNumber: String? = null,
    @SerializedName("po_number") val poNumber: String? = null,
    @SerializedName("image_base64") val imageBase64: String,
    @SerializedName("receipt_set_id") val receiptSetId: Int? = null,
    @SerializedName("mime_type") val mimeType: String? = null
)

data class ReceiptImageUploadResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String? = null,
    @SerializedName("receipt_set_id") val receiptSetId: Int = 0,
    @SerializedName("image_id") val imageId: Int = 0,
    @SerializedName("set_code") val setCode: String? = null,
    @SerializedName("doc_type") val docType: String? = null
)

data class RegisterResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String,
    @SerializedName("userId") val userId: Int = 0
)

data class BatchItemsResponse(
    @SerializedName("Success") val success: Boolean,
    @SerializedName("Message") val message: String,
    @SerializedName("CreatedCount") val createdCount: Int,
    @SerializedName("FailedItems") val failedItems: List<String>? = null
)

data class SerialFromMobileRequest(
    val serialNumber: String,
    val cellPhoneNumber: String? = null,
    val imei1: String? = null,
    val imei2: String? = null
)

data class SerialFromMobileResponse(
    val success: Boolean,
    val message: String,
    val serialNumber: String
)

object ApiClient {
    @Volatile
    private var cachedBaseUrl: String? = null

    @Volatile
    private var cachedService: YakultApiService? = null

    private fun buildClient(): OkHttpClient {
        val logging = HttpLoggingInterceptor().apply {
            // Avoid logging credentials/tokens. BASIC still logs method/URL/status, which is useful for QA.
            level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.BASIC else HttpLoggingInterceptor.Level.NONE
            redactHeader("Authorization")
        }

        return OkHttpClient.Builder()
            .addInterceptor { chain ->
                val original = chain.request()
                val token = UserSession.authToken
                val request = if (!token.isNullOrBlank()) {
                    original.newBuilder()
                        .addHeader("Authorization", "Bearer $token")
                        .build()
                } else {
                    original
                }
                chain.proceed(request)
            }
            .addInterceptor { chain ->
                val response = chain.proceed(chain.request())
                if (response.code == 401 &&
                    shouldAutoLogoutForUnauthorized(chain.request().url.encodedPath) &&
                    UserSession.isLoggedIn()) {
                    UserSession.logout()
                    Handler(Looper.getMainLooper()).post {
                        GlobalNav.navController?.navigate("login") {
                            popUpTo(0) { inclusive = true }
                        }
                    }
                }
                response
            }
            .addInterceptor(logging)
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .writeTimeout(30, TimeUnit.SECONDS)
            .callTimeout(35, TimeUnit.SECONDS)
            .build()
    }

    private fun shouldAutoLogoutForUnauthorized(path: String): Boolean {
        val normalized = path.lowercase()
        val token = UserSession.authToken.orEmpty()

        if (token.startsWith("offline_", ignoreCase = true)) {
            return false
        }

        if (normalized.contains("mobile-auth-login") ||
            normalized.contains("auth/login")) {
            return false
        }

        return !(normalized.contains("/api/borrow") ||
            normalized.contains("/api/reports"))
    }

    private fun buildService(baseUrl: String): YakultApiService {
        return Retrofit.Builder()
            .baseUrl(baseUrl)
            .client(buildClient())
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(YakultApiService::class.java)
    }

    val service: YakultApiService
        get() {
            val currentBase = ApiSettings.apiBaseUrl
            val existing = cachedService
            if (existing != null && currentBase == cachedBaseUrl) {
                return existing
            }
            synchronized(this) {
                val checkExisting = cachedService
                if (checkExisting != null && currentBase == cachedBaseUrl) {
                    return checkExisting
                }
                val newService = buildService(currentBase)
                cachedBaseUrl = currentBase
                cachedService = newService
                return newService
            }
        }

    fun clearCachedService() {
        synchronized(this) {
            cachedService = null
            cachedBaseUrl = null
        }
    }
}

data class HealthResponse(
    @SerializedName("ok") val ok: Boolean,
    @SerializedName("utc") val utc: String? = null,
    @SerializedName("database") val database: String? = null
)

data class DeployResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String,
    @SerializedName("error") val error: String? = null
)

data class ResolveTokenResponse(
    @SerializedName("setId") val setId: Int
)

data class SetStatusDto(
    @SerializedName("set_code") val setCode: String?,
    @SerializedName("status") val status: String?
)

data class BorrowResolveResponse(
    @SerializedName("item") val item: BorrowItemDto? = null,
    @SerializedName("openBorrow") val openBorrow: BorrowLogDto? = null,
    @SerializedName("canBorrow") val canBorrow: Boolean? = null,
    @SerializedName("eligibilityCode") val eligibilityCode: String? = null,
    @SerializedName("eligibilityMessage") val eligibilityMessage: String? = null
)

data class BorrowItemDto(
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("itemDescription") val itemDescription: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null
)

data class BorrowEmployeeDto(
    @SerializedName("empId") val empId: Int = 0,
    @SerializedName("comId") val comId: Int = 0,
    @SerializedName("employeeName") val employeeName: String? = null,
    @SerializedName("companyName") val companyName: String? = null,
    @SerializedName("branchId") val branchId: Int = 0,
    @SerializedName("branchName") val branchName: String? = null,
    @SerializedName("deptId") val deptId: Int = 0,
    @SerializedName("departmentName") val departmentName: String? = null
)

data class BorrowCompanyDto(
    @SerializedName("comId") val comId: Int = 0,
    @SerializedName("companyName") val companyName: String? = null
)

data class BorrowBranchDto(
    @SerializedName("branchId") val branchId: Int = 0,
    @SerializedName("comId") val comId: Int? = null,
    @SerializedName("branchName") val branchName: String? = null
)

data class BorrowDepartmentDto(
    @SerializedName("deptId") val deptId: Int = 0,
    @SerializedName("comId") val comId: Int? = null,
    @SerializedName("departmentName") val departmentName: String? = null
)

data class BorrowLogDto(
    @SerializedName("borrowId") val borrowId: Int = 0,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("itemDescription") val itemDescription: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("borrowedByEmpId") val borrowedByEmpId: Int? = null,
    @SerializedName("borrowedByEmpName") val borrowedByEmpName: String? = null,
    @SerializedName("borrowedByDeptId") val borrowedByDeptId: Int? = null,
    @SerializedName("borrowedByDeptName") val borrowedByDeptName: String? = null,
    @SerializedName("borrowEncodedByUserId") val borrowEncodedByUserId: Int? = null,
    @SerializedName("borrowEncodedByUserName") val borrowEncodedByUserName: String? = null,
    @SerializedName("borrowedAtUtc") val borrowedAtUtc: String? = null,
    @SerializedName("returnedByEmpId") val returnedByEmpId: Int? = null,
    @SerializedName("returnedByEmpName") val returnedByEmpName: String? = null,
    @SerializedName("returnedByDeptId") val returnedByDeptId: Int? = null,
    @SerializedName("returnedByDeptName") val returnedByDeptName: String? = null,
    @SerializedName("returnEncodedByUserId") val returnEncodedByUserId: Int? = null,
    @SerializedName("returnEncodedByUserName") val returnEncodedByUserName: String? = null,
    @SerializedName("returnedAtUtc") val returnedAtUtc: String? = null,
    @SerializedName("isOpen") val isOpen: Boolean = false
)

data class BorrowCreateRequest(
    @SerializedName("clientRequestId") val clientRequestId: String? = null,
    @SerializedName("serialNumber") val serialNumber: String,
    @SerializedName("borrowedByEmpId") val borrowedByEmpId: Int? = null,
    @SerializedName("borrowedByDeptId") val borrowedByDeptId: Int? = null,
    @SerializedName("borrowedByDeptName") val borrowedByDeptName: String? = null,
    @SerializedName("borrowedAtUtc") val borrowedAtUtc: String? = null
)

data class BorrowReturnRequest(
    @SerializedName("clientRequestId") val clientRequestId: String? = null,
    @SerializedName("borrowId") val borrowId: Int,
    @SerializedName("returnedByEmpId") val returnedByEmpId: Int? = null,
    @SerializedName("returnedByDeptId") val returnedByDeptId: Int? = null,
    @SerializedName("returnedByDeptName") val returnedByDeptName: String? = null
)

data class BorrowDeleteRequest(
    @SerializedName("clientRequestId") val clientRequestId: String? = null,
    @SerializedName("borrowId") val borrowId: Int
)

data class BorrowEmployeeCreateRequest(
    @SerializedName("employeeName") val employeeName: String,
    @SerializedName("employeeNumber") val employeeNumber: String? = null,
    @SerializedName("position") val position: String? = null,
    @SerializedName("comId") val comId: Int,
    @SerializedName("branchId") val branchId: Int,
    @SerializedName("deptId") val deptId: Int
)

data class BorrowActionResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null,
    @SerializedName("borrow") val borrow: BorrowLogDto? = null
)

data class BorrowAccessDto(
    @SerializedName("canDeleteOpenBorrow") val canDeleteOpenBorrow: Boolean = false,
    @SerializedName("canExportCsv") val canExportCsv: Boolean = false
)

data class BorrowLogPageResponse(
    @SerializedName("totalCount") val totalCount: Int = 0,
    @SerializedName("oldestBorrowedAtUtc") val oldestBorrowedAtUtc: String? = null,
    @SerializedName("rows") val rows: List<BorrowLogDto> = emptyList(),
    @SerializedName("access") val access: BorrowAccessDto? = null
)

data class DispatchCountResponse(
    @SerializedName("todayCount") val todayCount: Int = 0
)

data class BorrowHomeSummaryResponse(
    @SerializedName("openCount") val openCount: Int = 0,
    @SerializedName("overdueCount") val overdueCount: Int = 0,
    @SerializedName("returnedTodayCount") val returnedTodayCount: Int = 0,
    @SerializedName("oldestOpenBorrowedAtUtc") val oldestOpenBorrowedAtUtc: String? = null,
    @SerializedName("recentRows") val recentRows: List<BorrowLogDto> = emptyList(),
    @SerializedName("access") val access: BorrowAccessDto? = null
)

// NEW: Enhanced Set/QR DTOs
data class SetDeploymentHistoryResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("deployment_history") val deploymentHistory: List<DeploymentHistoryDto> = emptyList()
)

data class DeploymentHistoryDto(
    @SerializedName("deployed_at") val deployedAt: String? = null,
    @SerializedName("deployed_by") val deployedBy: String? = null,
    @SerializedName("previous_status") val previousStatus: String? = null,
    @SerializedName("new_status") val newStatus: String? = null,
    @SerializedName("deployment_location") val deploymentLocation: String? = null,
    @SerializedName("device_id") val deviceId: String? = null
)

data class SetItemsDetailResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("set_code") val setCode: String? = null,
    @SerializedName("metadata") val metadata: SetMetadataDto? = null,
    @SerializedName("items") val items: List<DetailedDispatchItemDto> = emptyList()
)

data class SetMetadataDto(
    @SerializedName("item_count") val itemCount: Int = 0,
    @SerializedName("priority") val priority: String? = "Normal",
    @SerializedName("delivery_notes") val deliveryNotes: String? = null,
    @SerializedName("expected_delivery_utc") val expectedDeliveryUtc: String? = null
)

data class DetailedDispatchItemDto(
    @SerializedName("item_id") val itemId: Int = 0,
    @SerializedName("item_type") val itemType: String? = null,
    @SerializedName("item_category") val itemCategory: String? = null,
    @SerializedName("quantity") val quantity: Int = 0,
    @SerializedName("description") val description: String? = null,
    @SerializedName("model_number") val modelNumber: String? = null,
    @SerializedName("serial_number") val serialNumber: String? = null,
    @SerializedName("item_status") val itemStatus: String? = null,
    @SerializedName("computer_name") val computerName: String? = null,
    @SerializedName("ip_address") val ipAddress: String? = null,
    @SerializedName("item_condition") val itemCondition: String? = null,
    @SerializedName("repair_count") val repairCount: Int = 0,
    @SerializedName("last_repair_action") val lastRepairAction: String? = null
)

data class SetConfirmationRequest(
    @SerializedName("signature_base64") val signatureBase64: String? = null,
    @SerializedName("photo_base64") val photoBase64: String? = null,
    @SerializedName("gps_coordinates") val gpsCoordinates: String? = null,
    @SerializedName("device_id") val deviceId: String? = null,
    @SerializedName("confirmed_by") val confirmedBy: String? = null,
    @SerializedName("notes") val notes: String? = null
)

data class SetConfirmationResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String? = null,
    @SerializedName("confirmed_at") val confirmedAt: String? = null,
    @SerializedName("confirmation_id") val confirmationId: String? = null
)

data class SetImageUploadRequest(
    @SerializedName("image_base64") val imageBase64: String,
    @SerializedName("image_type") val imageType: String? = "MobileUpload",
    @SerializedName("uploaded_by") val uploadedBy: String? = null,
    @SerializedName("mime_type") val mimeType: String? = null
)

data class SetImageUploadResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String? = null,
    @SerializedName("image_id") val imageId: String? = null,
    @SerializedName("uploaded_at") val uploadedAt: String? = null
)

data class SetImagesResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("set_code") val setCode: String? = null,
    @SerializedName("image_count") val imageCount: Int = 0,
    @SerializedName("images") val images: List<SetImageDto> = emptyList()
)

data class SetImageDto(
    @SerializedName("image_id") val imageId: Int,
    @SerializedName("set_id") val setId: Int,
    @SerializedName("image_path") val imagePath: String? = null,
    @SerializedName("image_type") val imageType: String? = null,
    @SerializedName("uploaded_by") val uploadedBy: String? = null,
    @SerializedName("upload_date") val uploadDate: String? = null
)

data class RepairTicketLookupResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null,
    @SerializedName("ticket") val ticket: RepairTicketSummaryDto? = null,
    @SerializedName("parts") val parts: List<RepairPartSummaryDto> = emptyList()
)

data class RepairTicketSummaryDto(
    @SerializedName("repairTicketId") val repairTicketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("problem") val problem: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("requesterName") val requesterName: String? = null,
    @SerializedName("assignedTechnicianName") val assignedTechnicianName: String? = null,
    @SerializedName("dateReceived") val dateReceived: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("linkedCallTicketId") val linkedCallTicketId: Int? = null,
    @SerializedName("linkedCallTicketCode") val linkedCallTicketCode: String? = null,
    @SerializedName("linkedCallStatus") val linkedCallStatus: String? = null,
    // 50 MB per-ticket evidence cap (whole-equipment + every Part combined) — same figure the
    // desktop app enforces, so both sides agree on how much room is left before uploading.
    @SerializedName("evidenceBytesUsed") val evidenceBytesUsed: Long = 0L,
    @SerializedName("evidenceBytesLimit") val evidenceBytesLimit: Long = 50L * 1024 * 1024
)

data class RepairPartSummaryDto(
    @SerializedName("repairPartId") val repairPartId: Int = 0,
    @SerializedName("partNumber") val partNumber: Int = 0,
    @SerializedName("partDisplayName") val partDisplayName: String? = null,
    @SerializedName("status") val status: String? = null
)

data class RepairPartPhotoUploadRequest(
    @SerializedName("repair_part_id") val repairPartId: Int,
    @SerializedName("image_base64") val imageBase64: String,
    @SerializedName("mime_type") val mimeType: String? = "image/jpeg",
    @SerializedName("file_name") val fileName: String? = null
)

data class RepairPartPhotoUploadResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null,
    @SerializedName("partAttachmentId") val partAttachmentId: Int? = null,
    @SerializedName("evidenceBytesUsed") val evidenceBytesUsed: Long? = null,
    @SerializedName("evidenceBytesLimit") val evidenceBytesLimit: Long? = null
)

// Convenience functions to call API from screens
suspend fun deploySet(setId: Int): retrofit2.Response<DeployResponse> {
    return ApiClient.service.deploySet(setId)
}

suspend fun resolveToken(token: String): retrofit2.Response<ResolveTokenResponse> {
    return ApiClient.service.resolveToken(token)
}

suspend fun uploadSetImage(token: String?, setCode: String?, request: SetImageUploadRequest): retrofit2.Response<SetImageUploadResponse> {
    return ApiClient.service.uploadSetImage(token, setCode, request)
}

suspend fun getSetImages(token: String?, setCode: String?): retrofit2.Response<SetImagesResponse> {
    return ApiClient.service.getSetImages(token, setCode)
}

data class SerialLookupItemDto(
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("cellPhoneNumber") val cellPhoneNumber: String? = null,
    @SerializedName("imei1") val imei1: String? = null,
    @SerializedName("imei2") val imei2: String? = null,
    @SerializedName("name") val name: String? = null,
    @SerializedName("description") val description: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("itemType") val itemType: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("condition") val condition: String? = null,
    @SerializedName("warrantyStartDate") val warrantyStartDate: String? = null,
    @SerializedName("warrantyEndDate") val warrantyEndDate: String? = null,
    @SerializedName("warrantyStatus") val warrantyStatus: String? = null,
    @SerializedName("active") val active: Boolean = true
)

data class SerialLookupSetDto(
    @SerializedName("setId") val setId: Int = 0,
    @SerializedName("setCode") val setCode: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("dispatchDate") val dispatchDate: String? = null,
    @SerializedName("remarks") val remarks: String? = null,
    @SerializedName("site") val site: String? = null,
    @SerializedName("qrToken") val qrToken: String? = null,
    @SerializedName("currentBranch") val currentBranch: String? = null,
    @SerializedName("currentDepartment") val currentDepartment: String? = null
)

data class SerialLookupResponse(
    @SerializedName("found") val found: Boolean = false,
    @SerializedName("item") val item: SerialLookupItemDto? = null,
    @SerializedName("isInSet") val isInSet: Boolean = false,
    @SerializedName("sets") val sets: List<SerialLookupSetDto> = emptyList()
)

data class ItemMovementResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("movement") val movement: List<ItemMovementEntryDto> = emptyList()
)

data class ItemMovementEntryDto(
    @SerializedName("entryType") val entryType: String? = null,
    @SerializedName("quantity") val quantity: Int = 0,
    @SerializedName("datePosted") val datePosted: String? = null,
    @SerializedName("description") val description: String? = null,
    @SerializedName("postedBy") val postedBy: String? = null,
    @SerializedName("employee") val employee: String? = null,
    @SerializedName("branch") val branch: String? = null,
    @SerializedName("department") val department: String? = null,
    @SerializedName("setCode") val setCode: String? = null,
    @SerializedName("reqStatus") val reqStatus: String? = null
)

data class CallTicketListItem(
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("company") val company: String? = null,
    @SerializedName("issue") val issue: String? = null,
    @SerializedName("department") val department: String? = null,
    @SerializedName("branch") val branch: String? = null,
    @SerializedName("responsiblePerson") val responsiblePerson: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("callerName") val callerName: String? = null,
    @SerializedName("issueType") val issueType: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("solvedAt") val solvedAt: String? = null,
    @SerializedName("ticketAgeDays") val ticketAgeDays: Int = 0,
    @SerializedName("idleDays") val idleDays: Int = 0,
    @SerializedName("lastContactAt") val lastContactAt: String? = null
)

data class CallTicketDetailResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("ticket") val ticket: CallTicketDetail? = null,
    @SerializedName("notes") val notes: List<CallTicketNoteDto> = emptyList(),
    @SerializedName("history") val history: List<CallTicketHistoryDto> = emptyList()
)

data class CallTicketDetail(
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("company") val company: String? = null,
    @SerializedName("department") val department: String? = null,
    @SerializedName("branch") val branch: String? = null,
    @SerializedName("callerName") val callerName: String? = null,
    @SerializedName("issue") val issue: String? = null,
    @SerializedName("providedSolution") val providedSolution: String? = null,
    @SerializedName("issueType") val issueType: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("assignedTo") val assignedTo: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("solvedAt") val solvedAt: String? = null
)

data class CallTicketNoteDto(
    @SerializedName("noteId") val noteId: Int = 0,
    @SerializedName("noteType") val noteType: String? = null,
    @SerializedName("noteText") val noteText: String? = null,
    @SerializedName("createdBy") val createdBy: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null
)

data class CallTicketHistoryDto(
    @SerializedName("historyId") val historyId: Int = 0,
    @SerializedName("fieldName") val fieldName: String? = null,
    @SerializedName("oldValue") val oldValue: String? = null,
    @SerializedName("newValue") val newValue: String? = null,
    @SerializedName("changedBy") val changedBy: String? = null,
    @SerializedName("changedAt") val changedAt: String? = null
)

// ── IT Call Monitoring Response/Request DTOs ──

data class CallTicketListResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("tickets") val tickets: List<CallTicketListItem> = emptyList(),
    @SerializedName("totalCount") val totalCount: Int = 0,
    @SerializedName("page") val page: Int = 1,
    @SerializedName("pageSize") val pageSize: Int = 25
)

data class CallTicketCreateResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("ticket") val ticket: CallTicketListItem? = null
)

data class CallTicketActionResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null,
    @SerializedName("repairTicket") val repairTicket: RepairForwardLinkedTicketDto? = null,
    @SerializedName("parentOutcome") val parentOutcome: String? = null
)

data class ResolutionRequest(
    @SerializedName("action") val action: String = "resolution",
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("resolutionType") val resolutionType: String,
    @SerializedName("remarks") val remarks: String? = null,
    @SerializedName("userId") val userId: Int? = null,
    @SerializedName("isTemporary") val isTemporary: Boolean? = null,
    @SerializedName("useUnlistedOldItem") val useUnlistedOldItem: Boolean? = null,
    @SerializedName("oldItemId") val oldItemId: Int? = null,
    @SerializedName("newItemId") val newItemId: Int? = null,
    @SerializedName("quantity") val quantity: Int? = null,
    @SerializedName("oldItemConditionId") val oldItemConditionId: Int? = null,
    @SerializedName("oldItemConditionRemarks") val oldItemConditionRemarks: String? = null,
    @SerializedName("oldItemRepairAction") val oldItemRepairAction: String? = null,
    @SerializedName("unlistedOldItemName") val unlistedOldItemName: String? = null,
    @SerializedName("unlistedOldItemDescription") val unlistedOldItemDescription: String? = null,
    @SerializedName("unlistedOldItemCategoryId") val unlistedOldItemCategoryId: Int? = null,
    @SerializedName("unlistedOldItemCategoryName") val unlistedOldItemCategoryName: String? = null,
    @SerializedName("unlistedOldItemSerialNumber") val unlistedOldItemSerialNumber: String? = null,
    @SerializedName("unlistedOldItemModelNumber") val unlistedOldItemModelNumber: String? = null,
    @SerializedName("unlistedOldItemUnitOfMeasure") val unlistedOldItemUnitOfMeasure: String? = null,
    @SerializedName("forwardedRepairTicketId") val forwardedRepairTicketId: Int? = null,
    @SerializedName("forwardedOldItemId") val forwardedOldItemId: Int? = null,
    @SerializedName("parentOutcome") val parentOutcome: String? = null
)

data class CallItemLookupDto(
    @SerializedName("itemId") val itemId: Int,
    @SerializedName("displayText") val displayText: String,
    @SerializedName("category") val category: String?,
    @SerializedName("stockOnHand") val stockOnHand: Int?
)

data class CallItemLookupResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("items") val items: List<CallItemLookupDto>
)

data class CallConditionDto(
    @SerializedName("conditionId") val conditionId: Int,
    @SerializedName("conditionName") val conditionName: String
)

data class CallConditionResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("conditions") val conditions: List<CallConditionDto>
)

data class CreateTicketRequest(
    @SerializedName("company") val company: String,
    @SerializedName("callerName") val callerName: String,
    @SerializedName("contactEmail") val contactEmail: String? = null,
    @SerializedName("issue") val issue: String,
    @SerializedName("comId") val comId: Int? = null,
    @SerializedName("deptId") val deptId: Int? = null,
    @SerializedName("branchId") val branchId: Int? = null,
    @SerializedName("providedSolution") val providedSolution: String? = null,
    @SerializedName("issueType") val issueType: String = "Other",
    @SerializedName("priority") val priority: String = "Medium",
    @SerializedName("assignedToEmpId") val assignedToEmpId: Int? = null,
    @SerializedName("createdByUserId") val createdByUserId: Int? = null,
    @SerializedName("ticketSource") val ticketSource: String = "CallIT"
)

data class CallTicketActionRequest(
    @SerializedName("action") val action: String,
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("newStatus") val newStatus: String? = null,
    @SerializedName("newPriority") val newPriority: String? = null,
    @SerializedName("noteType") val noteType: String = "Internal",
    @SerializedName("noteText") val noteText: String? = null,
    @SerializedName("userId") val userId: Int? = null,
    @SerializedName("note") val note: String? = null,
    @SerializedName("assignedToEmpId") val assignedToEmpId: Int? = null
)

data class EscalationSettingsResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("daysToSupervisor") val daysToSupervisor: Int = 2,
    @SerializedName("daysToManager") val daysToManager: Int = 3,
    @SerializedName("supervisorPosition") val supervisorPosition: String? = null,
    @SerializedName("managerPosition") val managerPosition: String? = null
)

data class SetEscalationOverrideRequest(
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("daysToSupervisor") val daysToSupervisor: Int? = null,
    @SerializedName("daysToManager") val daysToManager: Int? = null,
    @SerializedName("reason") val reason: String,
    @SerializedName("changedByUserId") val changedByUserId: Int? = null,
    @SerializedName("clear") val clear: Boolean = false
)

data class SetEscalationOverrideResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null
)

data class CallLookupItem(
    @SerializedName("id") val id: Int = 0,
    @SerializedName("name") val name: String = "",
    @SerializedName("deptId") val deptId: Int? = null,
    @SerializedName("branchId") val branchId: Int? = null,
    @SerializedName("comId") val comId: Int? = null
)

data class CallLookupResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("items") val items: List<CallLookupItem> = emptyList()
)

data class EmployeeOrgResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("empId") val empId: Int = 0,
    @SerializedName("employeeName") val employeeName: String? = null,
    @SerializedName("deptId") val deptId: Int? = null,
    @SerializedName("branchId") val branchId: Int? = null,
    @SerializedName("comId") val comId: Int? = null
)

data class DispatchSetResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("set") val set: com.example.yakultscanner.data.model.DispatchSet?
)

data class CallDashboardResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("generatedUtc") val generatedUtc: String? = null,
    @SerializedName("metrics") val metrics: CallDashboardMetricsDto? = null,
    @SerializedName("volume") val volume: List<CallVolumePointDto> = emptyList(),
    @SerializedName("issueTypes") val issueTypes: List<CallIssueTypeDto> = emptyList()
)

data class CallDashboardMetricsDto(
    @SerializedName("openTickets") val openTickets: Int = 0,
    @SerializedName("criticalTickets") val criticalTickets: Int = 0,
    @SerializedName("todaysVolume") val todaysVolume: Int = 0,
    @SerializedName("avgResolutionMinutes") val avgResolutionMinutes: Double? = null
)

data class CallVolumePointDto(
    @SerializedName("day") val day: String? = null,
    @SerializedName("ticketCount") val ticketCount: Int = 0
)

data class CallIssueTypeDto(
    @SerializedName("issueType") val issueType: String? = null,
    @SerializedName("ticketCount") val ticketCount: Int = 0
)

// ── Call Field Work (one visit per ticket, Scheduled → Completed/Cancelled, Cancelled → Scheduled reschedule) ──
data class CallFieldVisitDto(
    @SerializedName("fieldVisitId") val fieldVisitId: Int = 0,
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("status") val status: String? = null,
    @SerializedName("scheduledAt") val scheduledAt: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("notes") val notes: String? = null,
    @SerializedName("technicianEmpId") val technicianEmpId: Int? = null,
    @SerializedName("technicianName") val technicianName: String? = null,
    @SerializedName("hasSignature") val hasSignature: Boolean = false,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null
)

data class CallFieldVisitAttachmentDto(
    @SerializedName("attachmentId") val attachmentId: Int = 0,
    @SerializedName("fieldVisitId") val fieldVisitId: Int = 0,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("fileSizeBytes") val fileSizeBytes: Int? = null,
    @SerializedName("uploadedAt") val uploadedAt: String? = null,
    @SerializedName("uploadedByName") val uploadedByName: String? = null
)

data class CallFieldVisitsResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("ticketId") val ticketId: Int? = null,
    @SerializedName("visits") val visits: List<CallFieldVisitDto> = emptyList(),
    @SerializedName("attachments") val attachments: List<CallFieldVisitAttachmentDto> = emptyList(),
    @SerializedName("totalCount") val totalCount: Int = 0,
    @SerializedName("page") val page: Int = 1,
    @SerializedName("pageSize") val pageSize: Int = 25,
    @SerializedName("message") val message: String? = null
)

data class ScheduleFieldVisitRequest(
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("technicianEmpId") val technicianEmpId: Int? = null,
    @SerializedName("scheduledAt") val scheduledAt: String? = null,
    @SerializedName("notes") val notes: String? = null
)

data class FieldVisitActionRequest(
    @SerializedName("fieldVisitId") val fieldVisitId: Int,
    @SerializedName("action") val action: String,
    @SerializedName("notes") val notes: String? = null,
    @SerializedName("customerSignatureBase64") val customerSignatureBase64: String? = null,
    @SerializedName("technicianEmpId") val technicianEmpId: Int? = null,
    @SerializedName("scheduledAt") val scheduledAt: String? = null
)

data class FieldVisitPhotoUploadRequest(
    @SerializedName("fieldVisitId") val fieldVisitId: Int,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("fileBase64") val fileBase64: String
)

data class CallFieldVisitPhotoResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("attachmentId") val attachmentId: Int = 0,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("base64") val base64: String? = null,
    @SerializedName("size") val size: Int = 0,
    @SerializedName("message") val message: String? = null,
    @SerializedName("error") val error: String? = null
)

data class CallFieldVisitSignatureResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("fieldVisitId") val fieldVisitId: Int = 0,
    @SerializedName("base64") val base64: String? = null,
    @SerializedName("size") val size: Int = 0,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("message") val message: String? = null,
    @SerializedName("error") val error: String? = null
)


