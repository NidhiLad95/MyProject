using BOL;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserMaster_BOL;

namespace DAL.Interface
{
    public interface IUserMaster
    {
        Task<Response<UserMaster>> AddUserMaster(AddUserMaster objUserMaster);
        Task<List<GetAllUserMaster>> GetAllUserMaster();

        Task<Response<UserMaster>> UpdateUserMaster(UpdateUserMaster userMaster);
        Task<Response<GetUserDetailsByIdForChangePassword>> GetUserDetailsforChangePassword(GetByIdUserMaster model);
        Task<Response<int>> UpdatePassword(UpdateUserPassword updatePasswrd);
        Task<Response<UserDetailLogin>> GetUserDetails(GetUserDetailLogin model);
        Task<Response<string>> DeleteUserMaster(DeleteUserMaster DtoDelete);
        Task<Response<UserMaster>> GetByIdUserMaster(GetByIdUserMaster DtoGetbyId);

        Task<List<SelectListItem>> GetCompanyIdDDL();
        Task<List<SelectListItem>> GetGroupIdDDL();
    }
}
