using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class UserGroup_BOL
    {
        public class UserGroupMaster
        {
            public int GroupId { get; set; }
            public string RoleName { get; set; }
            public string DefaultPage { get; set; }
            public int CreatedBy { get; set; }
            public DateTime CreatedDate { get; set; }
            public int ModifiedBy { get; set; }
        }
       
    }

    public class AddUserGroup
    {
        public int GroupId { get; set; }
       public string? RoleName { get; set; }
        public string? DefaultPage { get; set; }
        public int? Createdby { get; set; }


    }

    public class GetAllUserGroup
    {
       public int GroupId { get; set; }
        public string? RoleName { get; set; }
        public string? DefaultPage { get; set; }
       


    }


    public class GetByIdUserGroup
    {
        public int? GroupId { get; set; }
    }

    public class ScreenroleMaster
    {
        public string ScreenIds { get; set; }
    }

    public class UpdateUserGroup
    {
        public int GroupId { get; set; }
        // public string ParentScreen { get; set; }
        public string? RoleName { get; set; }
        public string? DefaultPage { get; set; }
        public int? ModifiedBy { get; set; }


    }

    public class DeleteUserGroup
    {
        public int GroupId { get; set; }
        public int Modifiedby { get; set; }


    }

    public class GetScreenByRole
    {
        public int ScreenId { get; set; }
        public int ParentScreenid { get; set; }
        public string ParentScreen { get; set; }
        public string ScreenName { get; set; }
        //public string? ScreenURL { get; set; }



    }

    public class UserRoleMaster
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string ScreenId { get; set; }
        public int ParentId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int ModifiedBy { get; set; }
    }

    public class SaveRoleScreensModel_Bulk
    {
        public int GroupId { get; set; }
        public int ScreenId { get; set; }
        public int ParentId { get; set; }
        public int CreatedBy { get; set; }
        
    }

    public class SaveRoleScreensModel
    {
        public int GroupId { get; set; }
        public List<ScreenSelection> Screens { get; set; } = new List<ScreenSelection>();
        //public int ParentId { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class ScreenSelection
    {
        public int ScreenId { get; set; }
        public int ParentId { get; set; }
    }
}
