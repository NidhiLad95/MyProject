using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class CompanyProfile
    {
        public int CompanyID { get; set; }
        public string? CompanyName { get; set; }
        public string? BusinessTradeName { get; set; }
        public string? IndustryType { get; set; }
        public string? CompanyRegistrationNumber { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? CompanySize { get; set; }
        public string? Description { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? CorporateAddress { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? OfficialEmail { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? ContactPersonName { get; set; }
        public string? DesignationRole { get; set; }
        public string? WorkEmail { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateContact { get; set; }
        public string? GSTNo { get; set; }
        public string? PanNo { get; set; }
        public string? TANNo { get; set; }

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

    public class CompanyProfileCreate
    {
        //public int CompanyID { get; set; }
        public string? CompanyName { get; set; }
        public string? BusinessTradeName { get; set; }
        public string? IndustryType { get; set; }
        public string? CompanyRegistrationNumber { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? CompanySize { get; set; }
        public string? Description { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? CorporateAddress { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? OfficialEmail { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? ContactPersonName { get; set; }
        public string? DesignationRole { get; set; }
        public string? WorkEmail { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateContact { get; set; }
        public string? GSTNo { get; set; }
        public string? PanNo { get; set; }
        public string? TANNo { get; set; }
        public int? CreatedBy { get; set; }

    }

    public class SaveProfilePermanent
    {
        public int? CompanyID { get; set; }
        //public int? SqlConfigID { get; set; }
        //public int? FileConfigID { get; set; }
        public int? UpdatedBy { get; set; }
    }

    public class  GetSQLConfigurationByCompanyId
    {
        public int CompanyID { get; set; }
    }
    public class GetUserCompanyId
    {
        public int userId { get; set; }
    }

    public class GetPromptCompanyId
    {
        public int CompanyId { get; set; }
        public int SvcType { get; set; }
    }

    //Suchita
    public class GetAllCompanyList
    {
        public int CompanyID { get; set; }
        public string? CompanyName { get; set; }
        public string? BusinessTradeName { get; set; }
        public string? IndustryType { get; set; }
        public string? CompanyRegistrationNumber { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? CompanySize { get; set; }
        public string? Description { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? CorporateAddress { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? OfficialEmail { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? ContactPersonName { get; set; }
        public string? DesignationRole { get; set; }
        public string? WorkEmail { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateContact { get; set; }
        public string? GSTNo { get; set; }
        public string? PanNo { get; set; }
        public string? TANNo { get; set; }
        public string? FileAnalyticsStatus { get; set; }
        public string? SQLAnalyticsStatus { get; set; }

    }
    public class SQLAnalyticsMaster
    {
        public int ConfigID { get; set; }
        public string? DatabaseType { get; set; }
        public string? DataBaseName { get; set; }
        public string? ServerName { get; set; }
        public string? PortNum { get; set; }
        public string? DbUserName { get; set; }
        public string? DbPassword { get; set; }
        public string? SchemaName { get; set; }
        public string? ConnectionString { get; set; }
        public string? TablesSelected { get; set; }
        public string? ViewsSelected { get; set; }
        public string? Description { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? PromptConfiguration { get; set; }
        public string? SQLitedbName { get; set; }


        public int CompanyId { get; set; }

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

    public class UserCompanyDetail
    {
        public string? DatabaseType { get; set; }
        public string? DatabaseName { get; set; }
        public string? ConnectionString { get; set; }

        public string? SQLitedbName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyIDs { get; set; }
        public int GroupId { get; set; }
        public string? SQLitedbName_File { get; set; }
    }

    public class CompanyListUpdate
    {
        public int CompanyID { get; set; }
        public string? CompanyName { get; set; }
        public string? BusinessTradeName { get; set; }
        public string? IndustryType { get; set; }
        public string? CompanyRegistrationNumber { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? CompanySize { get; set; }
        public string? Description { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? CorporateAddress { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? OfficialEmail { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? ContactPersonName { get; set; }
        public string? DesignationRole { get; set; }
        public string? WorkEmail { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateContact { get; set; }
        public string? GSTNo { get; set; }
        public string? PanNo { get; set; }
        public string? TANNo { get; set; }
        //public DateTime? UpdatedOn { get; set; }
        public int? UpdatedBy { get; set; }
        //public bool IsActive { get; set; }

    }

    public class GetByIdCompanyList
    {
        public int CompanyId { get; set; }
    }

    public class DeleteCompanyList
    {
        public int CompanyID { get; set; }
        public int? DeletedBy { get; set; }
    }

    public class DeleteFileConfig
    {
        public int FileId { get; set; }
        public int? DeletedBy { get; set; }
    }
}
