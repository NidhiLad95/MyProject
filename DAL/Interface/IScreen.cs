
using BOL;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DAL.Interface
{
    public interface IScreen
    {
        Task<ResponseGetList<ScreenProperty>> Get(ScreenAccess screenaccess);
       Task<Response<string>> Delete(DeleteScreenbyID model);
        Task<List<SelectListItem>> GetScreenDDL();
        Task<List<ScreenMasterGetAll>> GetScreenAll();
        Task<Response<ScreenMaster>> InsertScreeenMaster(AddScreenMaster objScreen);
        Task<Response<ScreenMaster>> UpdateScreenMaster(UpdateScreenMaster screenMaster);
        Task<Response<string>> DeleteScreenMaster(DeleteScreenMaster DtoDelete);
        Task<Response<ScreenMaster>> GetByIdScreenMaster(GetByIdScreenMaster DtoGetbyId);
    }
}
