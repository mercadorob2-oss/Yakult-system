using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels
{
    public class EmailConfigurationViewModel
    {
        public EmailRepository EmailRepo { get; }
        public SystemEmailNotificationService EmailService { get; }

        public EmailAddressesViewModel EmailAddresses { get; }
        public SmtpProfilesViewModel   SmtpProfiles   { get; }
        public EmailTemplatesViewModel EmailTemplates { get; }

        public EmailConfigurationViewModel()
        {
            EmailRepo    = new EmailRepository();
            EmailService = new SystemEmailNotificationService(EmailRepo);

            EmailAddresses = new EmailAddressesViewModel(EmailRepo);
            SmtpProfiles   = new SmtpProfilesViewModel(EmailRepo);
            EmailTemplates = new EmailTemplatesViewModel(EmailRepo);
        }

        public async Task LoadAllAsync()
        {
            await EmailAddresses.LoadAsync();
            await SmtpProfiles.LoadAsync();
            await EmailTemplates.LoadAsync();
        }
    }
}
