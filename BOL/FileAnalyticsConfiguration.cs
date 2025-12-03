using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    //public class FileAnalyticsConfiguration
    //{
    //    public int FileConfigID { get; set; }
    //    public string? FileName { get; set; }
    //    public string? FilePath { get; set; }
    //    public string? Description { get; set; }
    //    public string? PromptConfiguration { get; set; }
    //    public DateTime UploadedAt { get; set; }

    //    public int? CompanyID { get; set; }

    //    public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
    //    public DateTime CreatedOn { get; set; }
    //    public int? CreatedBy { get; set; }
    //    public DateTime? UpdatedOn { get; set; }
    //    public int? UpdatedBy { get; set; }
    //    public bool IsActive { get; set; }
    //    public bool IsDeleted { get; set; }
    //    public DateTime? DeletedOn { get; set; }
    //    public int? DeletedBy { get; set; }
    //}

    //public class FileAnalyticsCreate
    //{
    //    public string? FileName { get; set; }
    //    public string? FilePath { get; set; }
    //    public string? Description { get; set; }
    //    public string? PromptConfiguration { get; set; }
    //    public DateTime UploadedAt { get; set; }
    //    public int? CompanyID { get; set; }
    //    public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
    //    public int? CreatedBy { get; set; }

    //}



    //public class FileAnalyticsCreate_req
    //{

    //    public string? PromptConfiguration { get; set; }

    //    public int? CompanyID { get; set; }
    //    public string? CompanyName { get; set; }
    //   // public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
    //    public int? CreatedBy { get; set; }

    //}

    //public class FileAnalyticsDetail
    //{
    //    public string? FilePath { get; set; }
    //    public string? SQLiteDBName { get; set; }
    //    public int? CompanyID { get; set; }


    //}


    //Suchita 
    public class FileAnalyticsConfiguration
    {
        public int FileConfigID { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public DateTime UploadedAt { get; set; }

        public int? CompanyID { get; set; }

        public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public DateTime CreatedOn { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public int? UpdatedBy { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOn { get; set; }
        public int? DeletedBy { get; set; }
    }

    public class FileAnalyticsCreate
    {
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public DateTime UploadedAt { get; set; }
        public int? CompanyID { get; set; }
        public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? CreatedBy { get; set; }

    }

    public class FileAnalyticsUpdate
    {
        public string? FileName { get; set; }

        [NotMapped]
        [ValidateNever]
        public IFormFile? File { get; set; }
        public string? FilePath { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public DateTime UploadedAt { get; set; }
        public int? CompanyID { get; set; }
        public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? UpdatedBy { get; set; }

    }



    public class FileAnalyticsCreate_req
    {

        public string? PromptConfiguration { get; set; }

        public int? CompanyID { get; set; }
        public string? CompanyName { get; set; }
        // public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? CreatedBy { get; set; }

    }

    public class FileAnalyticsUpdate_req
    {

        public string? PromptConfiguration { get; set; }

        public int? CompanyID { get; set; }
        public string? CompanyName { get; set; }
        // public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? UpdatedBy { get; set; }

    }

    public class FileAnalyticsDetail
    {
        public string? FilePath { get; set; }
        public string? SQLiteDBName { get; set; }
        public string? FileName { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public int? CompanyID { get; set; }
        public int? FileConfigID { get; set; }


    }

}
