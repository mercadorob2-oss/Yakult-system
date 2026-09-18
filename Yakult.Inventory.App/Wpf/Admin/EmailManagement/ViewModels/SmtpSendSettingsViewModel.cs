using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels
{
    public class SmtpSendSettingsViewModel : ViewModelBase
    {
        private readonly SystemSettingRepository _settingRepo;
        private readonly EmailRepository _emailRepo;

        private bool _smtpEnabled = true;
        private bool _isLoading;
        private SystemSmtpProfileDto _selectedGlobalProfile;

        public bool SmtpEnabled { get => _smtpEnabled; private set => SetField(ref _smtpEnabled, value); }
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        public const int NoneProfileSentinelId = int.MinValue;

        public ObservableCollection<EmailTemplateDto> Templates { get; } = new ObservableCollection<EmailTemplateDto>();
        public ObservableCollection<SystemSmtpProfileDto> Profiles { get; } = new ObservableCollection<SystemSmtpProfileDto>();

        /// <summary>Profiles plus a leading "— None —" sentinel, for the per-template picker only.</summary>
        public ObservableCollection<SystemSmtpProfileDto> ProfilesWithNone { get; } = new ObservableCollection<SystemSmtpProfileDto>();

        public SystemSmtpProfileDto SelectedGlobalProfile
        {
            get => _selectedGlobalProfile;
            set => SetField(ref _selectedGlobalProfile, value);
        }

        public SmtpSendSettingsViewModel(SystemSettingRepository settingRepo, EmailRepository emailRepo)
        {
            _settingRepo = settingRepo;
            _emailRepo = emailRepo;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var smtpTask = _settingRepo.GetSmtpEnabledAsync();
                var tplTask = _emailRepo.GetEmailTemplatesAsync(activeOnly: false);
                var profTask = _emailRepo.GetSmtpProfilesAsync(activeOnly: false);

                await Task.WhenAll(smtpTask, tplTask, profTask);

                SmtpEnabled = smtpTask.Result;

                Templates.Clear();
                foreach (var t in (tplTask.Result ?? new System.Collections.Generic.List<EmailTemplateDto>())
                             .OrderBy(t => t.TemplateId))
                    Templates.Add(t);

                var previouslySelectedId = SelectedGlobalProfile?.ProfileId;
                Profiles.Clear();
                foreach (var p in profTask.Result ?? new System.Collections.Generic.List<SystemSmtpProfileDto>())
                    Profiles.Add(p);

                ProfilesWithNone.Clear();
                ProfilesWithNone.Add(new SystemSmtpProfileDto { ProfileId = NoneProfileSentinelId, ProfileName = "— None —" });
                foreach (var p in Profiles)
                    ProfilesWithNone.Add(p);

                SelectedGlobalProfile = previouslySelectedId.HasValue
                    ? Profiles.FirstOrDefault(p => p.ProfileId == previouslySelectedId.Value) ?? Profiles.FirstOrDefault()
                    : Profiles.FirstOrDefault();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task SetGlobalSmtpEnabledAsync(bool enabled, int userId)
        {
            await _settingRepo.SetSmtpEnabledAsync(enabled, userId);
            SmtpEnabled = enabled;
        }

        public async Task SetTemplateProfileAsync(EmailTemplateDto template, int? profileId, string profileName)
        {
            var oldId = template.DefaultSmtpProfileId;
            var oldName = template.DefaultSmtpProfileName;
            try
            {
                await _emailRepo.SetEmailTemplateSmtpProfileAsync(template.TemplateId, profileId);
                template.DefaultSmtpProfileId = profileId;
                template.DefaultSmtpProfileName = profileId.HasValue ? profileName : null;
            }
            catch
            {
                template.DefaultSmtpProfileId = oldId;
                template.DefaultSmtpProfileName = oldName;
                throw;
            }
        }

        public async Task ApplyProfileToTemplatesAsync(SystemSmtpProfileDto profile, System.Collections.Generic.List<EmailTemplateDto> targets)
        {
            if (targets.Count == Templates.Count)
            {
                await _emailRepo.SetAllEmailTemplatesSmtpProfileAsync(profile.ProfileId);
            }
            else
            {
                foreach (var t in targets)
                    await _emailRepo.SetEmailTemplateSmtpProfileAsync(t.TemplateId, profile.ProfileId);
            }

            foreach (var t in targets)
            {
                t.DefaultSmtpProfileId = profile.ProfileId;
                t.DefaultSmtpProfileName = profile.ProfileName;
            }
        }

        public async Task SetTemplateActiveAsync(EmailTemplateDto template, bool isActive)
        {
            await _emailRepo.SetEmailTemplateIsActiveAsync(template.TemplateId, isActive);
            template.IsActive = isActive;
        }

        public async Task TurnOffTemplatesAsync(System.Collections.Generic.List<int> templateIds)
        {
            foreach (var id in templateIds)
            {
                await _emailRepo.SetEmailTemplateIsActiveAsync(id, false);
                var t = Templates.FirstOrDefault(x => x.TemplateId == id);
                if (t != null) t.IsActive = false;
            }
        }
    }
}
