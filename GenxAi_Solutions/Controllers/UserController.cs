using BAL.Interface;
using GenxAi_Solutions.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static BOL.UserMaster_BOL;

namespace GenxAi_Solutions.Controllers
{
    public class UserController : Controller
    {

       // private readonly ApplicationDbContext _context;
        private readonly IUserMaster_BAL _ObjUserMasterBAL;
        public UserController(//ApplicationDbContext context,
            IUserMaster_BAL ObjUserMasterBAL)
        {
            //_context = context;
            _ObjUserMasterBAL = ObjUserMasterBAL;

        }

        //[Authorize]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

            //[HttpPost]
            //public IActionResult Login(LoginViewModel model)
            //{
            //    if (ModelState.IsValid)
            //    {
            //        var user = _context.UserMaster.FirstOrDefault(u => u.Email == model.Email && u.PasswordHash == model.Password);
            //        if (user != null)
            //        {
            //            // ✅ Success – redirect to dashboard (or home)
            //            return RedirectToAction("Index", "Home");
            //        }

            //        ViewBag.Error = "Invalid email or password.";
            //    }
            //    return View(model);
            //}
            //[HttpPost]
            //public async Task<IActionResult> Login(LoginViewModel model)
            //{
            //string hash = BCrypt.Net.BCrypt.HashPassword("12345");
            //Console.WriteLine(hash);
            //if (ModelState.IsValid)
            //    {
            //        var user = _context.UserMaster.FirstOrDefault(u => u.Email == model.Email);
            //        if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            //        {
            //            // ✅ Create claims
            //            var claims = new List<Claim>
            //        {
            //            new Claim(ClaimTypes.Name, user.Email),
            //            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            //        };

            //            var claimsIdentity = new ClaimsIdentity(
            //                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            //            // ✅ Sign in user with cookie
            //            await HttpContext.SignInAsync(
            //                CookieAuthenticationDefaults.AuthenticationScheme,
            //                new ClaimsPrincipal(claimsIdentity));

            //            return RedirectToAction("Index", "Home");
            //        }

            //        ViewBag.Error = "Invalid email or password.";
            //    }

            //    return View(model);
            //}

            [HttpGet]
            public async Task<IActionResult> Logout()
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "User");
            }


        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }


        //[HttpGet]
        //public IActionResult ChangePassword()
        //{
        //    return View();
        //}

        //[HttpGet]
        //public IActionResult ChangePassword()
        //{
        //    // Initialize model properly to avoid null reference
        //    var model = new ChangePasswordViewModel
        //    {
        //        Id = HttpContext.Session.GetInt32("UserId") ?? 0
        //    };

        //    return View(model);
        //}

        [HttpGet]
        public IActionResult ChangePassword()
        {
            int userId = 0;

            if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int id))
                {
                    userId = id;
                }
            }

            var model = new ChangePasswordViewModel
            {
                Id = userId
            };

            return View(model);
        }


    }
}





