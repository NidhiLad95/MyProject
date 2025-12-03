using BOL;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BAL.Interface
{
    public interface IScreen_BAL
    {
        Task<List<MenuBind>> Get(ScreenAccess screenaccess);
        Task<Response<ScreenMaster>> InsertScreeenMaster(AddScreenMaster objScreen);
        Task<List<ScreenMasterGetAll>> GetScreenAll();
        Task<List<SelectListItem>> GetScreenDDL();
        Task<Response<ScreenMaster>> UpdateScreenMaster(UpdateScreenMaster screenMaster);
        Task<Response<string>> DeleteScreenMaster(DeleteScreenMaster DtoDelete);
        Task<Response<ScreenMaster>> GetByIdScreenMaster(GetByIdScreenMaster DtoGetbyId);

    }
}
