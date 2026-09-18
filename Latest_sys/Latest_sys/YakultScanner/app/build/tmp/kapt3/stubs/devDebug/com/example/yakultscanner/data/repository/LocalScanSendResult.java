package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.SerialFromMobileRequest;
import com.example.yakultscanner.data.db.LocalScanDao;
import com.example.yakultscanner.data.db.LocalScanItemEntity;
import com.example.yakultscanner.data.db.LocalScanSessionEntity;
import com.example.yakultscanner.data.db.LocalScanSessionStatus;
import com.example.yakultscanner.data.db.LocalScanSessionWithCount;
import kotlinx.coroutines.flow.Flow;
import javax.inject.Inject;
import javax.inject.Singleton;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\n\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u0017\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0005\u0010\u0006J\t\u0010\n\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u000b\u001a\u00020\u0003H\u00c6\u0003J\u001d\u0010\f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\r\u001a\u00020\u000e2\b\u0010\u000f\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u0011\u001a\u00020\u0012H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0007\u0010\bR\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\b\u00a8\u0006\u0013"}, d2 = {"Lcom/example/yakultscanner/data/repository/LocalScanSendResult;", "", "successCount", "", "failedCount", "<init>", "(II)V", "getSuccessCount", "()I", "getFailedCount", "component1", "component2", "copy", "equals", "", "other", "hashCode", "toString", "", "app_devDebug"})
public final class LocalScanSendResult {
    private final int successCount = 0;
    private final int failedCount = 0;
    
    public LocalScanSendResult(int successCount, int failedCount) {
        super();
    }
    
    public final int getSuccessCount() {
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
    public final com.example.yakultscanner.data.repository.LocalScanSendResult copy(int successCount, int failedCount) {
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