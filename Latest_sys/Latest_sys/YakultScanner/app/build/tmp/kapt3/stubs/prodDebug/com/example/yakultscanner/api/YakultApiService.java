package com.example.yakultscanner.api;

import android.os.Handler;
import android.os.Looper;
import com.example.yakultscanner.BuildConfig;
import com.example.yakultscanner.GlobalNav;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.data.model.DispatchSet;
import com.example.yakultscanner.settings.ApiSettings;
import com.google.gson.annotations.SerializedName;
import okhttp3.OkHttpClient;
import okhttp3.logging.HttpLoggingInterceptor;
import retrofit2.Retrofit;
import retrofit2.converter.gson.GsonConverterFactory;
import retrofit2.http.Body;
import retrofit2.http.GET;
import retrofit2.http.POST;
import retrofit2.http.PUT;
import retrofit2.http.Path;
import retrofit2.http.Query;
import java.util.concurrent.TimeUnit;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u00b0\u0003\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u000b\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\bf\u0018\u00002\u00020\u0001J\u001e\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\b\b\u0001\u0010\u0005\u001a\u00020\u0006H\u00a7@\u00a2\u0006\u0002\u0010\u0007J\u001e\u0010\b\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\b\b\u0001\u0010\u0005\u001a\u00020\nH\u00a7@\u00a2\u0006\u0002\u0010\u000bJ\u001e\u0010\f\u001a\b\u0012\u0004\u0012\u00020\r0\u00032\b\b\u0001\u0010\u0005\u001a\u00020\u000eH\u00a7@\u00a2\u0006\u0002\u0010\u000fJ$\u0010\u0010\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00120\u00110\u00032\b\b\u0001\u0010\u0013\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u0014\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\u00170\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001a\u0010\u0019\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00120\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001a\u0010\u001a\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00120\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001e\u0010\u001b\u001a\b\u0012\u0004\u0012\u00020\u001c0\u00032\b\b\u0001\u0010\u0005\u001a\u00020\u001dH\u00a7@\u00a2\u0006\u0002\u0010\u001eJ(\u0010\u001f\u001a\b\u0012\u0004\u0012\u00020 0\u00032\b\b\u0001\u0010\u0005\u001a\u00020!2\b\b\u0003\u0010\"\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010#J\u001a\u0010$\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020%0\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001a\u0010&\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\'0\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001a\u0010(\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020)0\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001e\u0010*\u001a\b\u0012\u0004\u0012\u00020+0\u00032\b\b\u0001\u0010,\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u001e\u0010-\u001a\b\u0012\u0004\u0012\u00020.0\u00032\b\b\u0001\u0010,\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u001e\u0010/\u001a\b\u0012\u0004\u0012\u0002000\u00032\b\b\u0001\u0010,\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J(\u00101\u001a\b\u0012\u0004\u0012\u0002020\u00032\b\b\u0001\u0010,\u001a\u00020\u00142\b\b\u0001\u0010\u0005\u001a\u000203H\u00a7@\u00a2\u0006\u0002\u00104JB\u00105\u001a\b\u0012\u0004\u0012\u0002060\u00032\n\b\u0003\u0010,\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010\u0013\u001a\u0004\u0018\u00010\u00142\b\b\u0001\u0010\u0005\u001a\u0002072\n\b\u0003\u00108\u001a\u0004\u0018\u00010\u0014H\u00a7@\u00a2\u0006\u0002\u00109J,\u0010:\u001a\b\u0012\u0004\u0012\u00020;0\u00032\n\b\u0003\u0010,\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010\u0013\u001a\u0004\u0018\u00010\u0014H\u00a7@\u00a2\u0006\u0002\u0010<JB\u0010=\u001a\b\u0012\u0004\u0012\u00020>0\u00032\n\b\u0003\u0010,\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010\u0013\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u00108\u001a\u0004\u0018\u00010\u00142\b\b\u0001\u0010\u0005\u001a\u00020?H\u00a7@\u00a2\u0006\u0002\u0010@J\u001e\u0010A\u001a\b\u0012\u0004\u0012\u00020B0\u00032\b\b\u0001\u0010C\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJ\u001e\u0010F\u001a\b\u0012\u0004\u0012\u00020G0\u00032\b\b\u0001\u0010,\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J*\u0010H\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020I0\u00110\u00032\u000e\b\u0001\u0010J\u001a\b\u0012\u0004\u0012\u00020\u00140\u0011H\u00a7@\u00a2\u0006\u0002\u0010KJ\u0014\u0010L\u001a\b\u0012\u0004\u0012\u00020M0\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u001e\u0010N\u001a\b\u0012\u0004\u0012\u00020O0\u00032\b\b\u0001\u0010P\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J>\u0010Q\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020R0\u00110\u00032\n\b\u0003\u0010S\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010T\u001a\u0004\u0018\u00010D2\n\b\u0003\u0010U\u001a\u0004\u0018\u00010DH\u00a7@\u00a2\u0006\u0002\u0010VJ\u001e\u0010W\u001a\b\u0012\u0004\u0012\u00020R0\u00032\b\b\u0001\u0010\u0005\u001a\u00020XH\u00a7@\u00a2\u0006\u0002\u0010YJ\u001a\u0010Z\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020[0\u00110\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J$\u0010\\\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020]0\u00110\u00032\b\b\u0001\u0010T\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJ$\u0010^\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020_0\u00110\u00032\b\b\u0001\u0010T\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJ\u001e\u0010`\u001a\b\u0012\u0004\u0012\u00020a0\u00032\b\b\u0001\u0010\u0005\u001a\u00020bH\u00a7@\u00a2\u0006\u0002\u0010cJ\u001e\u0010d\u001a\b\u0012\u0004\u0012\u00020a0\u00032\b\b\u0001\u0010\u0005\u001a\u00020eH\u00a7@\u00a2\u0006\u0002\u0010fJ\u001e\u0010g\u001a\b\u0012\u0004\u0012\u00020a0\u00032\b\b\u0001\u0010\u0005\u001a\u00020hH\u00a7@\u00a2\u0006\u0002\u0010iJ4\u0010j\u001a\b\u0012\u0004\u0012\u00020k0\u00032\n\b\u0003\u0010l\u001a\u0004\u0018\u00010\u00142\b\b\u0003\u0010m\u001a\u00020D2\b\b\u0003\u0010n\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010oJ\u001e\u0010p\u001a\b\u0012\u0004\u0012\u00020q0\u00032\b\b\u0003\u0010r\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJL\u0010s\u001a\b\u0012\u0004\u0012\u00020k0\u00032\n\b\u0003\u0010l\u001a\u0004\u0018\u00010\u00142\b\b\u0003\u0010m\u001a\u00020D2\b\b\u0003\u0010n\u001a\u00020D2\n\b\u0003\u0010t\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010u\u001a\u0004\u0018\u00010\u0014H\u00a7@\u00a2\u0006\u0002\u0010vJ(\u0010w\u001a\b\u0012\u0004\u0012\u00020x0\u00032\b\b\u0001\u0010y\u001a\u00020\u00142\b\b\u0003\u0010\"\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010<J(\u0010z\u001a\b\u0012\u0004\u0012\u00020{0\u00032\b\b\u0001\u0010y\u001a\u00020\u00142\b\b\u0001\u0010\"\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010<J\u001e\u0010|\u001a\b\u0012\u0004\u0012\u00020}0\u00032\b\b\u0001\u0010P\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u001f\u0010~\u001a\b\u0012\u0004\u0012\u00020\u007f0\u00032\t\b\u0001\u0010\u0080\u0001\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJ`\u0010\u0081\u0001\u001a\t\u0012\u0005\u0012\u00030\u0082\u00010\u00032\u000b\b\u0003\u0010\u0083\u0001\u001a\u0004\u0018\u00010\u00142\u000b\b\u0003\u0010\u0084\u0001\u001a\u0004\u0018\u00010\u00142\u000b\b\u0003\u0010\u0085\u0001\u001a\u0004\u0018\u00010\u00142\u000b\b\u0003\u0010\u0086\u0001\u001a\u0004\u0018\u00010\u00142\t\b\u0003\u0010\u0087\u0001\u001a\u00020D2\b\b\u0003\u0010n\u001a\u00020DH\u00a7@\u00a2\u0006\u0003\u0010\u0088\u0001J!\u0010\u0089\u0001\u001a\t\u0012\u0005\u0012\u00030\u008a\u00010\u00032\t\b\u0001\u0010\u008b\u0001\u001a\u00020DH\u00a7@\u00a2\u0006\u0002\u0010EJ\"\u0010\u008c\u0001\u001a\t\u0012\u0005\u0012\u00030\u008d\u00010\u00032\t\b\u0001\u0010\u0005\u001a\u00030\u008e\u0001H\u00a7@\u00a2\u0006\u0003\u0010\u008f\u0001J\"\u0010\u0090\u0001\u001a\t\u0012\u0005\u0012\u00030\u0091\u00010\u00032\t\b\u0001\u0010\u0005\u001a\u00030\u0092\u0001H\u00a7@\u00a2\u0006\u0003\u0010\u0093\u0001J\"\u0010\u0094\u0001\u001a\t\u0012\u0005\u0012\u00030\u0091\u00010\u00032\t\b\u0001\u0010\u0005\u001a\u00030\u0095\u0001H\u00a7@\u00a2\u0006\u0003\u0010\u0096\u0001J!\u0010\u0097\u0001\u001a\t\u0012\u0005\u0012\u00030\u0098\u00010\u00032\t\b\u0001\u0010\u0099\u0001\u001a\u00020\u0014H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u0016\u0010\u009a\u0001\u001a\t\u0012\u0005\u0012\u00030\u009b\u00010\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u0016\u0010\u009c\u0001\u001a\t\u0012\u0005\u0012\u00030\u009d\u00010\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J$\u0010\u009e\u0001\u001a\t\u0012\u0005\u0012\u00030\u009d\u00010\u00032\u000b\b\u0003\u0010\u009f\u0001\u001a\u0004\u0018\u00010DH\u00a7@\u00a2\u0006\u0003\u0010\u00a0\u0001J1\u0010\u00a1\u0001\u001a\t\u0012\u0005\u0012\u00030\u009d\u00010\u00032\u000b\b\u0003\u0010\u009f\u0001\u001a\u0004\u0018\u00010D2\u000b\b\u0003\u0010\u00a2\u0001\u001a\u0004\u0018\u00010DH\u00a7@\u00a2\u0006\u0003\u0010\u00a3\u0001J<\u0010\u00a4\u0001\u001a\t\u0012\u0005\u0012\u00030\u009d\u00010\u00032\t\b\u0001\u0010\u00a2\u0001\u001a\u00020D2\u000b\b\u0003\u0010\u009f\u0001\u001a\u0004\u0018\u00010D2\u000b\b\u0003\u0010\u00a5\u0001\u001a\u0004\u0018\u00010DH\u00a7@\u00a2\u0006\u0003\u0010\u00a6\u0001J\u0016\u0010\u00a7\u0001\u001a\t\u0012\u0005\u0012\u00030\u009d\u00010\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\u0016\u0010\u00a8\u0001\u001a\t\u0012\u0005\u0012\u00030\u00a9\u00010\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018J\"\u0010\u00aa\u0001\u001a\t\u0012\u0005\u0012\u00030\u00ab\u00010\u00032\t\b\u0001\u0010\u0005\u001a\u00030\u00ac\u0001H\u00a7@\u00a2\u0006\u0003\u0010\u00ad\u0001J.\u0010\u00ae\u0001\u001a\t\u0012\u0005\u0012\u00030\u00af\u00010\u00032\n\b\u0003\u0010\u0013\u001a\u0004\u0018\u00010\u00142\n\b\u0003\u0010,\u001a\u0004\u0018\u00010\u0014H\u00a7@\u00a2\u0006\u0002\u0010<J\u0016\u0010\u00b0\u0001\u001a\t\u0012\u0005\u0012\u00030\u00b1\u00010\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0018\u00a8\u0006\u00b2\u0001\u00c0\u0006\u0003"}, d2 = {"Lcom/example/yakultscanner/api/YakultApiService;", "", "login", "Lretrofit2/Response;", "Lcom/example/yakultscanner/api/LoginResponse;", "request", "Lcom/example/yakultscanner/api/LoginRequest;", "(Lcom/example/yakultscanner/api/LoginRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "register", "Lcom/example/yakultscanner/api/RegisterResponse;", "Lcom/example/yakultscanner/api/RegisterRequest;", "(Lcom/example/yakultscanner/api/RegisterRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "uploadSetUpdates", "Lcom/example/yakultscanner/api/SetUpdatesResponse;", "Lcom/example/yakultscanner/api/SetUpdatesRequest;", "(Lcom/example/yakultscanner/api/SetUpdatesRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getSetUpdates", "", "Lcom/example/yakultscanner/api/SetItemUpdateDto;", "setCode", "", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingIssues", "Lcom/example/yakultscanner/api/PendingIssuesResponse;", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getProcessedUpdates", "getPendingUpdates", "createBatchItems", "Lcom/example/yakultscanner/api/BatchItemsResponse;", "Lcom/example/yakultscanner/api/BatchItemsRequest;", "(Lcom/example/yakultscanner/api/BatchItemsRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "sendSerialToWindows", "Lcom/example/yakultscanner/api/SerialFromMobileResponse;", "Lcom/example/yakultscanner/api/SerialFromMobileRequest;", "route", "(Lcom/example/yakultscanner/api/SerialFromMobileRequest;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getItemCategories", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "getConditions", "Lcom/example/yakultscanner/api/ConditionDto;", "getVendors", "Lcom/example/yakultscanner/api/VendorDto;", "getSetByToken", "Lcom/example/yakultscanner/data/model/DispatchSet;", "token", "getSetDeploymentHistory", "Lcom/example/yakultscanner/api/SetDeploymentHistoryResponse;", "getSetItemsDetailed", "Lcom/example/yakultscanner/api/SetItemsDetailResponse;", "confirmSetDeployment", "Lcom/example/yakultscanner/api/SetConfirmationResponse;", "Lcom/example/yakultscanner/api/SetConfirmationRequest;", "(Ljava/lang/String;Lcom/example/yakultscanner/api/SetConfirmationRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "uploadSetImage", "Lcom/example/yakultscanner/api/SetImageUploadResponse;", "Lcom/example/yakultscanner/api/SetImageUploadRequest;", "documentNumber", "(Ljava/lang/String;Ljava/lang/String;Lcom/example/yakultscanner/api/SetImageUploadRequest;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getSetImages", "Lcom/example/yakultscanner/api/SetImagesResponse;", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "uploadReceiptImage", "Lcom/example/yakultscanner/api/ReceiptImageUploadResponse;", "Lcom/example/yakultscanner/api/ReceiptImageUploadRequest;", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Lcom/example/yakultscanner/api/ReceiptImageUploadRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "deploySet", "Lcom/example/yakultscanner/api/DeployResponse;", "setId", "", "(ILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "resolveToken", "Lcom/example/yakultscanner/api/ResolveTokenResponse;", "getSetStatuses", "Lcom/example/yakultscanner/api/SetStatusDto;", "setCodes", "(Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getDispatchCount", "Lcom/example/yakultscanner/api/DispatchCountResponse;", "resolveBorrow", "Lcom/example/yakultscanner/api/BorrowResolveResponse;", "serial", "searchBorrowEmployees", "Lcom/example/yakultscanner/api/BorrowEmployeeDto;", "query", "companyId", "departmentId", "(Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "createBorrowEmployee", "Lcom/example/yakultscanner/api/BorrowEmployeeCreateRequest;", "(Lcom/example/yakultscanner/api/BorrowEmployeeCreateRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getBorrowCompanies", "Lcom/example/yakultscanner/api/BorrowCompanyDto;", "getBorrowBranches", "Lcom/example/yakultscanner/api/BorrowBranchDto;", "getBorrowDepartments", "Lcom/example/yakultscanner/api/BorrowDepartmentDto;", "borrowItem", "Lcom/example/yakultscanner/api/BorrowActionResponse;", "Lcom/example/yakultscanner/api/BorrowCreateRequest;", "(Lcom/example/yakultscanner/api/BorrowCreateRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "returnBorrow", "Lcom/example/yakultscanner/api/BorrowReturnRequest;", "(Lcom/example/yakultscanner/api/BorrowReturnRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "deleteBorrow", "Lcom/example/yakultscanner/api/BorrowDeleteRequest;", "(Lcom/example/yakultscanner/api/BorrowDeleteRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getOpenBorrows", "Lcom/example/yakultscanner/api/BorrowLogPageResponse;", "serialContains", "pageIndex", "pageSize", "(Ljava/lang/String;IILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getBorrowHomeSummary", "Lcom/example/yakultscanner/api/BorrowHomeSummaryResponse;", "recentCount", "getBorrowHistory", "fromUtc", "toUtc", "(Ljava/lang/String;IILjava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getReportsSummary", "Lcom/example/yakultscanner/api/ReportsSummaryResponse;", "range", "getReportModuleDetail", "Lcom/example/yakultscanner/api/ReportModuleDetailResponse;", "serialLookup", "Lcom/example/yakultscanner/api/SerialLookupResponse;", "getItemMovement", "Lcom/example/yakultscanner/api/ItemMovementResponse;", "itemId", "getCallTickets", "Lcom/example/yakultscanner/api/CallTicketListResponse;", "status", "search", "priority", "issueType", "page", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;IILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallTicketDetail", "Lcom/example/yakultscanner/api/CallTicketDetailResponse;", "ticketId", "createCallTicket", "Lcom/example/yakultscanner/api/CallTicketCreateResponse;", "Lcom/example/yakultscanner/api/CreateTicketRequest;", "(Lcom/example/yakultscanner/api/CreateTicketRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "callTicketAction", "Lcom/example/yakultscanner/api/CallTicketActionResponse;", "Lcom/example/yakultscanner/api/CallTicketActionRequest;", "(Lcom/example/yakultscanner/api/CallTicketActionRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "callTicketResolution", "Lcom/example/yakultscanner/api/ResolutionRequest;", "(Lcom/example/yakultscanner/api/ResolutionRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallItemsLookup", "Lcom/example/yakultscanner/api/CallItemLookupResponse;", "type", "getCallConditions", "Lcom/example/yakultscanner/api/CallConditionResponse;", "getCallCompanies", "Lcom/example/yakultscanner/api/CallLookupResponse;", "getCallDepartments", "comId", "(Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallBranches", "deptId", "(Ljava/lang/Integer;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallEmployees", "branchId", "(ILjava/lang/Integer;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallItEmployees", "getEscalationSettings", "Lcom/example/yakultscanner/api/EscalationSettingsResponse;", "setEscalationOverride", "Lcom/example/yakultscanner/api/SetEscalationOverrideResponse;", "Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;", "(Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getDispatchSet", "Lcom/example/yakultscanner/api/DispatchSetResponse;", "getCallDashboard", "Lcom/example/yakultscanner/api/CallDashboardResponse;", "app_prodDebug"})
public abstract interface YakultApiService {
    
    @retrofit2.http.POST(value = "mobile-auth-login.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object login(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.LoginRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.LoginResponse>> $completion);
    
    @retrofit2.http.POST(value = "api/auth/register")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object register(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.RegisterRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.RegisterResponse>> $completion);
    
    @retrofit2.http.POST(value = "api/SetUpdates/Upload")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object uploadSetUpdates(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetUpdatesRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetUpdatesResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/SetUpdates")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetUpdates(@retrofit2.http.Query(value = "setCode")
    @org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.SetItemUpdateDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/SetUpdates/PendingCount")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getPendingIssues(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.PendingIssuesResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/SetUpdates/Processed")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getProcessedUpdates(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.SetItemUpdateDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/SetUpdates/Pending")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getPendingUpdates(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.SetItemUpdateDto>>> $completion);
    
    @retrofit2.http.POST(value = "api/Items/CreateBatch")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object createBatchItems(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BatchItemsRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BatchItemsResponse>> $completion);
    
    @retrofit2.http.POST(value = "api.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object sendSerialToWindows(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SerialFromMobileRequest request, @retrofit2.http.Query(value = "__route")
    @org.jetbrains.annotations.NotNull()
    java.lang.String route, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SerialFromMobileResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/Items/Categories")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getItemCategories(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/Items/Conditions")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getConditions(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.ConditionDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/Items/Vendors")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getVendors(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.VendorDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/sets/by-token/{token}")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetByToken(@retrofit2.http.Path(value = "token")
    @org.jetbrains.annotations.NotNull()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.data.model.DispatchSet>> $completion);
    
    @retrofit2.http.GET(value = "set-history.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetDeploymentHistory(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.NotNull()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetDeploymentHistoryResponse>> $completion);
    
    @retrofit2.http.GET(value = "set-items-detail.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetItemsDetailed(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.NotNull()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetItemsDetailResponse>> $completion);
    
    @retrofit2.http.POST(value = "set-confirm.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object confirmSetDeployment(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.NotNull()
    java.lang.String token, @retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetConfirmationRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetConfirmationResponse>> $completion);
    
    @retrofit2.http.POST(value = "set-image-upload.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object uploadSetImage(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.Nullable()
    java.lang.String token, @retrofit2.http.Query(value = "set_code")
    @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetImageUploadRequest request, @retrofit2.http.Query(value = "document_number")
    @org.jetbrains.annotations.Nullable()
    java.lang.String documentNumber, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetImageUploadResponse>> $completion);
    
    @retrofit2.http.GET(value = "set-images.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetImages(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.Nullable()
    java.lang.String token, @retrofit2.http.Query(value = "set_code")
    @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetImagesResponse>> $completion);
    
    @retrofit2.http.POST(value = "receipt-upload.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object uploadReceiptImage(@retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.Nullable()
    java.lang.String token, @retrofit2.http.Query(value = "set_code")
    @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @retrofit2.http.Query(value = "document_number")
    @org.jetbrains.annotations.Nullable()
    java.lang.String documentNumber, @retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ReceiptImageUploadRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ReceiptImageUploadResponse>> $completion);
    
    @retrofit2.http.PUT(value = "api/dispatch/{setId}")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deploySet(@retrofit2.http.Path(value = "setId")
    int setId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.DeployResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/dispatch/resolve-token/{token}")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object resolveToken(@retrofit2.http.Path(value = "token")
    @org.jetbrains.annotations.NotNull()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ResolveTokenResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/dispatch/status")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSetStatuses(@retrofit2.http.Query(value = "setCodes")
    @org.jetbrains.annotations.NotNull()
    java.util.List<java.lang.String> setCodes, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.SetStatusDto>>> $completion);
    
    @retrofit2.http.GET(value = "mobile-dispatch-count.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getDispatchCount(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.DispatchCountResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Resolve")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object resolveBorrow(@retrofit2.http.Query(value = "serial")
    @org.jetbrains.annotations.NotNull()
    java.lang.String serial, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowResolveResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Employees")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object searchBorrowEmployees(@retrofit2.http.Query(value = "q")
    @org.jetbrains.annotations.Nullable()
    java.lang.String query, @retrofit2.http.Query(value = "companyId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer companyId, @retrofit2.http.Query(value = "departmentId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer departmentId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.BorrowEmployeeDto>>> $completion);
    
    @retrofit2.http.POST(value = "api/Borrow/Employees")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object createBorrowEmployee(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BorrowEmployeeCreateRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowEmployeeDto>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Companies")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBorrowCompanies(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.BorrowCompanyDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Branches")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBorrowBranches(@retrofit2.http.Query(value = "companyId")
    int companyId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.BorrowBranchDto>>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Departments")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBorrowDepartments(@retrofit2.http.Query(value = "companyId")
    int companyId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.BorrowDepartmentDto>>> $completion);
    
    @retrofit2.http.POST(value = "api/Borrow")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object borrowItem(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BorrowCreateRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowActionResponse>> $completion);
    
    @retrofit2.http.POST(value = "api/Borrow/Return")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object returnBorrow(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BorrowReturnRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowActionResponse>> $completion);
    
    @retrofit2.http.POST(value = "api/Borrow/Delete")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deleteBorrow(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BorrowDeleteRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowActionResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/Open")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getOpenBorrows(@retrofit2.http.Query(value = "serialContains")
    @org.jetbrains.annotations.Nullable()
    java.lang.String serialContains, @retrofit2.http.Query(value = "pageIndex")
    int pageIndex, @retrofit2.http.Query(value = "pageSize")
    int pageSize, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowLogPageResponse>> $completion);
    
    @retrofit2.http.GET(value = "mobile-borrow-home.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBorrowHomeSummary(@retrofit2.http.Query(value = "recentCount")
    int recentCount, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowHomeSummaryResponse>> $completion);
    
    @retrofit2.http.GET(value = "api/Borrow/History")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBorrowHistory(@retrofit2.http.Query(value = "serialContains")
    @org.jetbrains.annotations.Nullable()
    java.lang.String serialContains, @retrofit2.http.Query(value = "pageIndex")
    int pageIndex, @retrofit2.http.Query(value = "pageSize")
    int pageSize, @retrofit2.http.Query(value = "fromUtc")
    @org.jetbrains.annotations.Nullable()
    java.lang.String fromUtc, @retrofit2.http.Query(value = "toUtc")
    @org.jetbrains.annotations.Nullable()
    java.lang.String toUtc, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BorrowLogPageResponse>> $completion);
    
    @retrofit2.http.GET(value = "api.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getReportsSummary(@retrofit2.http.Query(value = "range")
    @org.jetbrains.annotations.NotNull()
    java.lang.String range, @retrofit2.http.Query(value = "__route")
    @org.jetbrains.annotations.NotNull()
    java.lang.String route, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ReportsSummaryResponse>> $completion);
    
    @retrofit2.http.GET(value = "api.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getReportModuleDetail(@retrofit2.http.Query(value = "range")
    @org.jetbrains.annotations.NotNull()
    java.lang.String range, @retrofit2.http.Query(value = "__route")
    @org.jetbrains.annotations.NotNull()
    java.lang.String route, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ReportModuleDetailResponse>> $completion);
    
    @retrofit2.http.GET(value = "slookup.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object serialLookup(@retrofit2.http.Query(value = "serial")
    @org.jetbrains.annotations.NotNull()
    java.lang.String serial, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SerialLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "item-movement.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getItemMovement(@retrofit2.http.Query(value = "itemId")
    int itemId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ItemMovementResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-tickets.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallTickets(@retrofit2.http.Query(value = "status")
    @org.jetbrains.annotations.Nullable()
    java.lang.String status, @retrofit2.http.Query(value = "search")
    @org.jetbrains.annotations.Nullable()
    java.lang.String search, @retrofit2.http.Query(value = "priority")
    @org.jetbrains.annotations.Nullable()
    java.lang.String priority, @retrofit2.http.Query(value = "issueType")
    @org.jetbrains.annotations.Nullable()
    java.lang.String issueType, @retrofit2.http.Query(value = "page")
    int page, @retrofit2.http.Query(value = "pageSize")
    int pageSize, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketListResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-ticket-detail.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallTicketDetail(@retrofit2.http.Query(value = "ticketId")
    int ticketId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketDetailResponse>> $completion);
    
    @retrofit2.http.POST(value = "call-tickets.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object createCallTicket(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CreateTicketRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketCreateResponse>> $completion);
    
    @retrofit2.http.POST(value = "call-ticket-action.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object callTicketAction(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CallTicketActionRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion);
    
    @retrofit2.http.POST(value = "call-ticket-action.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object callTicketResolution(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ResolutionRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-items-lookup.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallItemsLookup(@retrofit2.http.Query(value = "type")
    @org.jetbrains.annotations.NotNull()
    java.lang.String type, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallItemLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-conditions.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallConditions(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallConditionResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-companies.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallCompanies(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-departments.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallDepartments(@retrofit2.http.Query(value = "comId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-branches.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallBranches(@retrofit2.http.Query(value = "comId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @retrofit2.http.Query(value = "deptId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer deptId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-employees.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallEmployees(@retrofit2.http.Query(value = "deptId")
    int deptId, @retrofit2.http.Query(value = "comId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @retrofit2.http.Query(value = "branchId")
    @org.jetbrains.annotations.Nullable()
    java.lang.Integer branchId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-it-employees.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallItEmployees(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-escalation-settings.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getEscalationSettings(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.EscalationSettingsResponse>> $completion);
    
    @retrofit2.http.POST(value = "call-set-escalation-override.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object setEscalationOverride(@retrofit2.http.Body()
    @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetEscalationOverrideRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetEscalationOverrideResponse>> $completion);
    
    @retrofit2.http.GET(value = "dispatch-set.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getDispatchSet(@retrofit2.http.Query(value = "setCode")
    @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @retrofit2.http.Query(value = "token")
    @org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.DispatchSetResponse>> $completion);
    
    @retrofit2.http.GET(value = "call-dashboard.ashx")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getCallDashboard(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallDashboardResponse>> $completion);
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 3, xi = 48)
    public static final class DefaultImpls {
    }
}