using BOL;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserMaster_BOL;

namespace BAL.Interface
{
    public interface IUserMaster_BAL
    {
        Task<Response<UserMaster>> AddUserMaster(AddUserMaster objUserMaster);
        Task<List<GetAllUserMaster>> GetAllUserMaster();
        Task<Response<UserDetailLogin>> GetUserDetails(GetUserDetailLogin model);
        Task<Response<GetUserDetailsByIdForChangePassword>> GetUserDetailsforChangePassword(GetByIdUserMaster model);
        Task<Response<UserMaster>> UpdateUserMaster(UpdateUserMaster userMaster);
        Task<Response<int>> UpdatePassword(UpdateUserPassword updatePasswrd);
        Task<Response<string>> DeleteUserMaster(DeleteUserMaster DtoDelete);
        Task<Response<UserMaster>> GetByIdUserMaster(GetByIdUserMaster DtoGetbyId);

        Task<List<SelectListItem>> GetCompanyIdDDL();
        Task<List<SelectListItem>> GetGroupIdDDL();



    }
}
