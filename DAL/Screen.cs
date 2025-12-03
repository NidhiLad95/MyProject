
using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DAL
{
    public class Screen : IScreen
    {
        private readonly ICRUDOperations _crudHelper;

        public Screen(ICRUDOperations crudHelper)
        {
            _crudHelper = crudHelper;
        }
        public async Task<Response<string>> Delete(DeleteScreenbyID model)
        {
            return await _crudHelper.Delete<string>("UspScreenDelete", model);
        }

        public async Task<ResponseGetList<ScreenProperty>> Get(ScreenAccess screenaccess)
        {
            return await _crudHelper.GetList<ScreenProperty>("Usp_ScreenMaster_GetAll", screenaccess);
            //return await _crudHelper.GetList<ScreenProperty>("Usp_ScreenMaster_GetAll_Nidhi", screenaccess);

        }

        public async Task<List<ScreenMasterGetAll>> GetScreenAll()
        {
            return await _crudHelper.GetListddl<ScreenMasterGetAll>("GetAllScreen", null);
        }

        public async Task<List<SelectListItem>> GetScreenDDL()
        {
            return await _crudHelper.GetListddl<SelectListItem>("GetParentScreen", null);

        }

        public async Task<Response<ScreenMaster>> InsertScreeenMaster(AddScreenMaster objScreen)
        {

            var data = await _crudHelper.Insert<string>("Sp_InsertScreenMaster", objScreen);
            return new Response<ScreenMaster>
            {
                Status = data.Status,
                Message = data.Message,

               
        Data = new ScreenMaster
                {
                    ParentScreenId = Convert.ToInt32(data.Data),
                    ScreenName = objScreen.ScreenName,
                    ScreenURL = objScreen.ScreenURL,
                    //CreatedBy=objScreen.Createdby
                }
            };
        }

        public async Task<Response<ScreenMaster>> UpdateScreenMaster(UpdateScreenMaster objScreenUpdate)
        {
            var data = await _crudHelper.InsertUpdateDelete<string>("Sp_UpdateScreenMaster", objScreenUpdate);
            return new Response<ScreenMaster>
            {
                Status = data.Status,
                Message = data.Message,

                Data = new ScreenMaster
                {
                    ScreenId = Convert.ToInt32(data.Data),
                    //CreatedBy = objProduct.CreatedBy,
                    ScreenName = objScreenUpdate.ScreenName,
                    ScreenURL = objScreenUpdate.ScreenURL,
                    ModifiedBy = (int)objScreenUpdate.ModifiedBy

                }
            };
        }

        public async Task<Response<string>> DeleteScreenMaster(DeleteScreenMaster DtoDelete)
        {
            return await _crudHelper.InsertUpdateDelete<string>("Sp_DeleteScreenMaster", DtoDelete);

        }

        public async Task<Response<ScreenMaster>> GetByIdScreenMaster(GetByIdScreenMaster dtoGetbyId)
        {
            return await _crudHelper.GetSingleRecord<ScreenMaster>("Usp_GetScreenById", dtoGetbyId);
        }


        
    }
}
