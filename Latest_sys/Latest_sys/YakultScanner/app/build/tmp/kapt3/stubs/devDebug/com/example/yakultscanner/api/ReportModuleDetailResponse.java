package com.example.yakultscanner.api;

import com.google.gson.annotations.SerializedName;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000.\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0006\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u001a\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u007f\u0012\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0003\u0012\u000e\b\u0002\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u0012\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u0012\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u00a2\u0006\u0004\b\u000e\u0010\u000fJ\u000b\u0010\u001b\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001c\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001d\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001e\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001f\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010 \u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003J\u000f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003J\u000f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003J\u0081\u0001\u0010$\u001a\u00020\u00002\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00032\u000e\b\u0002\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n2\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n2\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0001J\u0013\u0010%\u001a\u00020&2\b\u0010\'\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010(\u001a\u00020)H\u00d6\u0001J\t\u0010*\u001a\u00020\u0003H\u00d6\u0001R\u0018\u0010\u0002\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u0011R\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0011R\u0018\u0010\u0005\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0011R\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0011R\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0011R\u0018\u0010\b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0011R\u001c\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0018R\u001c\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0018R\u001c\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\n8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0018\u00a8\u0006+"}, d2 = {"Lcom/example/yakultscanner/api/ReportModuleDetailResponse;", "", "moduleId", "", "title", "summary", "heroLabel", "heroValue", "heroTone", "keyRows", "", "Lcom/example/yakultscanner/api/ReportListRowDto;", "leaderboardRows", "timelineRows", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/util/List;Ljava/util/List;Ljava/util/List;)V", "getModuleId", "()Ljava/lang/String;", "getTitle", "getSummary", "getHeroLabel", "getHeroValue", "getHeroTone", "getKeyRows", "()Ljava/util/List;", "getLeaderboardRows", "getTimelineRows", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "copy", "equals", "", "other", "hashCode", "", "toString", "app_devDebug"})
public final class ReportModuleDetailResponse {
    @com.google.gson.annotations.SerializedName(value = "moduleId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String moduleId = null;
    @com.google.gson.annotations.SerializedName(value = "title")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String title = null;
    @com.google.gson.annotations.SerializedName(value = "summary")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String summary = null;
    @com.google.gson.annotations.SerializedName(value = "heroLabel")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String heroLabel = null;
    @com.google.gson.annotations.SerializedName(value = "heroValue")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String heroValue = null;
    @com.google.gson.annotations.SerializedName(value = "heroTone")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String heroTone = null;
    @com.google.gson.annotations.SerializedName(value = "keyRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> keyRows = null;
    @com.google.gson.annotations.SerializedName(value = "leaderboardRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> leaderboardRows = null;
    @com.google.gson.annotations.SerializedName(value = "timelineRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows = null;
    
    public ReportModuleDetailResponse(@org.jetbrains.annotations.Nullable()
    java.lang.String moduleId, @org.jetbrains.annotations.Nullable()
    java.lang.String title, @org.jetbrains.annotations.Nullable()
    java.lang.String summary, @org.jetbrains.annotations.Nullable()
    java.lang.String heroLabel, @org.jetbrains.annotations.Nullable()
    java.lang.String heroValue, @org.jetbrains.annotations.Nullable()
    java.lang.String heroTone, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> keyRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> leaderboardRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getModuleId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getTitle() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSummary() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getHeroLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getHeroValue() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getHeroTone() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getKeyRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getLeaderboardRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getTimelineRows() {
        return null;
    }
    
    public ReportModuleDetailResponse() {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.ReportModuleDetailResponse copy(@org.jetbrains.annotations.Nullable()
    java.lang.String moduleId, @org.jetbrains.annotations.Nullable()
    java.lang.String title, @org.jetbrains.annotations.Nullable()
    java.lang.String summary, @org.jetbrains.annotations.Nullable()
    java.lang.String heroLabel, @org.jetbrains.annotations.Nullable()
    java.lang.String heroValue, @org.jetbrains.annotations.Nullable()
    java.lang.String heroTone, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> keyRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> leaderboardRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows) {
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