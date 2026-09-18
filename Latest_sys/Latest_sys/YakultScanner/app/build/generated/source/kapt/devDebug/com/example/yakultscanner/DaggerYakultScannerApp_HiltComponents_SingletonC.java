package com.example.yakultscanner;

import android.app.Activity;
import android.app.Service;
import android.content.Context;
import android.view.View;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.SavedStateHandle;
import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.db.AppDatabase;
import com.example.yakultscanner.data.db.LocalScanDao;
import com.example.yakultscanner.data.db.PendingUpdateDao;
import com.example.yakultscanner.data.repository.CallMonitoringRepository;
import com.example.yakultscanner.data.repository.InventoryRepository;
import com.example.yakultscanner.data.repository.LocalScanRepository;
import com.example.yakultscanner.data.repository.LocalScanRepository_Factory;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.data.repository.OfflineRepository_Factory;
import com.example.yakultscanner.data.repository.SyncRepository;
import com.example.yakultscanner.data.repository.SyncRepository_Factory;
import com.example.yakultscanner.di.DatabaseModule_ProvideAppDatabaseFactory;
import com.example.yakultscanner.di.DatabaseModule_ProvideConnectivityMonitorFactory;
import com.example.yakultscanner.di.DatabaseModule_ProvideLocalScanDaoFactory;
import com.example.yakultscanner.di.DatabaseModule_ProvidePendingUpdateDaoFactory;
import com.example.yakultscanner.di.NetworkModule_ProvideCallMonitoringRepositoryFactory;
import com.example.yakultscanner.di.NetworkModule_ProvideInventoryRepositoryFactory;
import com.example.yakultscanner.di.NetworkModule_ProvideYakultApiServiceFactory;
import com.example.yakultscanner.network.ConnectivityMonitor;
import com.example.yakultscanner.ui.screens.HomeViewModel;
import com.example.yakultscanner.ui.screens.HomeViewModel_Factory;
import com.example.yakultscanner.ui.screens.HomeViewModel_HiltModules;
import com.example.yakultscanner.ui.screens.HomeViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.ui.screens.HomeViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.ui.screens.PendingUpdatesViewModel;
import com.example.yakultscanner.ui.screens.PendingUpdatesViewModel_Factory;
import com.example.yakultscanner.ui.screens.PendingUpdatesViewModel_HiltModules;
import com.example.yakultscanner.ui.screens.PendingUpdatesViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.ui.screens.PendingUpdatesViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel_Factory;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel_HiltModules;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel_Factory;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel_HiltModules;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel_Factory;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel_HiltModules;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel_Factory;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel_HiltModules;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel_Factory;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel_HiltModules;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel_HiltModules_BindsModule_Binds_LazyMapKey;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel_HiltModules_KeyModule_Provide_LazyMapKey;
import com.example.yakultscanner.worker.SyncWorkScheduler;
import com.example.yakultscanner.worker.SyncWorkScheduler_Factory;
import dagger.hilt.android.ActivityRetainedLifecycle;
import dagger.hilt.android.ViewModelLifecycle;
import dagger.hilt.android.internal.builders.ActivityComponentBuilder;
import dagger.hilt.android.internal.builders.ActivityRetainedComponentBuilder;
import dagger.hilt.android.internal.builders.FragmentComponentBuilder;
import dagger.hilt.android.internal.builders.ServiceComponentBuilder;
import dagger.hilt.android.internal.builders.ViewComponentBuilder;
import dagger.hilt.android.internal.builders.ViewModelComponentBuilder;
import dagger.hilt.android.internal.builders.ViewWithFragmentComponentBuilder;
import dagger.hilt.android.internal.lifecycle.DefaultViewModelFactories;
import dagger.hilt.android.internal.lifecycle.DefaultViewModelFactories_InternalFactoryFactory_Factory;
import dagger.hilt.android.internal.managers.ActivityRetainedComponentManager_LifecycleModule_ProvideActivityRetainedLifecycleFactory;
import dagger.hilt.android.internal.managers.SavedStateHandleHolder;
import dagger.hilt.android.internal.modules.ApplicationContextModule;
import dagger.hilt.android.internal.modules.ApplicationContextModule_ProvideContextFactory;
import dagger.internal.DaggerGenerated;
import dagger.internal.DoubleCheck;
import dagger.internal.LazyClassKeyMap;
import dagger.internal.MapBuilder;
import dagger.internal.Preconditions;
import dagger.internal.Provider;
import java.util.Collections;
import java.util.Map;
import java.util.Set;
import javax.annotation.processing.Generated;

@DaggerGenerated
@Generated(
    value = "dagger.internal.codegen.ComponentProcessor",
    comments = "https://dagger.dev"
)
@SuppressWarnings({
    "unchecked",
    "rawtypes",
    "KotlinInternal",
    "KotlinInternalInJava",
    "cast",
    "deprecation",
    "nullness:initialization.field.uninitialized"
})
public final class DaggerYakultScannerApp_HiltComponents_SingletonC {
  private DaggerYakultScannerApp_HiltComponents_SingletonC() {
  }

  public static Builder builder() {
    return new Builder();
  }

  public static final class Builder {
    private ApplicationContextModule applicationContextModule;

    private Builder() {
    }

    public Builder applicationContextModule(ApplicationContextModule applicationContextModule) {
      this.applicationContextModule = Preconditions.checkNotNull(applicationContextModule);
      return this;
    }

    public YakultScannerApp_HiltComponents.SingletonC build() {
      Preconditions.checkBuilderRequirement(applicationContextModule, ApplicationContextModule.class);
      return new SingletonCImpl(applicationContextModule);
    }
  }

  private static final class ActivityRetainedCBuilder implements YakultScannerApp_HiltComponents.ActivityRetainedC.Builder {
    private final SingletonCImpl singletonCImpl;

    private SavedStateHandleHolder savedStateHandleHolder;

    private ActivityRetainedCBuilder(SingletonCImpl singletonCImpl) {
      this.singletonCImpl = singletonCImpl;
    }

    @Override
    public ActivityRetainedCBuilder savedStateHandleHolder(
        SavedStateHandleHolder savedStateHandleHolder) {
      this.savedStateHandleHolder = Preconditions.checkNotNull(savedStateHandleHolder);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ActivityRetainedC build() {
      Preconditions.checkBuilderRequirement(savedStateHandleHolder, SavedStateHandleHolder.class);
      return new ActivityRetainedCImpl(singletonCImpl, savedStateHandleHolder);
    }
  }

  private static final class ActivityCBuilder implements YakultScannerApp_HiltComponents.ActivityC.Builder {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private Activity activity;

    private ActivityCBuilder(SingletonCImpl singletonCImpl,
        ActivityRetainedCImpl activityRetainedCImpl) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
    }

    @Override
    public ActivityCBuilder activity(Activity activity) {
      this.activity = Preconditions.checkNotNull(activity);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ActivityC build() {
      Preconditions.checkBuilderRequirement(activity, Activity.class);
      return new ActivityCImpl(singletonCImpl, activityRetainedCImpl, activity);
    }
  }

  private static final class FragmentCBuilder implements YakultScannerApp_HiltComponents.FragmentC.Builder {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private Fragment fragment;

    private FragmentCBuilder(SingletonCImpl singletonCImpl,
        ActivityRetainedCImpl activityRetainedCImpl, ActivityCImpl activityCImpl) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;
    }

    @Override
    public FragmentCBuilder fragment(Fragment fragment) {
      this.fragment = Preconditions.checkNotNull(fragment);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.FragmentC build() {
      Preconditions.checkBuilderRequirement(fragment, Fragment.class);
      return new FragmentCImpl(singletonCImpl, activityRetainedCImpl, activityCImpl, fragment);
    }
  }

  private static final class ViewWithFragmentCBuilder implements YakultScannerApp_HiltComponents.ViewWithFragmentC.Builder {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private final FragmentCImpl fragmentCImpl;

    private View view;

    private ViewWithFragmentCBuilder(SingletonCImpl singletonCImpl,
        ActivityRetainedCImpl activityRetainedCImpl, ActivityCImpl activityCImpl,
        FragmentCImpl fragmentCImpl) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;
      this.fragmentCImpl = fragmentCImpl;
    }

    @Override
    public ViewWithFragmentCBuilder view(View view) {
      this.view = Preconditions.checkNotNull(view);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ViewWithFragmentC build() {
      Preconditions.checkBuilderRequirement(view, View.class);
      return new ViewWithFragmentCImpl(singletonCImpl, activityRetainedCImpl, activityCImpl, fragmentCImpl, view);
    }
  }

  private static final class ViewCBuilder implements YakultScannerApp_HiltComponents.ViewC.Builder {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private View view;

    private ViewCBuilder(SingletonCImpl singletonCImpl, ActivityRetainedCImpl activityRetainedCImpl,
        ActivityCImpl activityCImpl) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;
    }

    @Override
    public ViewCBuilder view(View view) {
      this.view = Preconditions.checkNotNull(view);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ViewC build() {
      Preconditions.checkBuilderRequirement(view, View.class);
      return new ViewCImpl(singletonCImpl, activityRetainedCImpl, activityCImpl, view);
    }
  }

  private static final class ViewModelCBuilder implements YakultScannerApp_HiltComponents.ViewModelC.Builder {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private SavedStateHandle savedStateHandle;

    private ViewModelLifecycle viewModelLifecycle;

    private ViewModelCBuilder(SingletonCImpl singletonCImpl,
        ActivityRetainedCImpl activityRetainedCImpl) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
    }

    @Override
    public ViewModelCBuilder savedStateHandle(SavedStateHandle handle) {
      this.savedStateHandle = Preconditions.checkNotNull(handle);
      return this;
    }

    @Override
    public ViewModelCBuilder viewModelLifecycle(ViewModelLifecycle viewModelLifecycle) {
      this.viewModelLifecycle = Preconditions.checkNotNull(viewModelLifecycle);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ViewModelC build() {
      Preconditions.checkBuilderRequirement(savedStateHandle, SavedStateHandle.class);
      Preconditions.checkBuilderRequirement(viewModelLifecycle, ViewModelLifecycle.class);
      return new ViewModelCImpl(singletonCImpl, activityRetainedCImpl, savedStateHandle, viewModelLifecycle);
    }
  }

  private static final class ServiceCBuilder implements YakultScannerApp_HiltComponents.ServiceC.Builder {
    private final SingletonCImpl singletonCImpl;

    private Service service;

    private ServiceCBuilder(SingletonCImpl singletonCImpl) {
      this.singletonCImpl = singletonCImpl;
    }

    @Override
    public ServiceCBuilder service(Service service) {
      this.service = Preconditions.checkNotNull(service);
      return this;
    }

    @Override
    public YakultScannerApp_HiltComponents.ServiceC build() {
      Preconditions.checkBuilderRequirement(service, Service.class);
      return new ServiceCImpl(singletonCImpl, service);
    }
  }

  private static final class ViewWithFragmentCImpl extends YakultScannerApp_HiltComponents.ViewWithFragmentC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private final FragmentCImpl fragmentCImpl;

    private final ViewWithFragmentCImpl viewWithFragmentCImpl = this;

    ViewWithFragmentCImpl(SingletonCImpl singletonCImpl,
        ActivityRetainedCImpl activityRetainedCImpl, ActivityCImpl activityCImpl,
        FragmentCImpl fragmentCImpl, View viewParam) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;
      this.fragmentCImpl = fragmentCImpl;


    }
  }

  private static final class FragmentCImpl extends YakultScannerApp_HiltComponents.FragmentC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private final FragmentCImpl fragmentCImpl = this;

    FragmentCImpl(SingletonCImpl singletonCImpl, ActivityRetainedCImpl activityRetainedCImpl,
        ActivityCImpl activityCImpl, Fragment fragmentParam) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;


    }

    @Override
    public DefaultViewModelFactories.InternalFactoryFactory getHiltInternalFactoryFactory() {
      return activityCImpl.getHiltInternalFactoryFactory();
    }

    @Override
    public ViewWithFragmentComponentBuilder viewWithFragmentComponentBuilder() {
      return new ViewWithFragmentCBuilder(singletonCImpl, activityRetainedCImpl, activityCImpl, fragmentCImpl);
    }
  }

  private static final class ViewCImpl extends YakultScannerApp_HiltComponents.ViewC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl;

    private final ViewCImpl viewCImpl = this;

    ViewCImpl(SingletonCImpl singletonCImpl, ActivityRetainedCImpl activityRetainedCImpl,
        ActivityCImpl activityCImpl, View viewParam) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;
      this.activityCImpl = activityCImpl;


    }
  }

  private static final class ActivityCImpl extends YakultScannerApp_HiltComponents.ActivityC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ActivityCImpl activityCImpl = this;

    ActivityCImpl(SingletonCImpl singletonCImpl, ActivityRetainedCImpl activityRetainedCImpl,
        Activity activityParam) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;


    }

    Map keySetMapOfClassOfAndBooleanBuilder() {
      MapBuilder mapBuilder = MapBuilder.<String, Boolean>newMapBuilder(7);
      mapBuilder.put(BatchEntryViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, BatchEntryViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(CallDashboardViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, CallDashboardViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(CallMonitoringViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, CallMonitoringViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(DispatchDetailsViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, DispatchDetailsViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(HomeViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, HomeViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(LocalSerialScanViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, LocalSerialScanViewModel_HiltModules.KeyModule.provide());
      mapBuilder.put(PendingUpdatesViewModel_HiltModules_KeyModule_Provide_LazyMapKey.lazyClassKeyName, PendingUpdatesViewModel_HiltModules.KeyModule.provide());
      return mapBuilder.build();
    }

    @Override
    public void injectMainActivity(MainActivity mainActivity) {
    }

    @Override
    public DefaultViewModelFactories.InternalFactoryFactory getHiltInternalFactoryFactory() {
      return DefaultViewModelFactories_InternalFactoryFactory_Factory.newInstance(getViewModelKeys(), new ViewModelCBuilder(singletonCImpl, activityRetainedCImpl));
    }

    @Override
    public Map<Class<?>, Boolean> getViewModelKeys() {
      return LazyClassKeyMap.<Boolean>of(keySetMapOfClassOfAndBooleanBuilder());
    }

    @Override
    public ViewModelComponentBuilder getViewModelComponentBuilder() {
      return new ViewModelCBuilder(singletonCImpl, activityRetainedCImpl);
    }

    @Override
    public FragmentComponentBuilder fragmentComponentBuilder() {
      return new FragmentCBuilder(singletonCImpl, activityRetainedCImpl, activityCImpl);
    }

    @Override
    public ViewComponentBuilder viewComponentBuilder() {
      return new ViewCBuilder(singletonCImpl, activityRetainedCImpl, activityCImpl);
    }
  }

  private static final class ViewModelCImpl extends YakultScannerApp_HiltComponents.ViewModelC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl;

    private final ViewModelCImpl viewModelCImpl = this;

    Provider<BatchEntryViewModel> batchEntryViewModelProvider;

    Provider<CallDashboardViewModel> callDashboardViewModelProvider;

    Provider<CallMonitoringViewModel> callMonitoringViewModelProvider;

    Provider<DispatchDetailsViewModel> dispatchDetailsViewModelProvider;

    Provider<HomeViewModel> homeViewModelProvider;

    Provider<LocalSerialScanViewModel> localSerialScanViewModelProvider;

    Provider<PendingUpdatesViewModel> pendingUpdatesViewModelProvider;

    ViewModelCImpl(SingletonCImpl singletonCImpl, ActivityRetainedCImpl activityRetainedCImpl,
        SavedStateHandle savedStateHandleParam, ViewModelLifecycle viewModelLifecycleParam) {
      this.singletonCImpl = singletonCImpl;
      this.activityRetainedCImpl = activityRetainedCImpl;

      initialize(savedStateHandleParam, viewModelLifecycleParam);

    }

    Map hiltViewModelMapMapOfClassOfAndProviderOfViewModelBuilder() {
      MapBuilder mapBuilder = MapBuilder.<String, javax.inject.Provider<ViewModel>>newMapBuilder(7);
      mapBuilder.put(BatchEntryViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (batchEntryViewModelProvider)));
      mapBuilder.put(CallDashboardViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (callDashboardViewModelProvider)));
      mapBuilder.put(CallMonitoringViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (callMonitoringViewModelProvider)));
      mapBuilder.put(DispatchDetailsViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (dispatchDetailsViewModelProvider)));
      mapBuilder.put(HomeViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (homeViewModelProvider)));
      mapBuilder.put(LocalSerialScanViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (localSerialScanViewModelProvider)));
      mapBuilder.put(PendingUpdatesViewModel_HiltModules_BindsModule_Binds_LazyMapKey.lazyClassKeyName, ((Provider) (pendingUpdatesViewModelProvider)));
      return mapBuilder.build();
    }

    @SuppressWarnings("unchecked")
    private void initialize(final SavedStateHandle savedStateHandleParam,
        final ViewModelLifecycle viewModelLifecycleParam) {
      this.batchEntryViewModelProvider = BatchEntryViewModel_Factory.create(singletonCImpl.provideInventoryRepositoryProvider);
      this.callDashboardViewModelProvider = CallDashboardViewModel_Factory.create(singletonCImpl.provideCallMonitoringRepositoryProvider);
      this.callMonitoringViewModelProvider = CallMonitoringViewModel_Factory.create(singletonCImpl.provideCallMonitoringRepositoryProvider);
      this.dispatchDetailsViewModelProvider = DispatchDetailsViewModel_Factory.create(singletonCImpl.provideYakultApiServiceProvider, singletonCImpl.offlineRepositoryProvider, singletonCImpl.syncRepositoryProvider, singletonCImpl.provideConnectivityMonitorProvider);
      this.homeViewModelProvider = HomeViewModel_Factory.create(singletonCImpl.offlineRepositoryProvider, singletonCImpl.provideConnectivityMonitorProvider);
      this.localSerialScanViewModelProvider = LocalSerialScanViewModel_Factory.create(singletonCImpl.localScanRepositoryProvider);
      this.pendingUpdatesViewModelProvider = PendingUpdatesViewModel_Factory.create(singletonCImpl.offlineRepositoryProvider, singletonCImpl.syncRepositoryProvider);
    }

    @Override
    public Map<Class<?>, javax.inject.Provider<ViewModel>> getHiltViewModelMap() {
      return LazyClassKeyMap.<javax.inject.Provider<ViewModel>>of(hiltViewModelMapMapOfClassOfAndProviderOfViewModelBuilder());
    }

    @Override
    public Map<Class<?>, Object> getHiltViewModelAssistedMap() {
      return Collections.<Class<?>, Object>emptyMap();
    }
  }

  private static final class ActivityRetainedCImpl extends YakultScannerApp_HiltComponents.ActivityRetainedC {
    private final SingletonCImpl singletonCImpl;

    private final ActivityRetainedCImpl activityRetainedCImpl = this;

    Provider<ActivityRetainedLifecycle> provideActivityRetainedLifecycleProvider;

    ActivityRetainedCImpl(SingletonCImpl singletonCImpl,
        SavedStateHandleHolder savedStateHandleHolderParam) {
      this.singletonCImpl = singletonCImpl;

      initialize(savedStateHandleHolderParam);

    }

    @SuppressWarnings("unchecked")
    private void initialize(final SavedStateHandleHolder savedStateHandleHolderParam) {
      this.provideActivityRetainedLifecycleProvider = DoubleCheck.provider(ActivityRetainedComponentManager_LifecycleModule_ProvideActivityRetainedLifecycleFactory.create());
    }

    @Override
    public ActivityComponentBuilder activityComponentBuilder() {
      return new ActivityCBuilder(singletonCImpl, activityRetainedCImpl);
    }

    @Override
    public ActivityRetainedLifecycle getActivityRetainedLifecycle() {
      return provideActivityRetainedLifecycleProvider.get();
    }
  }

  private static final class ServiceCImpl extends YakultScannerApp_HiltComponents.ServiceC {
    private final SingletonCImpl singletonCImpl;

    private final ServiceCImpl serviceCImpl = this;

    ServiceCImpl(SingletonCImpl singletonCImpl, Service serviceParam) {
      this.singletonCImpl = singletonCImpl;


    }
  }

  private static final class SingletonCImpl extends YakultScannerApp_HiltComponents.SingletonC {
    private final SingletonCImpl singletonCImpl = this;

    Provider<Context> provideContextProvider;

    Provider<SyncWorkScheduler> syncWorkSchedulerProvider;

    Provider<YakultApiService> provideYakultApiServiceProvider;

    Provider<InventoryRepository> provideInventoryRepositoryProvider;

    Provider<CallMonitoringRepository> provideCallMonitoringRepositoryProvider;

    Provider<AppDatabase> provideAppDatabaseProvider;

    Provider<PendingUpdateDao> providePendingUpdateDaoProvider;

    Provider<OfflineRepository> offlineRepositoryProvider;

    Provider<SyncRepository> syncRepositoryProvider;

    Provider<ConnectivityMonitor> provideConnectivityMonitorProvider;

    Provider<LocalScanDao> provideLocalScanDaoProvider;

    Provider<LocalScanRepository> localScanRepositoryProvider;

    SingletonCImpl(ApplicationContextModule applicationContextModuleParam) {

      initialize(applicationContextModuleParam);

    }

    @SuppressWarnings("unchecked")
    private void initialize(final ApplicationContextModule applicationContextModuleParam) {
      this.provideContextProvider = ApplicationContextModule_ProvideContextFactory.create(applicationContextModuleParam);
      this.syncWorkSchedulerProvider = DoubleCheck.provider(SyncWorkScheduler_Factory.create(provideContextProvider));
      this.provideYakultApiServiceProvider = DoubleCheck.provider(NetworkModule_ProvideYakultApiServiceFactory.create());
      this.provideInventoryRepositoryProvider = DoubleCheck.provider(NetworkModule_ProvideInventoryRepositoryFactory.create(provideYakultApiServiceProvider));
      this.provideCallMonitoringRepositoryProvider = DoubleCheck.provider(NetworkModule_ProvideCallMonitoringRepositoryFactory.create());
      this.provideAppDatabaseProvider = DoubleCheck.provider(DatabaseModule_ProvideAppDatabaseFactory.create(provideContextProvider));
      this.providePendingUpdateDaoProvider = DatabaseModule_ProvidePendingUpdateDaoFactory.create(provideAppDatabaseProvider);
      this.offlineRepositoryProvider = DoubleCheck.provider(OfflineRepository_Factory.create(providePendingUpdateDaoProvider));
      this.syncRepositoryProvider = DoubleCheck.provider(SyncRepository_Factory.create(offlineRepositoryProvider, provideYakultApiServiceProvider));
      this.provideConnectivityMonitorProvider = DoubleCheck.provider(DatabaseModule_ProvideConnectivityMonitorFactory.create(provideContextProvider));
      this.provideLocalScanDaoProvider = DatabaseModule_ProvideLocalScanDaoFactory.create(provideAppDatabaseProvider);
      this.localScanRepositoryProvider = DoubleCheck.provider(LocalScanRepository_Factory.create(provideLocalScanDaoProvider));
    }

    @Override
    public void injectYakultScannerApp(YakultScannerApp yakultScannerApp) {
      injectYakultScannerApp2(yakultScannerApp);
    }

    @Override
    public Set<Boolean> getDisableFragmentGetContextFix() {
      return Collections.<Boolean>emptySet();
    }

    @Override
    public ActivityRetainedComponentBuilder retainedComponentBuilder() {
      return new ActivityRetainedCBuilder(singletonCImpl);
    }

    @Override
    public ServiceComponentBuilder serviceComponentBuilder() {
      return new ServiceCBuilder(singletonCImpl);
    }

    private YakultScannerApp injectYakultScannerApp2(YakultScannerApp instance) {
      YakultScannerApp_MembersInjector.injectSyncScheduler(instance, syncWorkSchedulerProvider.get());
      return instance;
    }
  }
}
