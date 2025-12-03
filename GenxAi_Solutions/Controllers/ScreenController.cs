using BAL.Interface;
using GenxAi_Solutions.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;

public class ScreenController : Controller
{
    private readonly IScreen_BAL _balObj;
    public ScreenController(IScreen_BAL balObj)
    { 
           _balObj = balObj;
    }
    // GET: /Screen/Add
    public async Task<IActionResult> Add()
    {
        var res = await _balObj.GetScreenDDL();

        var model = new ScreenViewModel();
        model.ParentScreens = await _balObj.GetScreenDDL();
        //var model = new ScreenViewModel
        //{

        //    ParentScreens = (IEnumerable<SelectListItem>)_balObj.GetScreenDDL()
        //};
        return View(model);
    }

    //[HttpPost]
    //public async Task<IActionResult> Add(ScreenViewModel model)
    //{
    //    if (ModelState.IsValid)
    //    {
    //        // Save to DB (EF Core / Stored Procedure)
    //        // Example:
    //        // _context.Screens.Add(new Screen { ... });
    //        // _context.SaveChanges();

    //        TempData["Message"] = "Screen saved successfully!";
    //        return RedirectToAction("Add");
    //    }

    //    // Reload dropdowns if validation fails

    //    //model.ParentScreens = await _balObj.GetScreenDDL();
    //    return View(model);
    //}

    
}
