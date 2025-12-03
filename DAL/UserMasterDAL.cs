using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserMaster_BOL;

namespace DAL
{
    public class UserMasterDAL:IUserMaster
    {
        private readonly ICRUDOperations _crudHelper;
        public UserMasterDAL(ICRUDOperations crudHelper)
        {
            _crudHelper = crudHelper;
        }

        public async Task<Response<UserMaster>> AddUserMaster(AddUserMaster objUserMaster)
        {
            var data = await _crudHelper.Insert<string>("Sp_InsertUserMaster", objUserMaster);
            return new Response<UserMaster>
            {
                Status = data.Status,
                Message = data.Message,


                Data = new UserMaster
                {
                    //Id = Convert.ToInt32(data.Data),
                    FirstName = objUserMaster.FirstName,
                    MiddleName = objUserMaster.MiddleName,
                    LastName = objUserMaster.LastName,
                    Email = objUserMaster.Email,
                    PasswordHash = objUserMaster.Password,
                    Address1 = objUserMaster.Address1,
                    Address2 = objUserMaster.Address2,
                    Address3 = objUserMaster.Address3,
                    Pincode = objUserMaster.Pincode,
                    ProfilePhotoPath = objUserMaster.ProfilePhotoPath,
                    GroupId = objUserMaster.GroupId,
                    CompanyId = objUserMaster.CompanyId,
                    MobileNo = objUserMaster.MobileNo,
                    LoginID = objUserMaster.LoginID,
                   CreatedBy= objUserMaster.CreatedBy
                }
            };
        }
      
        public async Task<Response<UserMaster>> UpdateUserMaster(UpdateUserMaster userMaster)
        {
            var data = await _crudHelper.InsertUpdateDelete<string>("Sp_UpdateUserMaster", userMaster);
            return new Response<UserMaster>
            {
                Status = data.Status,
                Message = data.Message,

                Data = new UserMaster
                {
                    Id = Convert.ToInt32(data.Data),
                    FirstName = userMaster.FirstName,
                    MiddleName = userMaster.MiddleName,
                    LastName = userMaster.LastName,
                    Email = userMaster.Email,
                    //PasswordHash = userMaster.Password,
                    Address1 = userMaster.Address1,
                    Address2 = userMaster.Address2,
                    Address3 = userMaster.Address3,
                    Pincode = userMaster.Pincode,
                    ProfilePhotoPath = userMaster.ProfilePhotoPath,
                    GroupId = userMaster.GroupId,
                    CompanyId = userMaster.CompanyId,
                    MobileNo = userMaster.MobileNo,
                    LoginID = userMaster.LoginID,
                    ModifiedBy = (int)userMaster.ModifiedBy

                }
            };
        }

        public async Task<Response<string>> DeleteUserMaster(DeleteUserMaster DtoDelete)
        {
            return await _crudHelper.InsertUpdateDelete<string>("Sp_DeleteUserMaster", DtoDelete);

        }

        public async Task<Response<GetUserDetailsByIdForChangePassword>> GetUserDetailsforChangePassword(GetByIdUserMaster model)
        {
            return await _crudHelper.GetSingleRecord<GetUserDetailsByIdForChangePassword>("USP_GetUserDetailsById", model);
        }


        public async Task<Response<int>> UpdatePassword(UpdateUserPassword updatepasswrd)
        {
            return await _crudHelper.InsertUpdateDelete<int>("USP_UpdateUserPassword", updatepasswrd);
            //var data = await _crudHelper.InsertUpdateDelete<int>("USP_UpdateUserPassword", updatepasswrd);


        }
        public async Task<Response<UserDetailLogin>> GetUserDetails(GetUserDetailLogin model)
        {
            return await _crudHelper.GetSingleRecord<UserDetailLogin>("GetUserLoginDetails", model);
        }

        public async Task<List<GetAllUserMaster>> GetAllUserMaster()
        {
            return await _crudHelper.GetListddl<GetAllUserMaster>("GetAllUserMaster", null);
        }

        public async Task<Response<UserMaster>> GetByIdUserMaster(GetByIdUserMaster dtoGetbyId)
        {
            return await _crudHelper.GetSingleRecord<UserMaster>("GetUserMasterById", dtoGetbyId);
        }

        public async Task<List<SelectListItem>> GetCompanyIdDDL()
        {
            return await _crudHelper.GetListddl<SelectListItem>("GetCompany", null);

        }

        public async Task<List<SelectListItem>> GetGroupIdDDL()
        {
            return await _crudHelper.GetListddl<SelectListItem>("GetUserGroupDdl", null);

        }


    }


}
