using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class CompanyProfileDAL : ICompanyProfileDAL
    {
        private readonly ICRUDOperations _crudHelper;

        public CompanyProfileDAL(ICRUDOperations crudHelper)
        {
            _crudHelper = crudHelper;
        }
        public async Task<Response<int>> CreateCompanyasync(CompanyProfileCreate model)
        {
            try
            {
                return await _crudHelper.Insert<int>("Usp_CompanyProfileCreate", model);
            }
            catch (Exception ex)
            {
                throw ex ;
            }
        }

        public Task<List<DatabaseDDL>> GetDatabasesDdl()
        {
            try
            {
                return _crudHelper.GetListddl<DatabaseDDL>("GetDatabases", null);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<int>> SaveFileAnalyticsConfigAsync(List<FileAnalyticsCreate> model)
        {
            try
            {
                var tvp = new DataTable { TableName = "dbo.FileAnalyticsCreateType" };
                tvp.Columns.Add("FileName", typeof(string));
                tvp.Columns.Add("FilePath", typeof(string));
                tvp.Columns.Add("Description", typeof(string));
                tvp.Columns.Add("PromptConfiguration", typeof(string));
                tvp.Columns.Add("UploadedAt", typeof(DateTime));
                tvp.Columns.Add("CompanyID", typeof(int));
                tvp.Columns.Add("flgSave", typeof(int));
                tvp.Columns.Add("CreatedBy", typeof(int));

                foreach (var x in model)
                {
                    tvp.Rows.Add(
                        x.FileName,
                        x.FilePath,
                        x.Description,
                        x.PromptConfiguration,
                        x.UploadedAt == default ? (object)DBNull.Value : x.UploadedAt,
                        (object?)x.CompanyID ?? DBNull.Value,
                        x.flgSave,
                        (object?)x.CreatedBy ?? DBNull.Value
                    );
                }
                var resp = await _crudHelper.ExecuteTvpAsync<int>(
            storedProcedureName: "dbo.Usp_FileAnalyticsConfigurationCreate_Bulk",
            tvpParamName: "@Items",
            tvp: tvp,
            tvpTypeName: "dbo.FileAnalyticsCreateType",
            extraParams: null
        );


                //return await _crudHelper.Insert<int>("Usp_FileAnalyticsConfigurationCreate", model);
                return resp;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<int>> SaveprofileConfigPermanentAsync(SaveProfilePermanent model)
        {
            try
            {
                return await _crudHelper.InsertUpdateDelete<int>("Usp_ProfileConfigSavePermanent", model);
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
                return await _crudHelper.Insert<int>("Usp_SQLAnalyticsConfigurationCreate", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<SQLAnalyticsMaster>> GetSQLConfigurationByCompanyId(GetSQLConfigurationByCompanyId dtoGetbyId)

        {
            return await _crudHelper.GetSingleRecord<SQLAnalyticsMaster>("GetSQLConfigurationByCompanyID", dtoGetbyId);
        }

        public async Task<List<FileAnalyticsDetail>> GetFileConfigurationByCompanyId(GetSQLConfigurationByCompanyId DtoGetbyId)
        {

            return await _crudHelper.GetListddl<FileAnalyticsDetail>("GetFileConfigurationByCompanyID", DtoGetbyId);
        }

        public async Task<Response<UserCompanyDetail>> GetUserCompanyId(GetUserCompanyId DtoGetbyId)
        {

            return await _crudHelper.GetSingleRecord<UserCompanyDetail>("GetUserInfo", DtoGetbyId);
        }

        public async Task<Response<string>> GetPromptCompany(GetPromptCompanyId model)
        {

            return await _crudHelper.GetSingleRecord<string>("GetPromptCompany", model);
        }

        //Suchita
        public async Task<List<GetAllCompanyList>> GetAllCompanyList()
        {
            return await _crudHelper.GetListddl<GetAllCompanyList>("GetAllCompanyList", null);
        }

        //public async Task<Response<CompanyProfile>> UpdateCompanyList(CompanyListUpdate objcompanylistupdate)
        //{
        //    var data = await _crudHelper.InsertUpdateDelete<string>("Sp_UpdateCompanyList", objcompanylistupdate);
        //    return new Response<CompanyProfile>
        //    {
        //        Status = data.Status,
        //        Message = data.Message,

        //        Data = new CompanyProfile
        //        {
        //            CompanyID = Convert.ToInt32(data.Data),
        //            CreatedBy = objProduct.CreatedBy,
        //            CompanyName = objcompanylistupdate.CompanyName,
        //            BusinessTradeName = objcompanylistupdate.BusinessTradeName,
        //            IndustryType = objcompanylistupdate.IndustryType,
        //            CompanyRegistrationNumber = objcompanylistupdate.CompanyRegistrationNumber,
        //            DateOfIncorporation = objcompanylistupdate.DateOfIncorporation,
        //            CompanySize = objcompanylistupdate.CompanySize,
        //            Description = objcompanylistupdate.Description,
        //            RegisteredAddress = objcompanylistupdate.RegisteredAddress,
        //            CorporateAddress = objcompanylistupdate.CorporateAddress,
        //            PrimaryPhone = objcompanylistupdate.PrimaryPhone,
        //            OfficialEmail = objcompanylistupdate.OfficialEmail,
        //            CompanyWebsite = objcompanylistupdate.CompanyWebsite,
        //            ContactPersonName = objcompanylistupdate.ContactPersonName,
        //            DesignationRole = objcompanylistupdate.DesignationRole,
        //            WorkEmail = objcompanylistupdate.WorkEmail,
        //            MobileNumber = objcompanylistupdate.MobileNumber,
        //            AlternateContact = objcompanylistupdate.AlternateContact,
        //            GSTNo = objcompanylistupdate.GSTNo,
        //            PanNo = objcompanylistupdate.PanNo,
        //            TANNo = objcompanylistupdate.TANNo,
        //            UpdatedBy = objcompanylistupdate.UpdatedBy

        //        }
        //    };
        //}
        public async Task<Response<int>> UpdateCompanyList(CompanyListUpdate objcompanylistupdate)
        {
            //var data = await _crudHelper.InsertUpdateDelete<string>("Sp_UpdateCompanyList", objcompanylistupdate);
            try
            {
                return await _crudHelper.Update<int>("Sp_UpdateCompanyList", objcompanylistupdate);
            }
            catch (Exception ex)
            {
                throw ex;
            }


        }

        public async Task<Response<string>> DeleteCompanyList(DeleteCompanyList DtoDelete)

        {
            return await _crudHelper.InsertUpdateDelete<string>("Sp_DeleteCompanyList", DtoDelete);

        }

        public async Task<Response<CompanyProfile>> GetByIdCompanyList(GetByIdCompanyList dtoGetbyId)
        {
            return await _crudHelper.GetSingleRecord<CompanyProfile>("GetCompanyListById", dtoGetbyId);
        }

        public async Task<Response<SQLAnalyticsMaster>> UpdateSQLAnalyticsConfiguration(SQLAnalyticsUpdate objcompanylistupdate)
        {
            var data = await _crudHelper.InsertUpdateDelete<string>("Usp_SQLAnalyticsConfigurationUpdate_New", objcompanylistupdate);
            return new Response<SQLAnalyticsMaster>
            {
                Status = data.Status,
                Message = data.Message,

                Data = new SQLAnalyticsMaster
                {
                    CompanyId = Convert.ToInt32(data.Data),
                    //CreatedBy = objProduct.CreatedBy,
                    DatabaseType = objcompanylistupdate.DatabaseType,
                    DataBaseName = objcompanylistupdate.DatabaseName,
                    ConnectionString = objcompanylistupdate.Connectionstring,
                    ServerName = objcompanylistupdate.ServerName,
                    DbUserName = objcompanylistupdate.DbUserName,
                    DbPassword = objcompanylistupdate.DbPassword,
                    Description = objcompanylistupdate.Description,
                    PortNum = objcompanylistupdate.PortNum,
                    SchemaName = objcompanylistupdate.SchemaName,
                    TablesSelected = objcompanylistupdate.TablesSelected,
                    ViewsSelected = objcompanylistupdate.ViewsSelected,
                    PromptConfiguration = objcompanylistupdate.PromptConfiguration,
                    flgSave = objcompanylistupdate.flgSave,
                    UpdatedBy = objcompanylistupdate.UpdatedBy

                }
            };
        }

        public async Task<Response<int>> UpdateFileAnalyticsConfigAsync(List<FileAnalyticsUpdate> model)
        {
            try
            {
                var tvp = new DataTable { TableName = "dbo.FileAnalyticsUpdateType" };
                tvp.Columns.Add("FileName", typeof(string));
                tvp.Columns.Add("FilePath", typeof(string));
                tvp.Columns.Add("Description", typeof(string));
                tvp.Columns.Add("PromptConfiguration", typeof(string));
                tvp.Columns.Add("UploadedAt", typeof(DateTime));
                tvp.Columns.Add("CompanyID", typeof(int));
                tvp.Columns.Add("flgSave", typeof(int));
                tvp.Columns.Add("UpdatedBy", typeof(int));

                foreach (var x in model)
                {
                    tvp.Rows.Add(
                        x.FileName,
                        x.FilePath,
                        x.Description,
                        x.PromptConfiguration,
                        x.UploadedAt == default ? (object)DBNull.Value : x.UploadedAt,
                        (object?)x.CompanyID ?? DBNull.Value,
                        x.flgSave,
                        (object?)x.UpdatedBy ?? DBNull.Value
                    );
                }
                var resp = await _crudHelper.ExecuteTvpAsync<int>(
            storedProcedureName: "dbo.Usp_FileAnalyticsConfigurationUpdate_Bulk",
            tvpParamName: "@Items",
            tvp: tvp,
            tvpTypeName: "dbo.FileAnalyticsUpdateType",
            extraParams: null
        );


                //return await _crudHelper.Insert<int>("Usp_FileAnalyticsConfigurationCreate", model);
                return resp;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public async Task<Response<string>> DeleteFileConfig(DeleteFileConfig DtoDelete)

        {
            return await _crudHelper.InsertUpdateDelete<string>("Sp_DeleteFileAnalyticsConfiguration", DtoDelete);

        }


    }
}
