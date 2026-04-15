using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    [ExcludeFromCodeCoverage]
    public class TreeItem
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public int? ParentId { get; set; }
        public bool HasChildren { get; set; }
    }
}