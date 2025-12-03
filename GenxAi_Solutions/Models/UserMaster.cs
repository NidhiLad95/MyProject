using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GenxAi_Solutions.Models
{
    public class UserMaster
    {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string LastName { get; set; }
            public string MobileNo { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string Address1 { get; set; }
            public string Address2 { get; set; }
            public string Address3 { get; set; }
            public string Pincode { get; set; }
            public string? ProfilePhoto { get; set; }
        public int? GroupId { get; set; }
        public int? CompanyId { get; set; }
        public List<SelectListItem> Group { get; set; } = new();
        public List<SelectListItem> Company { get; set; } = new();
        public string? LoginID { get; set; }

          
        
    }

    public class GetComapany
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; }

        // For dropdown binding
        public List<SelectListItem> UserCompany { get; set; } = new();

    }

    public class GetGroup
    {
        public int GroupID { get; set; }
        public string GroupName { get; set; }

        // For dropdown binding
        public List<SelectListItem> Groups { get; set; } = new();

    }


}
