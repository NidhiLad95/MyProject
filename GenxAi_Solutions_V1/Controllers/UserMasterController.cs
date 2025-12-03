using BAL.Interface;
using GenxAi_Solutions_V1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;

public class UserMasterController : Controller
{
    private readonly IUserMaster_BAL _balObj;
    public UserMasterController(IUserMaster_BAL balObj)
    {
        _balObj = balObj;
    }
    public async  Task<IActionResult> Add()
    {
        var obj = new UserMaster();
        obj.Group = await _balObj.GetGroupIdDDL();
        obj.Company = await _balObj.GetCompanyIdDDL();

        return View(obj);
    }

    //[HttpGet]
    //public async Task<IActionResult> GetGroup()
    //{
    //    var model = new GetGroup();
    //    model.Groups = await _balObj.GetGroupIdDDL();
    //    return View(model);
    //}

    //[HttpGet]
    //public async Task<IActionResult> GetComapny()
    //{
    //    var model = new GetComapany();
    //    model.UserCompany = await _balObj.GetCompanyIdDDL();
    //    return View(model);
    //}








}
