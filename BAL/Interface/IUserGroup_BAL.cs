using BOL;
using DAL;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserGroup_BOL;

namespace BAL.Interface
{
    public interface IUserGroup_BAL
    {
        Task<Response<UserGroupMaster>> InsertUserGroup(AddUserGroup ObjUsergrp);
        Task<List<GetAllUserGroup>> GetAllUserGroup();

        Task<Response<UserGroupMaster>> UpdateUserGroup(UpdateUserGroup objUsergrpUpdate);
        Task<Response<string>> DeleteUserGroup(DeleteUserGroup DtoDelete);
        Task<Response<UserGroupMaster>> GetByIdUserGroup(GetByIdUserGroup DtoGetbyId);
        Task<List<SelectListItem>> GetUserGroupDDL();
        Task<List<GetScreenByRole>> GetScreenByRole();
        Task<Response<UserRoleMaster>> InsertUserRole(SaveRoleScreensModel ObjUserRole);
        Task<List<ScreenroleMaster>> GetRoleScreens(GetByIdUserGroup DtoGetbyIdScreen);


    }
}
