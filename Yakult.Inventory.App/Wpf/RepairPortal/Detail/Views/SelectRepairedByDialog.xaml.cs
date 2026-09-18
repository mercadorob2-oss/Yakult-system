using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Small checkbox-list picker for "Repaired By" — candidates are already IT-Dept.
    /// filtered by the caller (RepairTicketRepository.GetItDepartmentEmployeesAsync). Mirrors the
    /// checkbox-DataGrid multi-select pattern used elsewhere (e.g. AssignReturnsDialog), just as a
    /// plain checkbox ListBox since there's only one column of data (a name) to show here.</summary>
    public partial class SelectRepairedByDialog : Window
    {
        public sealed class EmpCheckItem : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Name { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ObservableCollection<EmpCheckItem> Items { get; } = new ObservableCollection<EmpCheckItem>();

        public List<int> SelectedIds => Items.Where(i => i.IsSelected).Select(i => i.Id).ToList();

        public SelectRepairedByDialog(List<OrgLookupOption> candidates, List<int> preselectedIds)
        {
            InitializeComponent();
            DataContext = this;

            var preselected = new HashSet<int>(preselectedIds ?? Enumerable.Empty<int>());
            foreach (var c in candidates ?? Enumerable.Empty<OrgLookupOption>())
                Items.Add(new EmpCheckItem { Id = c.Id, Name = c.Name, IsSelected = preselected.Contains(c.Id) });
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
