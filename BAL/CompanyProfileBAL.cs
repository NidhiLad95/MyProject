using BAL.Interface;
using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BAL
{
    public class CompanyProfileBAL : ICompanyProfileBAL
    {
        private readonly ICompanyProfileDAL _DALHelper;

        public CompanyProfileBAL(ICompanyProfileDAL DALHelper)
        {
            _DALHelper = DALHelper;
        }
        public async Task<Response<int>> CreateCompanyasync(CompanyProfileCreate model)
        {
            try
            {
                return await _DALHelper.CreateCompanyasync(model);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task<List<DatabaseDDL>> GetDatabasesDdl()
        {
            try
            {
                return  _DALHelper.GetDatabasesDdl();

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<int>> SaveFileAnalyticsConfigAsync(List<FileAnalyticsCreate> model)
        {
           
            if (model == null || model.Count == 0)
            {
                return new Response<int>
                {
                    Status = false,
                    Message = "No items to save.",
                    Data = 0
                };
            }

            try
            {


                // Call the generic TVP method from your DAL (CRUDOperations)
                // Choose TResp based on what your SP returns in its [Status]/[Message]/[Data].
                // If your bulk SP returns just a status/message (no Data), switch TResp to a suitable DTO.
                var resp = await _DALHelper.SaveFileAnalyticsConfigAsync(model);

                return resp;
            }
            catch
            {
                // keep original stack
                throw;
            }
        }

        public async Task<Response<int>> SaveprofileConfigPermanentAsync(SaveProfilePermanent model)
        {
            try
            {
                

                return await _DALHelper.SaveprofileConfigPermanentAsync(model);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<int>> SaveSqlAnalyticConfigasync(SQLAnalyticsCreate model)
        {
            try
            {
                return await _DALHelper.SaveSqlAnalyticConfigasync(model);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<SQLAnalyticsMaster>> GetSQLConfigurationByCompanyId(GetSQLConfigurationByCompanyId dtoGetbyId)
        {
            try
            {
                return await _DALHelper.GetSQLConfigurationByCompanyId(dtoGetbyId);
            }
            catch (Exception ex) 
            {
                throw ex;
            }
            
        }

        public async Task<Response<UserCompanyDetail>> GetUserCompanyId(GetUserCompanyId dtoGetbyId)
        {
            try
            {
                return await _DALHelper.GetUserCompanyId(dtoGetbyId);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<Response<string>> GetPromptCompany(GetPromptCompanyId model)
        {
            try
            {
                return await _DALHelper.GetPromptCompany(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<List<FileAnalyticsDetail>> GetFileConfigurationByCompanyId(GetSQLConfigurationByCompanyId DtoGetbyId)
        {
            try
            {
                return await _DALHelper.GetFileConfigurationByCompanyId(DtoGetbyId);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        
        //Suchita
        public async Task<List<GetAllCompanyList>> GetAllCompanyList()
        {
            return await _DALHelper.GetAllCompanyList();
        }

        //public async Task<Response<CompanyProfile>> UpdateCompanyList(CompanyListUpdate objcompanylist)
        //{
        //    return await _DALHelper.UpdateCompanyList(objcompanylist);

        //}
        public async Task<Response<int>> UpdateCompanyList(CompanyListUpdate objcompanylist)
        {
            try
            {
                return await _DALHelper.UpdateCompanyList(objcompanylist);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<Response<string>> DeleteCompanyList(DeleteCompanyList DtoDelete)
        {
            return await _DALHelper.DeleteCompanyList(DtoDelete);
        }

        public async Task<Response<CompanyProfile>> GetByIdCompanyList(GetByIdCompanyList dtoGetbyId)

        {
            return await _DALHelper.GetByIdCompanyList(dtoGetbyId);
        }


        public async Task<Response<SQLAnalyticsMaster>> UpdateSQLAnalyticsConfiguration(SQLAnalyticsUpdate objsqlanalytics)
        {
            return await _DALHelper.UpdateSQLAnalyticsConfiguration(objsqlanalytics);

        }

        public async Task<Response<int>> UpdateFileAnalyticsConfigAsync(List<FileAnalyticsUpdate> model)
        {

            if (model == null || model.Count == 0)
            {
                return new Response<int>
                {
                    Status = false,
                    Message = "No items to save.",
                    Data = 0
                };
            }

            try
            {


                // Call the generic TVP method from your DAL (CRUDOperations)
                // Choose TResp based on what your SP returns in its [Status]/[Message]/[Data].
                // If your bulk SP returns just a status/message (no Data), switch TResp to a suitable DTO.
                var resp = await _DALHelper.UpdateFileAnalyticsConfigAsync(model);

                return resp;
            }
            catch
            {
                // keep original stack
                throw;
            }
        }

        public async Task<Response<string>> DeleteFileConfig(DeleteFileConfig DtoDelete)
        {
            return await _DALHelper.DeleteFileConfig(DtoDelete);
        }
    }
}
