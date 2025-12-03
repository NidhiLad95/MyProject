using BOL;

namespace GenxAi_Solutions.Models
{
    public class CompanyOnboard
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

    }

    public class CompanyFormViewModel
    {
        public CompanyProfile Company { get; set; }
        public SQLAnalyticsMaster SqlAnalytics { get; set; }
        public List<FileAnalyticsDetail> FileAnalytics { get; set; }
        // public FileAnalyticsConfiguration FileAnalytics { get; set; }
    }

}
