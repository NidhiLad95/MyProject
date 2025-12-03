using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace BOL
{
    public class ScreenMaster
    {
        public int ScreenId { get; set; }
        public int ParentScreenId { get; set; }
        public string ScreenName { get; set; }
        public string? ScreenURL { get; set; }
       // public int? CreatedBy { get; set; }
       // public DateTime CreatedDate { get; set; }
        public int ModifiedBy { get; set; }

        //For dropdown binding
        public List<SelectListItem> ParentScreens { get; set; } = new();


    }


    public class ScreenMasterGetAll
    {
        public int ScreenId { get; set; }
        public int ParentId { get; set; }
        public string ParentScreen { get; set; }
        public string ScreenName { get; set; }
        public string? ScreenURL { get; set; }
        


    }

    public class AddScreenMaster
    {
        public int ParentScreenId { get; set; }
       // public string ParentScreen { get; set; }
        public string? ScreenName { get; set; }
        public string? ScreenURL { get; set; }
        public int? Createdby { get; set; }


    }

    public class GetByIdScreenMaster
    {
        public int? ScreenId { get; set; }
    }

    public class UpdateScreenMaster
    {
        public int ScreenId { get; set; }
        // public string ParentScreen { get; set; }
        public string? ScreenName { get; set; }
        public string? ScreenURL { get; set; }
        public int? ModifiedBy { get; set; }


    }

    public class DeleteScreenMaster
    {
        public int ScreenId { get; set; }
        public int Modifiedby { get; set; }
       

    }

}
