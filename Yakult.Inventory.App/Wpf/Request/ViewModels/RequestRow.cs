using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Request.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around Pages.RequestDto for the Requests grid.
    /// Wraps (rather than reuses) RequestDto directly so the checkbox column can raise
    /// PropertyChanged on Selected — required for the "select all" toggle to visually
    /// update already-rendered rows, since RequestDto itself is a plain POCO.
    /// </summary>
    public sealed class RequestRow : ViewModelBase
    {
        public RequestDto Dto { get; }

        public RequestRow(RequestDto dto)
        {
            Dto = dto;
            _selected = dto.Selected;
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set
            {
                if (SetField(ref _selected, value))
                    Dto.Selected = value;
            }
        }

        public int ReqId => Dto.ReqId;
        public string EmployeeName => Dto.EmployeeName;
        public string EmployeeNumber => Dto.EmployeeNumber;
        public string CompanyName => Dto.CompanyName;
        public string DepartmentName => Dto.DepartmentName;
        public string BranchName => Dto.BranchName;
        public string ItemName => Dto.ItemName;
        public string ModelNumber => Dto.ModelNumber;
        public string Category => Dto.Category;
        public string FixedAssetDisplay => Dto.IsTrackedAsset ? "Yes" : "No";
        public string SerialNumber => Dto.SerialNumber;
        public int Quantity => Dto.Quantity;
        public string Status => Dto.Status;
        public System.DateTime? DateRequested => Dto.DateRequested;
        public string CreatedByName => Dto.CreatedByName;
        public string ModifiedByName => Dto.ModifiedByName;
    }
}
