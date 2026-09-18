using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class ApplyProfileToTemplatesDialog : Window
    {
        private readonly ObservableCollection<SelectableTemplate> _items;

        public SystemSmtpProfileDto SelectedProfile { get; private set; }
        public List<EmailTemplateDto> SelectedTemplates { get; private set; } = new List<EmailTemplateDto>();

        public ApplyProfileToTemplatesDialog(
            List<EmailTemplateDto> templates,
            List<SystemSmtpProfileDto> profiles,
            int? preselectedProfileId)
        {
            InitializeComponent();

            CboProfile.ItemsSource = profiles;
            CboProfile.SelectedItem = preselectedProfileId.HasValue
                ? profiles.FirstOrDefault(p => p.ProfileId == preselectedProfileId.Value)
                : profiles.FirstOrDefault();

            _items = new ObservableCollection<SelectableTemplate>(
                templates.Select(t => new SelectableTemplate(t)));
            foreach (var item in _items) item.PropertyChanged += Item_PropertyChanged;
            LstTemplates.ItemsSource = _items;

            UpdateSelectedCount();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateSelectedCount();

        private void UpdateSelectedCount()
            => LblSelectedCount.Text = $"{_items.Count(i => i.IsSelected)} selected";

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in _items) i.IsSelected = true;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in _items) i.IsSelected = false;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (CboProfile.SelectedItem == null)
            {
                MessageBox.Show("Please select a profile to apply.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _items.Where(i => i.IsSelected).Select(i => i.Template).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one template.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedProfile = (SystemSmtpProfileDto)CboProfile.SelectedItem;
            SelectedTemplates = selected;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private sealed class SelectableTemplate : INotifyPropertyChanged
        {
            public EmailTemplateDto Template { get; }
            public string TemplateKey => Template.TemplateKey;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public SelectableTemplate(EmailTemplateDto template) => Template = template;

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
