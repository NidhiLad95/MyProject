using BAL.Interface;
using BOL;
using DAL;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserGroup_BOL;

namespace BAL
{
    public class UserGroup_BAL:IUserGroup_BAL
    {

        private readonly IUserGroup _ObjDAL;
        public UserGroup_BAL(IUserGroup ObjDAL)
        {
            _ObjDAL = ObjDAL;
        }

        public async Task<Response<UserGroupMaster>> InsertUserGroup(AddUserGroup objUsergroup)
        {
            return await _ObjDAL.InsertUserGroup(objUsergroup);
        }

        public async Task<List<GetAllUserGroup>> GetAllUserGroup()
        {
            return await _ObjDAL.GetAllUserGroup();
        }


        public async Task<Response<UserGroupMaster>> UpdateUserGroup(UpdateUserGroup objUserGrpUpdate)
        {
            return await _ObjDAL.UpdateUserGroup(objUserGrpUpdate);

        }

        public async Task<Response<string>> DeleteUserGroup(DeleteUserGroup DtoDelete)
        {
            return await _ObjDAL.DeleteUserGroup(DtoDelete);
        }

        public async Task<Response<UserGroupMaster>> GetByIdUserGroup(GetByIdUserGroup dtoGetbyId)
        {
            return await _ObjDAL.GetByIdUserGroup(dtoGetbyId);
        }

        public async Task<List<SelectListItem>> GetUserGroupDDL()
        {
            return await _ObjDAL.GetUserGroupDDL();
        }

        public async Task<List<GetScreenByRole>> GetScreenByRole()
        {
            return await _ObjDAL.GetScreenByRole();
        }

        public async Task<Response<UserRoleMaster>> InsertUserRole(SaveRoleScreensModel objUserRole)
        {
            return await _ObjDAL.InsertUserRole(objUserRole);
        }

        public async Task<List<ScreenroleMaster>> GetRoleScreens(GetByIdUserGroup dtoGetbyIdRoleScreen)
        {
            return await _ObjDAL.GetRoleScreens(dtoGetbyIdRoleScreen);
        }


    }
}
