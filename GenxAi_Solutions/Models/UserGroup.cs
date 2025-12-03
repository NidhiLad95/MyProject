using Microsoft.AspNetCore.Mvc.Rendering;

namespace GenxAi_Solutions.Models
{
    public class UserGroup
    {

        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public string? DefaultPage { get; set; }
        
    }

    public class UserAuthorization
    {
        public int GroupID { get; set; }
        public string GroupName { get; set; }

        // For dropdown binding
        public List<SelectListItem> UserGroups { get; set; } = new();

    }

}
