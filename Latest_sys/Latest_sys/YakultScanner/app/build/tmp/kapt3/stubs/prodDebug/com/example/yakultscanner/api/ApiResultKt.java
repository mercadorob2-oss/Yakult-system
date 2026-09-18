package com.example.yakultscanner.api;

import com.example.yakultscanner.ConnectionHealthStore;
import org.json.JSONObject;
import retrofit2.Response;
import java.io.IOException;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000L\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\u001a\u0012\u0010\u0006\u001a\u00020\u0001*\u00020\u00072\u0006\u0010\b\u001a\u00020\u0001\u001a\u0014\u0010\u0006\u001a\u00020\u0001*\u00020\t2\b\b\u0002\u0010\b\u001a\u00020\u0001\u001a\u0014\u0010\u0006\u001a\u00020\u0001*\u00020\n2\b\b\u0002\u0010\b\u001a\u00020\u0001\u001a>\u0010\u000b\u001a\b\u0012\u0004\u0012\u0002H\r0\f\"\u0004\b\u0000\u0010\r2\"\u0010\u000e\u001a\u001e\b\u0001\u0012\u0010\u0012\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u0002H\r0\u00110\u0010\u0012\u0006\u0012\u0004\u0018\u00010\u00120\u000fH\u0086@\u00a2\u0006\u0002\u0010\u0013\u001a\u001a\u0010\u0014\u001a\u00020\u00152\u0006\u0010\u0016\u001a\u00020\u00172\b\u0010\u0018\u001a\u0004\u0018\u00010\u0001H\u0002\u001a\u0012\u0010\u0019\u001a\u0004\u0018\u00010\u001a2\u0006\u0010\u001b\u001a\u00020\u0001H\u0002\u001a\u001c\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u0016\u001a\u00020\u00172\n\b\u0002\u0010\u001d\u001a\u0004\u0018\u00010\u0001H\u0002\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0002\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0003\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0004\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0005\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u001e"}, d2 = {"SCANNER_INVALID_QR_MESSAGE", "", "SCANNER_UNSUPPORTED_QR_MESSAGE", "SCANNER_NETWORK_MESSAGE", "SCANNER_SERVICE_UNAVAILABLE_MESSAGE", "SCANNER_GENERIC_MESSAGE", "userMessageOr", "Lcom/example/yakultscanner/api/ApiResult$HttpError;", "defaultMessage", "Lcom/example/yakultscanner/api/ApiResult$NetworkError;", "Lcom/example/yakultscanner/api/ApiResult$UnknownError;", "safeApiCall", "Lcom/example/yakultscanner/api/ApiResult;", "T", "call", "Lkotlin/Function1;", "Lkotlin/coroutines/Continuation;", "Lretrofit2/Response;", "", "(Lkotlin/jvm/functions/Function1;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "parseFriendlyHttpError", "Lcom/example/yakultscanner/api/ParsedApiError;", "httpCode", "", "rawError", "parseJsonErrorPayload", "Lcom/example/yakultscanner/api/JsonErrorPayload;", "raw", "defaultHttpMessage", "errorCode", "app_prodDebug"})
public final class ApiResultKt {
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String SCANNER_INVALID_QR_MESSAGE = "This QR code is not a valid Yakult dispatch QR code. Please scan a valid Yakult QR code.";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String SCANNER_UNSUPPORTED_QR_MESSAGE = "This QR code is not supported by Yakult Scanner. Please scan a valid Yakult QR code.";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String SCANNER_NETWORK_MESSAGE = "We couldn\'t connect to the server. Please check your connection and try again.";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String SCANNER_SERVICE_UNAVAILABLE_MESSAGE = "We couldn\'t reach the scanner service right now. Please try again in a moment.";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String SCANNER_GENERIC_MESSAGE = "Something went wrong while processing your request. Please try again.";
    
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String userMessageOr(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ApiResult.HttpError $this$userMessageOr, @org.jetbrains.annotations.NotNull()
    java.lang.String defaultMessage) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String userMessageOr(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ApiResult.NetworkError $this$userMessageOr, @org.jetbrains.annotations.NotNull()
    java.lang.String defaultMessage) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String userMessageOr(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ApiResult.UnknownError $this$userMessageOr, @org.jetbrains.annotations.NotNull()
    java.lang.String defaultMessage) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public static final <T extends java.lang.Object>java.lang.Object safeApiCall(@org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super kotlin.coroutines.Continuation<? super retrofit2.Response<T>>, ? extends java.lang.Object> call, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.api.ApiResult<? extends T>> $completion) {
        return null;
    }
    
    private static final com.example.yakultscanner.api.ParsedApiError parseFriendlyHttpError(int httpCode, java.lang.String rawError) {
        return null;
    }
    
    private static final com.example.yakultscanner.api.JsonErrorPayload parseJsonErrorPayload(java.lang.String raw) {
        return null;
    }
    
    private static final java.lang.String defaultHttpMessage(int httpCode, java.lang.String errorCode) {
        return null;
    }
}