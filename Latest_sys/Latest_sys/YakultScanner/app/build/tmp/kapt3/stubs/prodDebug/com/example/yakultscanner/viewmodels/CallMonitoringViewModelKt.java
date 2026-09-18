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

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u0016\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\u001a\u0016\u0010\u0000\u001a\u0004\u0018\u00010\u00012\n\u0010\u0002\u001a\u0006\u0012\u0002\b\u00030\u0003H\u0002\u001a\u0010\u0010\u0004\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u0006H\u0002\u00a8\u0006\u0007"}, d2 = {"serverErrorMessage", "", "response", "Lretrofit2/Response;", "friendlyHttpMessage", "code", "", "app_prodDebug"})
public final class CallMonitoringViewModelKt {
    
    private static final java.lang.String serverErrorMessage(retrofit2.Response<?> response) {
        return null;
    }
    
    private static final java.lang.String friendlyHttpMessage(int code) {
        return null;
    }
}