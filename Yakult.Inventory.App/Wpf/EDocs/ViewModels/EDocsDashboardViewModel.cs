using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Models;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.ViewModels;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Views;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.Models;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.ViewModels;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.Views;
using Yakult.Inventory.App.WPF.EDocs.Views;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Dialogs;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Services;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.ViewModels
{
    // E-Docs dashboard view-model. Each tab is a STANDALONE fill-and-print
    // form (mobile e-docs basis): typed inputs live here, Print validates and
    // opens the corresponding print preview dialog. Nothing here is tied to
    // sets, requests, cartridges, or any other record.
    public class EDocsDashboardViewModel : INotifyPropertyChanged
    {
        private int _selectedTabIndex;

        public EDocsDashboardViewModel()
        {
            RecentGatepass = new ObservableCollection<GatepassDocument>();
            TransmittalInput = new TransmittalDocument();
            TransmittalEntryItems = new ObservableCollection<TransmittalLineItem>();
            GatepassInput = new GatepassDocument();
            GatepassEntryItems = new ObservableCollection<GatepassDocument.GatepassItem>();
            // Standalone E-Docs requisition: DEPARTMENT, NOTED BY and APPROVED BY are
            // permanent (this section always requisitions on behalf of IT, and the
            // same two people always sign off), CC mirrors DEPARTMENT.
            RequisitionInput = new RequisitionFormViewModel
            {
                IsYakultPhilippines = true,
                Department          = PermanentRequisitionDepartment,
                CcRequestedBy       = PermanentRequisitionDepartment,
                NotedByName         = "MR. ROD ANG JR.",
                NotedByPosition     = "Asst. Manager",
                ApprovedByName      = "MR. Y. KAWASAKI",
                ApprovedByPosition  = "Comptroller"
            };
            ReqItems = new ObservableCollection<RequisitionFormItem>();

            NewGatepassCommand = new RelayCommand(() => SelectedTabIndex = 1);
            OpenTransmittalCommand = new RelayCommand(() => SelectedTabIndex = 0);
            OpenRequisitionCommand = new RelayCommand(() => SelectedTabIndex = 2);
            PrintTransmittalCommand = new RelayCommand(PrintTransmittal);
            PrintGatepassCommand = new RelayCommand(PrintGatepass);
            PrintRequisitionCommand = new RelayCommand(PrintRequisition);
            SaveTransmittalPdfCommand = new RelayCommand(SaveTransmittalPdf);
            SaveRequisitionPdfCommand = new RelayCommand(SaveRequisitionPdf);

            AddTransmittalItemCommand = new RelayParamCommand(_ => TransmittalEntryItems.Add(new TransmittalLineItem()));
            DeleteTransmittalItemsCommand = new RelayParamCommand(p => RemoveSelected(p, TransmittalEntryItems));
            AddGatepassItemCommand = new RelayParamCommand(_ => GatepassEntryItems.Add(new GatepassDocument.GatepassItem()));
            DeleteGatepassItemsCommand = new RelayParamCommand(p => RemoveSelected(p, GatepassEntryItems));
            AddReqItemCommand = new RelayParamCommand(_ => ReqItems.Add(new RequisitionFormItem()));
            DeleteReqItemsCommand = new RelayParamCommand(p => RemoveSelected(p, ReqItems));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<GatepassDocument> RecentGatepass { get; }

        // ── Standalone Transmittal inputs ──────────────────────────────
        public TransmittalDocument TransmittalInput { get; }

        public ObservableCollection<TransmittalLineItem> TransmittalEntryItems { get; }

        // ── Standalone Gatepass inputs ─────────────────────────────────
        public GatepassDocument GatepassInput { get; }

        public ObservableCollection<GatepassDocument.GatepassItem> GatepassEntryItems { get; }

        public List<GatepassItemCategory> GatepassCategories { get; } =
            Enum.GetValues(typeof(GatepassItemCategory)).Cast<GatepassItemCategory>().ToList();

        public List<GatepassModel> GatepassModels { get; } =
            Enum.GetValues(typeof(GatepassModel)).Cast<GatepassModel>().ToList();

        // Chip selector list — only the real printer models (mobile parity).
        public List<GatepassModel> GatepassModelChips { get; } =
            new List<GatepassModel> { GatepassModel.Lx300, GatepassModel.Lx310, GatepassModel.Lq2190 };

        public List<GatepassFormType> GatepassFormTypes { get; } =
            Enum.GetValues(typeof(GatepassFormType)).Cast<GatepassFormType>().ToList();

        // ── Standalone Requisition inputs ──────────────────────────────
        public RequisitionFormViewModel RequisitionInput { get; }

        public ObservableCollection<RequisitionFormItem> ReqItems { get; }

        // ── Signatory pick-lists, shared by the Transmittal and Gatepass tabs.
        // "-" represents leaving the field blank on the printed form.
        private const string BlankSignatory = "-";

        public List<string> ApprovedBySignatories { get; } = new List<string>
        {
            BlankSignatory,
            "RODOLFO L. ANG JR."
        };

        public List<string> NotedBySignatories { get; } = new List<string>
        {
            BlankSignatory,
            "CAROLYN A. BANZON",
            "JOSEPH M. JASMIN",
            "FROILAN A. ARTATES"
        };

        public List<string> IssuedBySignatories { get; } = new List<string>
        {
            BlankSignatory,
            "ROGELIO T. SOLANO JR.",
            "CELMAR P. CASALMER",
            "RACHEL O. POTOT",
            "ROJAN E. JIMENEZ",
            "ABIGAIL JOYNE G. PADUADA",
            "MA. ELENA P. MIRANDA",
            "CHRISTIAN GEORGE SANTOS",
            "SHAWN QUIN A. BAGNOL",
            "RUSSEL ROB C. MERCADO",
            "PRECIOUS JANE A. MIRANDA"
        };

        // Requisition PREPARED/NOTED/APPROVED BY (NAME) pick-list. Unlike the
        // Transmittal/Gatepass lists above, these fields stay editable — the
        // user can pick one of these two names, leave it blank, or type any
        // other name.
        public List<string> RequisitionSignatories { get; } = new List<string>
        {
            BlankSignatory,
            "PRECIOUS JANE A. MIRANDA",
            "ABIGAIL JOYNE G. PADUADA"
        };

        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                if (_selectedTabIndex == value)
                    return;
                _selectedTabIndex = value;
                OnPropertyChanged("SelectedTabIndex");
            }
        }

        public ICommand NewGatepassCommand { get; }

        public ICommand OpenTransmittalCommand { get; }

        public ICommand OpenRequisitionCommand { get; }

        public ICommand PrintTransmittalCommand { get; }

        public ICommand PrintGatepassCommand { get; }

        public ICommand PrintRequisitionCommand { get; }

        public ICommand SaveTransmittalPdfCommand { get; }

        public ICommand SaveRequisitionPdfCommand { get; }

        public ICommand AddTransmittalItemCommand { get; }

        public ICommand DeleteTransmittalItemsCommand { get; }

        public ICommand AddGatepassItemCommand { get; }

        public ICommand DeleteGatepassItemsCommand { get; }

        public ICommand AddReqItemCommand { get; }

        public ICommand DeleteReqItemsCommand { get; }

        private static void RemoveSelected<T>(object param, ObservableCollection<T> source) where T : class
        {
            var selected = param as IList;
            if (selected == null || source == null) return;
            var doomed = new List<T>();
            foreach (var o in selected)
            {
                var item = o as T;
                if (item != null) doomed.Add(item);
            }
            foreach (var item in doomed) source.Remove(item);
        }

        private void PrintTransmittal()
        {
            var vm = BuildTransmittalViewModel();
            if (vm == null) return;
            var printView = new TransmittalPrintView { DataContext = vm };
            // The reference form (transmittal.xlsx) is a Legal sheet carrying
            // both copies, so Legal is preselected in the preview.
            ShowPrintPreview("Print Transmittal Form",
                "Review the transmittal form below before printing.",
                printView,
                d => printView.Print(null, d.SelectedPageSize, d.SelectedCopies),
                "Legal");
        }

        private void PrintGatepass()
        {
            var vm = BuildGatepassViewModel();
            if (vm == null) return;
            var preview = BuildGatepassPreviewVisual(vm);
            bool printed = ShowPrintPreview("Print Gatepass Form", "Review the gatepass form below before printing.", preview, d =>
            {
                var printView = new GatepassPrintView { DataContext = vm };
                printView.Print(null);
            }, "Legal");
            if (printed)
                RecentGatepass.Add(GatepassInput);
        }

        // Preview visual matches exactly what Print() will spool: the
        // GatepassPrintView builds its own page model (default = one Legal sheet
        // with two identical copies; print-all = Gatepass+Transmittal on sheet 1
        // and File+data-only on sheet 2), mirroring the mobile reference.
        private FrameworkElement BuildGatepassPreviewVisual(GatepassPrintViewModel vm)
        {
            return new GatepassPrintView { DataContext = vm };
        }

        private GatepassPrintViewModel BuildGatepassViewModel()
        {
            GatepassInput.Items = GatepassEntryItems
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new GatepassDocument.GatepassItem { Description = (l.Description ?? "").Trim() })
                .ToList();
            var vm = new GatepassPrintViewModel(GatepassInput);
            var error = vm.Validate();
            if (error != null)
            {
                MessageBox.Show(error, "Gatepass", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return vm;
        }

        private TransmittalPrintViewModel BuildTransmittalViewModel()
        {
            TransmittalInput.Items = TransmittalEntryItems
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new TransmittalLineItem { Description = (l.Description ?? "").Trim() })
                .ToList();
            var vm = new TransmittalPrintViewModel(TransmittalInput);
            var error = vm.Validate();
            if (error != null)
            {
                MessageBox.Show(error, "Transmittal", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return vm;
        }

        private void SaveTransmittalPdf()
        {
            var vm = BuildTransmittalViewModel();
            if (vm == null) return;
            var printView = new TransmittalPrintView { DataContext = vm };
            // Microsoft Print to PDF prompts for the file location itself.
            printView.Print("Microsoft Print to PDF", "Legal", 1);
        }

        private void PrintRequisition()
        {
            // Standalone print document: every field is optional. Blank cells
            // print blank, exactly like the paper pad — nothing is required.
            BuildRequisitionItems();
            var dialog = new PrintRequisitionDialog(RequisitionInput);
            SetDialogOwner(dialog);
            dialog.ShowDialog();
        }

        // Permanent DEPARTMENT for the standalone E-Docs requisition — this
        // section always requisitions on behalf of IT.
        private const string PermanentRequisitionDepartment = "IT DEPARTMENT";

        private void BuildRequisitionItems()
        {
            RequisitionInput.Items = ReqItems
                .Where(r => r != null &&
                    (!string.IsNullOrWhiteSpace(r.Quantity) ||
                     !string.IsNullOrWhiteSpace(r.Description) ||
                     !string.IsNullOrWhiteSpace(r.Remarks)))
                .Select(r => new RequisitionFormItem
                {
                    Quantity = r.Quantity ?? "",
                    Description = r.Description ?? "",
                    Remarks = r.Remarks ?? ""
                })
                .ToList();
        }

        private void SaveRequisitionPdf()
        {
            BuildRequisitionItems();
            // Microsoft Print to PDF prompts for the file location itself.
            // Legal, matching the paper this section prints on.
            RequisitionFormPrintService.Print(
                RequisitionInput,
                RequisitionPageSize.Legal,
                2,
                "Microsoft Print to PDF");
        }

        // Print-preview dialog shared by the standalone forms, using the same
        // shell design as PrintRequisitionDialog. Returns true when printed.
        // The callback receives the dialog so it can honour the page-size and
        // copies pickers in the header.
        private bool ShowPrintPreview(string title, string subtitle, FrameworkElement printView, Action<EDocsPrintPreviewDialog> onPrint, string defaultPageSize)
        {
            EDocsPrintPreviewDialog dialog = null;
            dialog = new EDocsPrintPreviewDialog(title, subtitle, printView,
                () => { if (onPrint != null) onPrint(dialog); }, defaultPageSize);
            SetDialogOwner(dialog);
            return dialog.ShowDialog() == true;
        }

        private static void SetDialogOwner(Window dialog)
        {
            try
            {
                var activeForm = System.Windows.Forms.Form.ActiveForm;
                if (activeForm != null)
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                    helper.Owner = activeForm.Handle;
                }
            }
            catch
            {
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action _execute;

            public RelayCommand(Action execute)
            {
                _execute = execute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                _execute?.Invoke();
            }
        }

        private sealed class RelayParamCommand : ICommand
        {
            private readonly Action<object> _execute;

            public RelayParamCommand(Action<object> execute)
            {
                _execute = execute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                _execute?.Invoke(parameter);
            }
        }
    }
}
