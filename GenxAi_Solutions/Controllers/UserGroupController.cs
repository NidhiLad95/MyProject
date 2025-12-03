using BAL.Interface;
using GenxAi_Solutions.Models;
using Microsoft.AspNetCore.Mvc;

namespace GenxAi_Solutions.Controllers
{
    public class UserGroupController : Controller
    {
        private readonly IUserGroup_BAL _balObj;
        public UserGroupController(IUserGroup_BAL balObj)
        {
            _balObj = balObj;
        }
        public IActionResult Index()
        {
            return View();
        }

        

        [HttpGet]
        public IActionResult Add()
        {
            //ViewBag.Screens = GetScreens();
            return View(new UserGroup());
        }


        [HttpGet]
        public async Task<IActionResult> UserAuthorization()
        {
            var model = new UserAuthorization();
            model.UserGroups =await _balObj.GetUserGroupDDL();
            return View(model);
        }
        //[HttpPost]
        //public IActionResult Add(UserGroup model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        model.GroupID = _groups.Count + 1;
        //        model.IsActive = true;
        //        _groups.Add(model);
        //        return RedirectToAction("Index");
        //    }
        //    //ViewBag.Screens = GetScreens();
        //    return View(model);
        //}



    }
}
   


    
        

        
