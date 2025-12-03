using BAL.Interface;
using BOL;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserMaster_BOL;

namespace BAL
{
    public class UserMaster_BAL:IUserMaster_BAL
    {
        private readonly IUserMaster _ObjDAL;
        public UserMaster_BAL(IUserMaster ObjDAL)
        {
            _ObjDAL = ObjDAL;
        }


        public async Task<List<GetAllUserMaster>> GetAllUserMaster()
        {
            return await _ObjDAL.GetAllUserMaster();
        }

        public async Task<Response<UserMaster>> AddUserMaster(AddUserMaster objUserMaster)
        {
            return await _ObjDAL.AddUserMaster(objUserMaster);

        }

        public async Task<Response<UserMaster>> UpdateUserMaster(UpdateUserMaster objusermasterUpdate)
        {
            return await _ObjDAL.UpdateUserMaster(objusermasterUpdate);

        }

        public async Task<Response<GetUserDetailsByIdForChangePassword>> GetUserDetailsforChangePassword(GetByIdUserMaster model)
        {
            return await _ObjDAL.GetUserDetailsforChangePassword(model);

        }

        public async Task<Response<int>> UpdatePassword(UpdateUserPassword model)
        {
            return await _ObjDAL.UpdatePassword(model);

        }

        public async Task<Response<UserDetailLogin>> GetUserDetails(GetUserDetailLogin model)
        {
            return await _ObjDAL.GetUserDetails(model);

        }

        public async Task<Response<string>> DeleteUserMaster(DeleteUserMaster DtoDelete)
        {
            return await _ObjDAL.DeleteUserMaster(DtoDelete);
        }

        public async Task<Response<UserMaster>> GetByIdUserMaster(GetByIdUserMaster DtoGetbyId)
        {
            return await _ObjDAL.GetByIdUserMaster(DtoGetbyId);
        }

        public async Task<List<SelectListItem>> GetCompanyIdDDL()
        {
            return await _ObjDAL.GetCompanyIdDDL();
        }

        public async Task<List<SelectListItem>> GetGroupIdDDL()
        {
            return await _ObjDAL.GetGroupIdDDL();
        }


    }
}
