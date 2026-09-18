using System;
using System.Collections.ObjectModel;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.InvoicePreparation.ViewModels
{
    /// <summary>Business logic for the read-only Preview Contents dialog — shows one
    /// Sub-Type Group's header and its member items. No editing here; membership and
    /// category are set from the Items Page.</summary>
    public sealed class InvoicePreparationDetailViewModel : ViewModelBase
    {
        private readonly InvoicePreparationRepository _repository = new InvoicePreparationRepository();
        private readonly int _preparationId;

        public ObservableCollection<InvoicePreparationItemRow> Items { get; } = new ObservableCollection<InvoicePreparationItemRow>();

        public event Action<string, string> RequestError;

        public string SubType { get; private set; }
        public string ReferenceCode { get; private set; }
        public DateTime? BeginDate { get; private set; }
        public DateTime? EndDate { get; private set; }
        public string Status { get; private set; }

        public InvoicePreparationDetailViewModel(int preparationId)
        {
            _preparationId = preparationId;
        }

        public void Load()
        {
            try
            {
                var group = _repository.GetGroupById(_preparationId);
                if (group == null)
                {
                    RequestError?.Invoke("Error", "This Sub-Type Group could not be found.");
                    return;
                }

                SubType = group.SubType;
                ReferenceCode = group.ReferenceCode;
                BeginDate = group.BeginDate;
                EndDate = group.EndDate;
                Status = group.Status;
                OnPropertyChanged(nameof(SubType));
                OnPropertyChanged(nameof(ReferenceCode));
                OnPropertyChanged(nameof(BeginDate));
                OnPropertyChanged(nameof(EndDate));
                OnPropertyChanged(nameof(Status));

                Items.Clear();
                foreach (var dto in _repository.GetGroupItems(_preparationId))
                {
                    Items.Add(new InvoicePreparationItemRow
                    {
                        PreparationItemId = dto.PreparationItemId,
                        ItemId = dto.ItemId,
                        ItemName = dto.ItemName,
                        Description = dto.Description,
                        UnitOfMeasure = dto.UnitOfMeasure,
                        Quantity = dto.Quantity,
                        UnitPrice = dto.UnitPrice,
                        Remarks = dto.Remarks,
                        ModelNumber = dto.ModelNumber,
                        SerialNumber = dto.SerialNumber,
                        ItemType = dto.ItemType,
                        Category = dto.Category,
                        WarrantyStartDate = dto.WarrantyStartDate,
                        WarrantyEndDate = dto.WarrantyEndDate
                    });
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load Sub-Type Group: {ex.Message}");
            }
        }
    }
}
