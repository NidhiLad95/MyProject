using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class UserMaster_BOL
    {
        public class UserMaster
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string LastName { get; set; }
            public string MobileNo { get; set; }
            public string Email { get; set; }
            public string PasswordHash { get; set; }
            public string Address1 { get; set; }
            public string Address2 { get; set; }
            public string Address3 { get; set; }
            public string Pincode { get; set; }
            //public string ProfilePhoto { get; set; }
            public string ProfilePhotoPath { get; set; }
            public int? GroupId { get; set; }
            public int? CompanyId { get; set; }
            public string LoginID { get; set; }
            public int? CreatedBy { get; set; }
            public int? ModifiedBy { get; set; }
        }

        public class AddUserMaster
        {
            //public int Id { get; set; }
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? LastName { get; set; }
            public string? MobileNo { get; set; }
            public string? Email { get; set; }
            public string? Password { get; set; }
            public string? Address1 { get; set; }
            public string? Address2 { get; set; }
            public string? Address3 { get; set; }
            public string? Pincode { get; set; }
            // public string ProfilePhoto { get; set; } 
            [NotMapped]
            public IFormFile? ProfilePhoto { get; set; }
            public string? ProfilePhotoPath { get; set; }
            public int? GroupId { get; set; }
            public int? CompanyId { get; set; }
            public string? LoginID { get; set; }
            public int? CreatedBy { get; set; }
        }



        public class UpdateUserMaster
        {
            public int Id { get; set; }
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? LastName { get; set; }
            public string? MobileNo { get; set; }
            public string? Email { get; set; }
            //public string Password { get; set; }
            public string? Address1 { get; set; }
            public string? Address2 { get; set; }
            public string? Address3 { get; set; }
            public string? Pincode { get; set; }
            //public string ProfilePhoto { get; set; }
            public string? ProfilePhotoPath { get; set; }
            [NotMapped]
            [ValidateNever]
            public IFormFile? ProfilePhoto { get; set; }
            public int? GroupId { get; set; }
            public int? CompanyId { get; set; }
            public string LoginID { get; set; }
            public int? ModifiedBy { get; set; }
        }

        public class DeleteUserMaster
        {
            public int Id { get; set; }
            public int Modifiedby { get; set; }


        }

        public class GetAllUserMaster
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string LastName { get; set; }
            public string MobileNo { get; set; }
            public string Email { get; set; }
            public string Address1 { get; set; }
            public string Address2 { get; set; }
            public string Address3 { get; set; }
            public string Pincode { get; set; }
            public string ProfilePhoto { get; set; }
            public int? GroupId { get; set; }
            public int? CompanyId { get; set; }
            public string LoginID { get; set; }



        }


        public class GetByIdUserMaster
        {
            public int? Id { get; set; }
        }

        public class UserDetailLogin
        {
            public int Id { get; set; }

            public string? Email { get; set; }
            public string? PasswordHash { get; set; }

            public string? DatabaseType { get; set; }
            public string? DatabaseName { get; set; }
            public string? ConnectionString { get; set; }

            public string? SQLitedbName { get; set; }

            public int CompanyId { get; set; }
            public string? CompanyIDs { get; set; }
            public int GroupId { get; set; }
            public string? SQLitedbName_File { get; set; }
        }

        public class GetUserDetailLogin
        {
            public string Email { get; set; }
        }

        public class GetUserDetailsByIdForChangePassword
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string PasswordHash { get; set; }
            //public string IsActive { get; set; }
        }

        public class UpdateUserPasswordModel
        {
            
            //public int UserId { get; set; }

            
            public string NewPasswordHash { get; set; }

           
        }

        public class UpdateUserPassword
        {
            public int UserId { get; set; }
            public string NewPasswordHash { get; set; }
           // public string OldPasswordHash { get; set; }
            public int? ModifiedBy { get; set; }



        }




    }
}
