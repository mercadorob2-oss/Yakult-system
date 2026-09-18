namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    public class SetItemRowDto
    {
        public int    ReqId        { get; set; }
        public string Status       { get; set; }
        public string Description  { get; set; }
        public int    Qty          { get; set; }
        public string ItemName     { get; set; }
        public string ModelNumber  { get; set; }
        public string Category     { get; set; }
        public string SerialNumber { get; set; }
        public string Employee     { get; set; }
        public string Remarks      { get; set; }
    }
}
