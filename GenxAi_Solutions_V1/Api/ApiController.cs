using Azure.Core;
using BAL;
using BAL.Interface;
using BOL;
using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using static BOL.UserMaster_BOL;

namespace GenxAi_Solutions_V1.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class ApiController : ControllerBase
    {
       
        private readonly IScreen_BAL _ObjBAL;
        private readonly IUserGroup_BAL _ObjUsergrpBAL;
        private readonly IUserMaster_BAL _ObjUserMasterBAL;
        private readonly ICompanyProfileBAL _combal;
        private readonly ILogger<ApiController> _logger;
        private readonly IAuditLogger _auditLogger;
        private readonly IJwtTokenService _jwt;

        public ApiController(
            
            IScreen_BAL ObjBAL, IUserGroup_BAL ObjUsergrpBAL, ICompanyProfileBAL combal,
            IUserMaster_BAL ObjUserMasterBAL,
            ILogger<ApiController> logger,
            IAuditLogger auditLogger,
            IJwtTokenService jwt)
        {
            
            _ObjBAL = ObjBAL;
            _ObjUsergrpBAL = ObjUsergrpBAL;
            _combal = combal;
            _ObjUserMasterBAL = ObjUserMasterBAL;
            _logger = logger;
            _auditLogger = auditLogger;
            _jwt = jwt;
        }
        

        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            var mySalt = BCrypt.Net.BCrypt.GenerateSalt();
            passwordSalt = Encoding.ASCII.GetBytes(mySalt);
            passwordHash = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(password, mySalt));
        }

        

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginViewModel model)

        {
            try
            {
                
                if (!ModelState.IsValid)

                    return BadRequest(new { status = false, message = "Invalid request." });

                var user = _ObjUserMasterBAL.GetUserDetails(new GetUserDetailLogin { Email = model.Email });
                var userData = user.Result.Data;
                var gid = 0;
                if (userData != null && BCrypt.Net.BCrypt.Verify(model.Password, userData?.PasswordHash))
                {


                    //  Create claims
                   
                    if(userData!=null)
                    {
                        gid = userData.GroupId;
                    }

                    var userModel = new User
                    {
                        Id = userData.Id,
                        Email = userData.Email
                    };

                    var claims = new List<Claim>

                        {


                            new Claim(ClaimTypes.Name, userData?.Email),

                            new Claim(ClaimTypes.NameIdentifier, userData.Id.ToString()),
                             new Claim("CompanyId", ((userData!=null && (userData.CompanyId>0))?userData.CompanyId.ToString():"")),
                             new Claim("companies", ((userData!=null && (!string.IsNullOrEmpty(userData.CompanyIDs)))?userData.CompanyIDs.ToString():"0")),
                             new Claim("DatabaseName", ((userData!=null && (!string.IsNullOrEmpty(userData.DatabaseName)))?userData.DatabaseName.ToString():"")),
                             new Claim("SQLitedbName", ((userData!=null && (!string.IsNullOrEmpty(userData.SQLitedbName)))?userData.SQLitedbName.ToString():"")),
                             new Claim("SQLitedbName_File",((userData!=null && (!string.IsNullOrEmpty(userData.SQLitedbName_File)))?userData.SQLitedbName_File.ToString():"")), 
                             new Claim("Connstr", ((userData!=null && (!string.IsNullOrEmpty(userData.ConnectionString)))?userData.ConnectionString.ToString():"")),
                             new Claim("dbType", ((userData!=null && (!string.IsNullOrEmpty(userData.DatabaseType)))?userData.DatabaseType.ToString():"")),
                             new Claim("groupId", gid.ToString())
                        

                        };

                    var claimsIdentity = new ClaimsIdentity(

                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    // Sign in user with cookie

                    HttpContext.SignInAsync(

                        CookieAuthenticationDefaults.AuthenticationScheme,

                        new ClaimsPrincipal(claimsIdentity));



                    //HttpContext.Session.SetInt32("UserId", userData.Id);

                    //HttpContext.Session.SetString("Email", userData.Email);

                    //HttpContext.Session.SetString("groupId", gid.ToString());
                    var (accessToken, expiresUtc) = _jwt.GenerateAccessToken(userModel, claims);
                    var refreshToken = _jwt.GenerateRefreshToken(userModel);

                    return Ok(new {
                        status = true, message = "Login successful", userId = userData.Id, email = userData.Email, groupId = gid,
                    
                        accessToken = accessToken,
                        expiresAtUtc = expiresUtc,
                        refreshToken = refreshToken
                    });

                }

                return Unauthorized(new { status = false, message = "Invalid email or password" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = false, message = ex.Message });
            }
            

        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.RefreshToken))
                return BadRequest(new { message = "Refresh token required" });

            var principal = _jwt.ValidateRefreshToken(req.RefreshToken);
            if (principal == null) return Unauthorized(new { message = "Invalid refresh token" });

            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            // Get the user so we can include email in the new access token
            var byId = await _ObjUserMasterBAL.GetByIdUserMaster(new GetByIdUserMaster { Id = userId });
            var data = byId?.Data;
            if (data == null) return Unauthorized();

            var userModel = new GenxAi_Solutions_V1.Models.User { Id = data.Id, Email = data.Email };

            var (accessToken, expiresUtc) = _jwt.GenerateAccessToken(userModel);
            var newRefresh = _jwt.GenerateRefreshToken(userModel); // rotate

            return Ok(new
            {
                accessToken = accessToken,
                expiresAtUtc = expiresUtc,
                refreshToken = newRefresh
            });
        }

        //[Authorize] // logout requires the user to be signed in (cookie or bearer)
        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Logout()
        {
            //// 1) If a cookie exists, this clears it and triggers your Cookie OnSigningOut event
            //await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            //// 2) For JWT: nothing to do server-side (stateless). Client must drop tokens.
            //return Ok(new { message = "Logged out" });

            var svc = HttpContext.RequestServices.GetRequiredService<AuthEventsService>();
            await svc.OnSigningOut(HttpContext, User);
            return Ok(new { message = "Logged out" });
        }

        [HttpPost("ChangePassword")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult ChangePassword([FromBody]ChangePasswordViewModel model)
        {
            int Uid = 0;
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    Uid = userId;
                }
                else
                {
                    Uid = 0;
                }
            }
            else
            {
                Uid = 0;
            }
            if (!ModelState.IsValid)
                return BadRequest("Invalid request.");

            try
            {
                // var userId = HttpContext.Session.GetInt32("UserId");
                var userId = Uid;
                if (userId == null)
                {
                    return RedirectToAction("Login", "User");
                }

                //var user = _ObjUserMasterBAL.GetUserDetailsforChangePassword((int)userId);
                var user = _ObjUserMasterBAL.GetUserDetailsforChangePassword(new GetByIdUserMaster { Id = userId });
                var userData = user.Result.Data;
                if (userData == null || !BCrypt.Net.BCrypt.Verify(model.OldPassword, userData.PasswordHash))
                {
                    //ModelState.AddModelError("", "Old password is incorrect.");
                    return BadRequest("Old password is incorrect.");
                }

                var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

                //_ObjUserMasterBAL.UpdatePassword( new UpdateUserPassword { UserId=model.Id, NewPasswordHash=model.NewPassword, ModifiedBy=Uid} );
                _ObjUserMasterBAL.UpdatePassword(new UpdateUserPassword
                {
                    UserId = Uid,
                    NewPasswordHash = newHash,  // Use the hashed password, not plain text
                    ModifiedBy = Uid
                });

                return Ok(new { message = "Password changed successfully!", UserId = model.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
                return BadRequest(ex.Message);
            }
        }



        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request.");

            var email = (model.Email ?? "").Trim();
            //var user = await _context.UserMaster.FirstOrDefaultAsync(u => u.Email == email);
            var user = await _ObjUserMasterBAL.GetUserDetails(new GetUserDetailLogin { Email = model.Email });

            // Always return the same response to prevent email enumeration
            var genericOk = Ok(new { message = "If the email exists, a reset link has been sent." });

            if (user == null)
                return genericOk;

            // Generate a cryptographically secure token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = WebEncoders.Base64UrlEncode(tokenBytes);

            //// Hash token before storing
            //var tokenHash = ToSha256(token);
            //user.ResetToken = tokenHash;
            //user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1); // validity window

            //await _context.SaveChangesAsync();

            // Build reset link (adjust host to your domain)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            //var resetLink = $"{baseUrl}/User/ResetPassword?email={UrlEncoder.Default.Encode(user.Email)}&token={UrlEncoder.Default.Encode(token)}";

            // Send email
        //    var body = $@"
        //<p>We received a request to reset your password.</p>
        //<p><a href=""{HtmlEncoder.Default.Encode(resetLink)}"">Click here to reset your password</a></p>
        //<p>This link will expire in 1 hour. If you didn't request this, you can ignore this email.</p>";
        //    try
        //    {
        //        await _emailSender.SendAsync(user.Data.Email, "Reset your password", body);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }


            return genericOk;
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request.");

            var email = (model.Email ?? "").Trim();
            //var user = await _context.UserMaster.FirstOrDefaultAsync(u => u.Email == email);
            var user = await _ObjUserMasterBAL.GetUserDetails(new GetUserDetailLogin { Email=model.Email});
            if (user == null)
                return Unauthorized(new { message = "Invalid token or expired." });

            //if (user.ResetToken == null || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
            //    return Unauthorized(new { message = "Invalid token or expired." });

            //// Compare hashes
            //var incomingHash = ToSha256(model.Token);
            //if (!CryptographicEquals(incomingHash, user.ResetToken))
            //    return Unauthorized(new { message = "Invalid token or expired." });

            // Update password
            user.Data.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // Clear reset fields
            //user.ResetToken = null;
            //user.ResetTokenExpiry = null;

            //await _context.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully. Please sign in." });
        }

        //[HttpPost("getmenu")]
        //[SessionAuthorize]
        //public async Task<IActionResult> GetAllMenu([FromBody] ScreenAccess screenaccess)
        //{
        //    int? userId = HttpContext.Session.GetInt32("UserId");
        //    string? email = HttpContext.Session.GetString("Email");
        //    string? gid= HttpContext.Session.GetString("GroupId");
        //    if (!ModelState.IsValid)
        //        return BadRequest("Invalid request.");
        //    var menus =await  _ObjBAL.Get(screenaccess).ConfigureAwait(false);
        //    return Ok(menus);

        //}

        //[SessionAuthorize]
        [HttpPost("getmenu")]        
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAllMenu([FromBody] ScreenAccess screenaccess)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request.");

            // Get UserId claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.FindFirst(ClaimTypes.Name)?.Value;
            var groupIdClaim = User.FindFirst("GroupId")?.Value; // Custom claim

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(emailClaim))
                return Unauthorized("Missing user claims.");

            // Convert if needed
            int userId = int.Parse(userIdClaim);
            string email = emailClaim;
            string gid = groupIdClaim ?? string.Empty;

            // You can optionally attach these to your ScreenAccess model if required
            // screenaccess.UserId = userId;
            // screenaccess.Email = email;
            // screenaccess.GroupId = gid;

            var menus = await _ObjBAL.Get(screenaccess).ConfigureAwait(false);
            return Ok(menus);
        }




        [HttpPost("InsertScreen")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Create([FromBody] AddScreenMaster addScreenMaster)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    addScreenMaster.Createdby = userId;
                }
                else
                {
                    addScreenMaster.Createdby = 0;
                }
            }
            else
            {
                addScreenMaster.Createdby = 0;
            }

            var response = await _ObjBAL.InsertScreeenMaster(addScreenMaster);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }


        // Get all
        [HttpGet("GetScreens")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetScreens()
        {
            var list = await _ObjBAL.GetScreenAll();
            return Ok(list);
        }


        // Update
        [HttpPut("UpdateScreen")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateScreen([FromBody] UpdateScreenMaster model)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    model.ModifiedBy = userId;
                }
                else
                {
                    model.ModifiedBy = 0;
                }
            }
            else
            {
                model.ModifiedBy = 0;
            }

            var response = await _ObjBAL.UpdateScreenMaster(model);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }


        // Delete
        [HttpDelete("DeleteScreen")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteScreenMaster(DeleteScreenMaster dtodelete)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                dtodelete.Modifiedby = userId;
            }
            else
            {
                dtodelete.Modifiedby = 0;
            }

            var response = await _ObjBAL.DeleteScreenMaster(dtodelete).ConfigureAwait(false);
            return Ok(response);
        }


        [HttpPost("GetById")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetByIdScreenMaster([FromBody] GetByIdScreenMaster GetByIdscreenmaster)
        {
            var result = await _ObjBAL.GetByIdScreenMaster(GetByIdscreenmaster).ConfigureAwait(false);
            return Ok(result);
           
        }




        // Helpers
        private static string ToSha256(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash); // uppercase hex
        }

        // Time-constant comparison
        private static bool CryptographicEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }

     [HttpPost("InsertUserGroup")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> InsertGroup([FromBody] AddUserGroup addUserGroup)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    addUserGroup.Createdby = userId;
                }
                else
                {
                    addUserGroup.Createdby = 0;
                }
            }
            else
            {
                addUserGroup.Createdby = 0;
            }

            var response = await _ObjUsergrpBAL.InsertUserGroup(addUserGroup);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }


        // Get all
        [HttpGet("GetAllUsergrp")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAllUserGrp()
        {
            var list = await _ObjUsergrpBAL.GetAllUserGroup();
            return Ok(list);
        }


        // Update
        [HttpPut("UpdateUserGrp")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateUserGrp([FromBody] UpdateUserGroup model)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    model.ModifiedBy = userId;
                }
                else
                {
                    model.ModifiedBy = 0;
                }
            }
            else
            {
                model.ModifiedBy = 0;
            }

            var response = await _ObjUsergrpBAL.UpdateUserGroup(model);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }


        // Delete
        [HttpDelete("DeleteUsergrp")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteUserGroup(DeleteUserGroup dtodelete)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                dtodelete.Modifiedby = userId;
            }
            else
            {
                dtodelete.Modifiedby = 0;
            }

            var response = await _ObjUsergrpBAL.DeleteUserGroup(dtodelete).ConfigureAwait(false);
            return Ok(response);
        }


        [HttpPost("GetByIdUserGrp")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetByIdUsergrp([FromBody] GetByIdUserGroup GetByIdUsrGrp)
        {
            var result = await _ObjUsergrpBAL.GetByIdUserGroup(GetByIdUsrGrp).ConfigureAwait(false);
            return Ok(result);
        }

        // Get all
        [HttpGet("GetScreensByRole")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetScreensByRole()
        {
            var list = await _ObjUsergrpBAL.GetScreenByRole();
            return Ok(list);
        }

        [HttpPost("SaveRoleScreens")]
        
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SaveRoleScreens([FromBody] SaveRoleScreensModel model)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    model.CreatedBy = userId;
                }
                else
                {
                    model.CreatedBy = 0;
                }
            }
            else
            {
                model.CreatedBy = 0;
            }
       
            var response = await _ObjUsergrpBAL.InsertUserRole(model);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }

        [HttpPost("GetRoleScreens")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetRoleScreens([FromBody] GetByIdUserGroup GetByIdUsrGrpRole)
        {
            var list = await _ObjUsergrpBAL.GetRoleScreens(GetByIdUsrGrpRole).ConfigureAwait(false);
            return Ok(list);
        }




        //[HttpPost("InsertUserMaster")]
        //[SessionAuthorize]
        //public async Task<IActionResult> InsertUserMaster([FromBody] AddUserMaster addUserMaster, IFormFile ProfilePhoto)
        //{
        //    if (User?.Identity?.IsAuthenticated == true)
        //    {
        //        // read int userId from claims
        //        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        //        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        //        {
        //            addUserMaster.CreatedBy = userId;
        //        }
        //        else
        //        {
        //            addUserMaster.CreatedBy = 0;
        //        }
        //    }
        //    else
        //    {
        //        addUserMaster.CreatedBy = 0;
        //    }
        //    // 🔑 Hash password using BCrypt
        //    if (!string.IsNullOrEmpty(addUserMaster.Password))
        //    {
        //        // WorkFactor = 11 (you can adjust cost factor)
        //        addUserMaster.Password = BCrypt.Net.BCrypt.HashPassword(addUserMaster.Password, workFactor: 11);
        //    }

        //    // Save profile photo
        //    if (ProfilePhoto != null && ProfilePhoto.Length > 0)
        //    {
        //        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        //        if (!Directory.Exists(uploadsFolder))
        //        {
        //            Directory.CreateDirectory(uploadsFolder);
        //        }

        //        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ProfilePhoto.FileName);
        //        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        //        using (var stream = new FileStream(filePath, FileMode.Create))
        //        {
        //            await ProfilePhoto.CopyToAsync(stream);
        //        }

        //        // Save relative path (e.g. /uploads/file.jpg)
        //        addUserMaster.ProfilePhoto = "/uploads/" + uniqueFileName;
        //    }

        //    var response = await _ObjUserMasterBAL.AddUserMaster(addUserMaster);

        //    if (response.Status)
        //    {
        //        return StatusCode(StatusCodes.Status201Created, response);
        //    }
        //    else if (!response.Status)
        //    {
        //        return StatusCode(StatusCodes.Status409Conflict, response);
        //    }
        //    else
        //    {
        //        return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
        //    }
        //}


        [HttpPost("InsertUserMaster")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> InsertUserMaster([FromForm] AddUserMaster addUserMaster)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    addUserMaster.CreatedBy = userId;
                }
                else
                {
                    addUserMaster.CreatedBy = 0;
                }
            }
            else
            {
                addUserMaster.CreatedBy = 0;
            }

             //🔑 Hash password using BCrypt
                if (!string.IsNullOrEmpty(addUserMaster.Password))
            {
                //WorkFactor = 11 (you can adjust cost factor)
                addUserMaster.Password = BCrypt.Net.BCrypt.HashPassword(addUserMaster.Password, workFactor: 11);
            }

            if (addUserMaster.ProfilePhoto != null && addUserMaster.ProfilePhoto.Length > 0)
            {
                
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                //var uniqueFileName = Guid.NewGuid().ToString() + "_" + addUserMaster.ProfilePhoto.FileName;
                var uniqueFileName =  addUserMaster.ProfilePhoto.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await addUserMaster.ProfilePhoto.CopyToAsync(stream);
                }

                
                addUserMaster.ProfilePhotoPath = "/uploads/" + uniqueFileName;
            }


            var response = await _ObjUserMasterBAL.AddUserMaster(addUserMaster);

            if (response.Status)
                return StatusCode(StatusCodes.Status201Created, response);
            else if (!response.Status)
                return StatusCode(StatusCodes.Status409Conflict, response);
            else
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
        }




        // Get all
        [HttpGet("GetAllUserMaster")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetUserMaster()
        {
            var list = await _ObjUserMasterBAL.GetAllUserMaster();
            return Ok(list);
        }


        // Update
        [HttpPut("UpdateUserMaster")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateUserMaster([FromForm] UpdateUserMaster model)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    model.ModifiedBy = userId;
                }
                else
                {
                    model.ModifiedBy = 0;
                }
            }
            else
            {
                model.ModifiedBy = 0;
            }

            if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
            {

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                //var uniqueFileName = Guid.NewGuid().ToString() + "_" + addUserMaster.ProfilePhoto.FileName;
                var uniqueFileName = model.ProfilePhoto.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePhoto.CopyToAsync(stream);
                }


                model.ProfilePhotoPath = "/uploads/" + uniqueFileName;
            }

            var response = await _ObjUserMasterBAL.UpdateUserMaster(model);

            if (response.Status)
            {
                return StatusCode(StatusCodes.Status201Created, response);
            }
            else if (!response.Status)
            {
                return StatusCode(StatusCodes.Status409Conflict, response);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Something went wrong, Please contact system administrator");
            }
        }


        // Delete
        [HttpDelete("DeleteUserMaster")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteUserMaster(DeleteUserMaster dtodelete)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                dtodelete.Modifiedby = userId;
            }
            else
            {
                dtodelete.Modifiedby = 0;
            }

            var response = await _ObjUserMasterBAL.DeleteUserMaster(dtodelete).ConfigureAwait(false);
            return Ok(response);
        }


        [HttpPost("GetByIdUserMaster")]
        //[SessionAuthorize]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetByIdUserMaster([FromBody] GetByIdUserMaster GetByIdUsrmaster)
        {
            var result = await _ObjUserMasterBAL.GetByIdUserMaster(GetByIdUsrmaster).ConfigureAwait(false);
            return Ok(result);

        }


    }
}
    


