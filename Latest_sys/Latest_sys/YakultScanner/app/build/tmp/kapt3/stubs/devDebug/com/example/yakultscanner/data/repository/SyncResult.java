package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.SetUpdatesRequest;
import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.db.SyncStatus;
import kotlinx.coroutines.flow.Flow;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;
import javax.inject.Singleton;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0005\u0004\u0005\u0006\u0007\bB\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0005\t\n\u000b\f\r\u00a8\u0006\u000e"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult;", "", "<init>", "()V", "Success", "PartialSuccess", "Error", "NoPendingUpdates", "NetworkUnavailable", "Lcom/example/yakultscanner/data/repository/SyncResult$Error;", "Lcom/example/yakultscanner/data/repository/SyncResult$NetworkUnavailable;", "Lcom/example/yakultscanner/data/repository/SyncResult$NoPendingUpdates;", "Lcom/example/yakultscanner/data/repository/SyncResult$PartialSuccess;", "Lcom/example/yakultscanner/data/repository/SyncResult$Success;", "app_devDebug"})
public abstract class SyncResult {
    
    private SyncResult() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult$Error;", "Lcom/example/yakultscanner/data/repository/SyncResult;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.data.repository.SyncResult {
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String message = null;
        
        public Error(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getMessage() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.repository.SyncResult.Error copy(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
            return null;
        }
        
        @java.lang.Override()
        public boolean equals(@org.jetbrains.annotations.Nullable()
        java.lang.Object other) {
            return false;
        }
        
        @java.lang.Override()
        public int hashCode() {
            return 0;
        }
        
        @java.lang.Override()
        @org.jetbrains.annotations.NotNull()
        public java.lang.String toString() {
            return null;
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult$NetworkUnavailable;", "Lcom/example/yakultscanner/data/repository/SyncResult;", "<init>", "()V", "app_devDebug"})
    public static final class NetworkUnavailable extends com.example.yakultscanner.data.repository.SyncResult {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.data.repository.SyncResult.NetworkUnavailable INSTANCE = null;
        
        private NetworkUnavailable() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult$NoPendingUpdates;", "Lcom/example/yakultscanner/data/repository/SyncResult;", "<init>", "()V", "app_devDebug"})
    public static final class NoPendingUpdates extends com.example.yakultscanner.data.repository.SyncResult {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.data.repository.SyncResult.NoPendingUpdates INSTANCE = null;
        
        private NoPendingUpdates() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0010\u000e\n\u0002\b\f\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0002\b\u0003\b\u0086\b\u0018\u00002\u00020\u0001B%\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\f\u0010\u0005\u001a\b\u0012\u0004\u0012\u00020\u00070\u0006\u00a2\u0006\u0004\b\b\u0010\tJ\t\u0010\u000f\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u0010\u001a\u00020\u0003H\u00c6\u0003J\u000f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00070\u0006H\u00c6\u0003J-\u0010\u0012\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\u000e\b\u0002\u0010\u0005\u001a\b\u0012\u0004\u0012\u00020\u00070\u0006H\u00c6\u0001J\u0013\u0010\u0013\u001a\u00020\u00142\b\u0010\u0015\u001a\u0004\u0018\u00010\u0016H\u00d6\u0003J\t\u0010\u0017\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u0018\u001a\u00020\u0007H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\n\u0010\u000bR\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\f\u0010\u000bR\u0017\u0010\u0005\u001a\b\u0012\u0004\u0012\u00020\u00070\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\u000e\u00a8\u0006\u0019"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult$PartialSuccess;", "Lcom/example/yakultscanner/data/repository/SyncResult;", "uploadedCount", "", "failedCount", "errors", "", "", "<init>", "(IILjava/util/List;)V", "getUploadedCount", "()I", "getFailedCount", "getErrors", "()Ljava/util/List;", "component1", "component2", "component3", "copy", "equals", "", "other", "", "hashCode", "toString", "app_devDebug"})
    public static final class PartialSuccess extends com.example.yakultscanner.data.repository.SyncResult {
        private final int uploadedCount = 0;
        private final int failedCount = 0;
        @org.jetbrains.annotations.NotNull()
        private final java.util.List<java.lang.String> errors = null;
        
        public PartialSuccess(int uploadedCount, int failedCount, @org.jetbrains.annotations.NotNull()
        java.util.List<java.lang.String> errors) {
        }
        
        public final int getUploadedCount() {
            return 0;
        }
        
        public final int getFailedCount() {
            return 0;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.util.List<java.lang.String> getErrors() {
            return null;
        }
        
        public final int component1() {
            return 0;
        }
        
        public final int component2() {
            return 0;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.util.List<java.lang.String> component3() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.repository.SyncResult.PartialSuccess copy(int uploadedCount, int failedCount, @org.jetbrains.annotations.NotNull()
        java.util.List<java.lang.String> errors) {
            return null;
        }
        
        @java.lang.Override()
        public boolean equals(@org.jetbrains.annotations.Nullable()
        java.lang.Object other) {
            return false;
        }
        
        @java.lang.Override()
        public int hashCode() {
            return 0;
        }
        
        @java.lang.Override()
        @org.jetbrains.annotations.NotNull()
        public java.lang.String toString() {
            return null;
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\n\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u0017\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0005\u0010\u0006J\t\u0010\n\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u000b\u001a\u00020\u0003H\u00c6\u0003J\u001d\u0010\f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\r\u001a\u00020\u000e2\b\u0010\u000f\u001a\u0004\u0018\u00010\u0010H\u00d6\u0003J\t\u0010\u0011\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u0012\u001a\u00020\u0013H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0007\u0010\bR\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\b\u00a8\u0006\u0014"}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncResult$Success;", "Lcom/example/yakultscanner/data/repository/SyncResult;", "uploadedCount", "", "failedCount", "<init>", "(II)V", "getUploadedCount", "()I", "getFailedCount", "component1", "component2", "copy", "equals", "", "other", "", "hashCode", "toString", "", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.data.repository.SyncResult {
        private final int uploadedCount = 0;
        private final int failedCount = 0;
        
        public Success(int uploadedCount, int failedCount) {
        }
        
        public final int getUploadedCount() {
            return 0;
        }
        
        public final int getFailedCount() {
            return 0;
        }
        
        public final int component1() {
            return 0;
        }
        
        public final int component2() {
            return 0;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.repository.SyncResult.Success copy(int uploadedCount, int failedCount) {
            return null;
        }
        
        @java.lang.Override()
        public boolean equals(@org.jetbrains.annotations.Nullable()
        java.lang.Object other) {
            return false;
        }
        
        @java.lang.Override()
        public int hashCode() {
            return 0;
        }
        
        @java.lang.Override()
        @org.jetbrains.annotations.NotNull()
        public java.lang.String toString() {
            return null;
        }
    }
}