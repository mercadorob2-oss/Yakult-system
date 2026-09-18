package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.api.CallLookupItem;
import com.example.yakultscanner.api.CallTicketActionResponse;
import com.example.yakultscanner.api.CallTicketDetailResponse;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.api.CallTicketListResponse;
import com.example.yakultscanner.api.CreateTicketRequest;
import com.example.yakultscanner.api.EscalationSettingsResponse;
import com.example.yakultscanner.api.SetEscalationOverrideRequest;
import com.example.yakultscanner.api.CallItemLookupDto;
import com.example.yakultscanner.api.CallItemLookupResponse;
import com.example.yakultscanner.api.CallConditionDto;
import com.example.yakultscanner.api.CallConditionResponse;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.ResolutionRequest;
import com.example.yakultscanner.data.repository.CallMonitoringRepository;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import org.json.JSONObject;
import retrofit2.Response;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u00a8\u0001\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u000f\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0015\n\u0002\u0018\u0002\n\u0002\b\u000e\n\u0002\u0018\u0002\n\u0002\b\u000b\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005JV\u0010H\u001a\u00020I2\n\b\u0002\u0010J\u001a\u0004\u0018\u00010K2\n\b\u0002\u0010L\u001a\u0004\u0018\u00010K2\n\b\u0002\u0010M\u001a\u0004\u0018\u00010K2\n\b\u0002\u0010N\u001a\u0004\u0018\u00010K2\n\b\u0002\u0010O\u001a\u0004\u0018\u00010K2\b\b\u0002\u0010P\u001a\u00020\u001a2\b\b\u0002\u0010Q\u001a\u00020EJ\u000e\u0010R\u001a\u00020I2\u0006\u0010S\u001a\u00020\u001aJ1\u0010T\u001a\u00020I2\u0006\u0010S\u001a\u00020\u001a2\u0006\u0010U\u001a\u00020K2\b\b\u0002\u0010V\u001a\u00020K2\n\b\u0002\u0010W\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010XJ3\u0010Y\u001a\u00020I2\u0006\u0010S\u001a\u00020\u001a2\u0006\u0010Z\u001a\u00020K2\n\b\u0002\u0010[\u001a\u0004\u0018\u00010K2\n\b\u0002\u0010W\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010XJ\'\u0010\\\u001a\u00020I2\u0006\u0010S\u001a\u00020\u001a2\u0006\u0010]\u001a\u00020K2\n\b\u0002\u0010W\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010^J\u000e\u0010_\u001a\u00020I2\u0006\u0010`\u001a\u00020aJ\u0006\u0010b\u001a\u00020IJ\u0017\u0010c\u001a\u00020I2\n\b\u0002\u0010d\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010eJ#\u0010f\u001a\u00020I2\n\b\u0002\u0010d\u001a\u0004\u0018\u00010\u001a2\n\b\u0002\u0010g\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010hJ+\u0010i\u001a\u00020I2\u0006\u0010g\u001a\u00020\u001a2\n\b\u0002\u0010d\u001a\u0004\u0018\u00010\u001a2\n\b\u0002\u0010j\u001a\u0004\u0018\u00010\u001a\u00a2\u0006\u0002\u0010kJ\u0006\u0010l\u001a\u00020IJ\u0006\u0010m\u001a\u00020IJ\u0006\u0010n\u001a\u00020IJ\u000e\u0010o\u001a\u00020I2\u0006\u0010`\u001a\u00020pJ\u0006\u0010q\u001a\u00020IJ7\u0010r\u001a\u00020I2\u0006\u0010`\u001a\u00020a2\n\b\u0002\u0010s\u001a\u0004\u0018\u00010\u001a2\n\b\u0002\u0010t\u001a\u0004\u0018\u00010\u001a2\n\b\u0002\u0010u\u001a\u0004\u0018\u00010K\u00a2\u0006\u0002\u0010vJ\u0006\u0010w\u001a\u00020IJ\u0006\u0010x\u001a\u00020IJ\u0006\u0010y\u001a\u00020IJ\u0016\u0010z\u001a\u00020I2\f\u0010{\u001a\b\u0012\u0004\u0012\u00020}0|H\u0002R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0014\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\t\u001a\b\u0012\u0004\u0012\u00020\b0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\fR\u0014\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u000e0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\fR\u0014\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00120\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u00120\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\fR\u0014\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00160\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00160\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\fR\u000e\u0010\u0019\u001a\u00020\u001aX\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u001b\u001a\u00020\u001aX\u0082D\u00a2\u0006\u0002\n\u0000R\u001a\u0010\u001c\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010\u001f\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010\fR\u001a\u0010!\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010\"\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010\fR\u001a\u0010$\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010%\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010\fR\u001a\u0010\'\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010(\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b)\u0010\fR\u001a\u0010*\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010+\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u001e0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b,\u0010\fR\u0016\u0010-\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010.0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0019\u0010/\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010.0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b0\u0010\fR\u0014\u00101\u001a\b\u0012\u0004\u0012\u0002020\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u00103\u001a\b\u0012\u0004\u0012\u0002020\n\u00a2\u0006\b\n\u0000\u001a\u0004\b4\u0010\fR\u001a\u00105\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u0002060\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u00107\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u0002060\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b8\u0010\fR\u001a\u00109\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u0002060\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010:\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u0002060\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b;\u0010\fR\u001a\u0010<\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020=0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010>\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020=0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b?\u0010\fR\u001a\u0010@\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020A0\u001d0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010B\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020A0\u001d0\n\u00a2\u0006\b\n\u0000\u001a\u0004\bC\u0010\fR\u0014\u0010D\u001a\b\u0012\u0004\u0012\u00020E0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010F\u001a\b\u0012\u0004\u0012\u00020E0\n\u00a2\u0006\b\n\u0000\u001a\u0004\bG\u0010\f\u00a8\u0006~"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "Landroidx/lifecycle/ViewModel;", "repository", "Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;", "<init>", "(Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;)V", "_ticketsState", "Lkotlinx/coroutines/flow/MutableStateFlow;", "Lcom/example/yakultscanner/viewmodels/CallTicketsUiState;", "ticketsState", "Lkotlinx/coroutines/flow/StateFlow;", "getTicketsState", "()Lkotlinx/coroutines/flow/StateFlow;", "_detailState", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState;", "detailState", "getDetailState", "_actionState", "Lcom/example/yakultscanner/viewmodels/ActionUiState;", "actionState", "getActionState", "_createState", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "createState", "getCreateState", "currentPage", "", "pageSize", "_companies", "", "Lcom/example/yakultscanner/api/CallLookupItem;", "companies", "getCompanies", "_departments", "departments", "getDepartments", "_branches", "branches", "getBranches", "_callerEmployees", "callerEmployees", "getCallerEmployees", "_itEmployees", "itEmployees", "getItEmployees", "_escalationSettings", "Lcom/example/yakultscanner/api/EscalationSettingsResponse;", "escalationSettings", "getEscalationSettings", "_resolutionState", "Lcom/example/yakultscanner/viewmodels/ResolutionUiState;", "resolutionState", "getResolutionState", "_resolutionOutItems", "Lcom/example/yakultscanner/api/CallItemLookupDto;", "resolutionOutItems", "getResolutionOutItems", "_resolutionStockItems", "resolutionStockItems", "getResolutionStockItems", "_resolutionConditions", "Lcom/example/yakultscanner/api/CallConditionDto;", "resolutionConditions", "getResolutionConditions", "_resolutionCategories", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "resolutionCategories", "getResolutionCategories", "_resolutionLoadingLookups", "", "resolutionLoadingLookups", "getResolutionLoadingLookups", "loadTickets", "", "status", "", "scope", "search", "priority", "issueType", "page", "refresh", "loadTicketDetail", "ticketId", "addNote", "noteText", "noteType", "userId", "(ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;)V", "updateStatus", "newStatus", "note", "updatePriority", "newPriority", "(ILjava/lang/String;Ljava/lang/Integer;)V", "createTicket", "request", "Lcom/example/yakultscanner/api/CreateTicketRequest;", "loadCompanies", "loadDepartments", "comId", "(Ljava/lang/Integer;)V", "loadBranches", "deptId", "(Ljava/lang/Integer;Ljava/lang/Integer;)V", "loadCallerEmployees", "branchId", "(ILjava/lang/Integer;Ljava/lang/Integer;)V", "loadItEmployees", "loadEscalationSettings", "loadResolutionLookups", "applyResolution", "Lcom/example/yakultscanner/api/ResolutionRequest;", "resetResolutionState", "createTicketWithEscalation", "daysToSupervisor", "daysToManager", "reason", "(Lcom/example/yakultscanner/api/CreateTicketRequest;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;)V", "clearCallerEmployees", "resetActionState", "resetCreateState", "handleActionResponse", "response", "Lretrofit2/Response;", "Lcom/example/yakultscanner/api/CallTicketActionResponse;", "app_devDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class CallMonitoringViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.CallMonitoringRepository repository = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.CallTicketsUiState> _ticketsState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CallTicketsUiState> ticketsState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.TicketDetailUiState> _detailState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.TicketDetailUiState> detailState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.ActionUiState> _actionState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.ActionUiState> actionState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.CreateTicketUiState> _createState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CreateTicketUiState> createState = null;
    private int currentPage = 1;
    private final int pageSize = 25;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> _companies = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> companies = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> _departments = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> departments = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> _branches = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> branches = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> _callerEmployees = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> callerEmployees = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> _itEmployees = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> itEmployees = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.api.EscalationSettingsResponse> _escalationSettings = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.api.EscalationSettingsResponse> escalationSettings = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.ResolutionUiState> _resolutionState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.ResolutionUiState> resolutionState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> _resolutionOutItems = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> resolutionOutItems = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> _resolutionStockItems = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> resolutionStockItems = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.CallConditionDto>> _resolutionConditions = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallConditionDto>> resolutionConditions = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>> _resolutionCategories = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>> resolutionCategories = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _resolutionLoadingLookups = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> resolutionLoadingLookups = null;
    
    @javax.inject.Inject()
    public CallMonitoringViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.CallMonitoringRepository repository) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CallTicketsUiState> getTicketsState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.TicketDetailUiState> getDetailState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.ActionUiState> getActionState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CreateTicketUiState> getCreateState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> getCompanies() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> getDepartments() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> getBranches() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> getCallerEmployees() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallLookupItem>> getItEmployees() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.api.EscalationSettingsResponse> getEscalationSettings() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.ResolutionUiState> getResolutionState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> getResolutionOutItems() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallItemLookupDto>> getResolutionStockItems() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.CallConditionDto>> getResolutionConditions() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>> getResolutionCategories() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> getResolutionLoadingLookups() {
        return null;
    }
    
    public final void loadTickets(@org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String scope, @org.jetbrains.annotations.Nullable()
    java.lang.String search, @org.jetbrains.annotations.Nullable()
    java.lang.String priority, @org.jetbrains.annotations.Nullable()
    java.lang.String issueType, int page, boolean refresh) {
    }
    
    public final void loadTicketDetail(int ticketId) {
    }
    
    public final void addNote(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String noteText, @org.jetbrains.annotations.NotNull()
    java.lang.String noteType, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId) {
    }
    
    public final void updateStatus(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String note, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId) {
    }
    
    public final void updatePriority(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String newPriority, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId) {
    }
    
    public final void createTicket(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CreateTicketRequest request) {
    }
    
    public final void loadCompanies() {
    }
    
    public final void loadDepartments(@org.jetbrains.annotations.Nullable()
    java.lang.Integer comId) {
    }
    
    public final void loadBranches(@org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer deptId) {
    }
    
    public final void loadCallerEmployees(int deptId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer branchId) {
    }
    
    public final void loadItEmployees() {
    }
    
    public final void loadEscalationSettings() {
    }
    
    public final void loadResolutionLookups() {
    }
    
    public final void applyResolution(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ResolutionRequest request) {
    }
    
    public final void resetResolutionState() {
    }
    
    public final void createTicketWithEscalation(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CreateTicketRequest request, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToSupervisor, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToManager, @org.jetbrains.annotations.Nullable()
    java.lang.String reason) {
    }
    
    public final void clearCallerEmployees() {
    }
    
    public final void resetActionState() {
    }
    
    public final void resetCreateState() {
    }
    
    private final void handleActionResponse(retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse> response) {
    }
}