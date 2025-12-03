using BAL.Interface;
using BOL;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BAL
{
    public class ScreenBAL : IScreen_BAL
    {

        private readonly IScreen _ObjDAL;
        public ScreenBAL(IScreen ObjDAL)
        {
            _ObjDAL = ObjDAL;
        }


        public async Task<List<MenuBind>> Get(ScreenAccess screenacccess)
        {
            List<MenuBind> lstMenu = new List<MenuBind>();
            var res = await _ObjDAL.Get(screenacccess);
            foreach (var item in res.Data.Where(x => x.ParentScreenid == 0).ToList())
            {
                var child = res.Data.Where(x => x.ParentScreenid == item.ScreenId).ToList();

                lstMenu.Add(new MenuBind
                {
                    ParentMenu = item,
                    ChildMenus = child
                });
            }

            return lstMenu;
        }


        public async Task<List<ScreenMasterGetAll>> GetScreenAll()
        {
            return await _ObjDAL.GetScreenAll();
        }

        public async Task<List<SelectListItem>> GetScreenDDL()
        {
            return await _ObjDAL.GetScreenDDL();
        }

        public async Task<Response<ScreenMaster>> InsertScreeenMaster(AddScreenMaster objScreen)
        {
            return await _ObjDAL.InsertScreeenMaster(objScreen);
            
        }

        public async Task<Response<ScreenMaster>> UpdateScreenMaster(UpdateScreenMaster objScreenUpdate)
        {
            return await _ObjDAL.UpdateScreenMaster(objScreenUpdate);

        }

        public async Task<Response<string>> DeleteScreenMaster(DeleteScreenMaster DtoDelete)
        {
            return await _ObjDAL.DeleteScreenMaster(DtoDelete);
        }

        public async Task<Response<ScreenMaster>> GetByIdScreenMaster(GetByIdScreenMaster dtoGetbyId)
        {
            return await _ObjDAL.GetByIdScreenMaster(dtoGetbyId);
        }

       
    }
}

             

        
            
