using BOL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interface
{
    public record SqlAnalyticsConfig(string? DatabaseName, string? ConnectionString, string? SchemaName);
    public record FileConfigRow(
    int FileConfigID,
    string? FileName,
    string? FilePath,
    string? Description,
    string? PromptConfiguration,
    DateTime? UploadedAt,
    int CompanyID
);
    public interface ICompanyProfileDAL
    {
        Task<Response<int>> CreateCompanyasync(CompanyProfileCreate model);
        Task<Response<int>> SaveSqlAnalyticConfigasync(SQLAnalyticsCreate model);
        Task<Response<int>> SaveFileAnalyticsConfigAsync(List<FileAnalyticsCreate> model);
        Task<Response<int>> SaveprofileConfigPermanentAsync(SaveProfilePermanent model);
        Task<List<DatabaseDDL>> GetDatabasesDdl();
        Task<Response<SQLAnalyticsMaster>> GetSQLConfigurationByCompanyId(GetSQLConfigurationByCompanyId DtoGetbyId);
        Task<Response<UserCompanyDetail>> GetUserCompanyId(GetUserCompanyId model);
        Task<Response<string>> GetPromptCompany(GetPromptCompanyId model);
        Task<List<FileAnalyticsDetail>> GetFileConfigurationByCompanyId(GetSQLConfigurationByCompanyId DtoGetbyId);

        //Suchita
        Task<List<GetAllCompanyList>> GetAllCompanyList();
        //Task<Response<CompanyProfile>> UpdateCompanyList(CompanyListUpdate companyList);
        Task<Response<int>> UpdateCompanyList(CompanyListUpdate companyList);
        Task<Response<string>> DeleteCompanyList(DeleteCompanyList DtoDelete);
        Task<Response<CompanyProfile>> GetByIdCompanyList(GetByIdCompanyList DtoGetbyId);
        Task<Response<SQLAnalyticsMaster>> UpdateSQLAnalyticsConfiguration(SQLAnalyticsUpdate companyList);

        Task<Response<int>> UpdateFileAnalyticsConfigAsync(List<FileAnalyticsUpdate> model);

        Task<Response<string>> DeleteFileConfig(DeleteFileConfig DtoDelete);
        //Task<Response<SQLAnalyticsMaster>> GetSQLConfigurationByCompanyID(GetSQLConfigurationByCompanyId DtoGetbyId);

    }
}
