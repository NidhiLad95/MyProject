using Microsoft.AspNetCore.Mvc.Rendering;

namespace GenxAi_Solutions_V1.Models
{
    public class ScreenViewModel
    {
        public int ScreenId { get; set; }
        public int ParentScreenId { get; set; }
        public string? ScreenName { get; set; }
        public string? ScreenURL { get; set; }

        // For dropdown binding
        public List<SelectListItem> ParentScreens { get; set; } = new();

    }
}
