using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BOL.UserGroup_BOL;

namespace DAL
{
    public class UserGroup:IUserGroup
    {
        private readonly ICRUDOperations _crudHelper;
        private readonly string _connectionString;

       
        public UserGroup(ICRUDOperations crudHelper, IConfiguration configuration)
        {
            _crudHelper = crudHelper;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }


        public async Task<Response<UserGroupMaster>> InsertUserGroup(AddUserGroup objScreen)
        {

            var data = await _crudHelper.Insert<string>("AddUserGroup", objScreen);
            return new Response<UserGroupMaster>
            {
                Status = data.Status,
                Message = data.Message,


                Data = new UserGroupMaster
                {
                    GroupId = Convert.ToInt32(data.Data),
                    RoleName = objScreen.RoleName,
                    DefaultPage = objScreen.DefaultPage,
                    //CreatedBy=objScreen.Createdby
                }
            };
        }


        public async Task<List<GetAllUserGroup>> GetAllUserGroup()
        {
            return await _crudHelper.GetListddl<GetAllUserGroup>("GetAllUserGroup", null);
        }

       
        public async Task<Response<UserGroupMaster>> UpdateUserGroup(UpdateUserGroup objUsergrpUpdate)
        {
            var data = await _crudHelper.InsertUpdateDelete<string>("Sp_UpdateUserGroup", objUsergrpUpdate);
            return new Response<UserGroupMaster>
            {
                Status = data.Status,
                Message = data.Message,

                Data = new UserGroupMaster
                {
                    GroupId = Convert.ToInt32(data.Data),
                    //CreatedBy = objProduct.CreatedBy,
                    RoleName = objUsergrpUpdate.RoleName,
                    DefaultPage = objUsergrpUpdate.DefaultPage,
                    ModifiedBy = (int)objUsergrpUpdate.ModifiedBy

                }
            };
        }

        public async Task<Response<string>> DeleteUserGroup(DeleteUserGroup DtoDelete)
        {
            return await _crudHelper.InsertUpdateDelete<string>("Sp_DeleteUserGroup", DtoDelete);

        }

        public async Task<Response<UserGroupMaster>> GetByIdUserGroup(GetByIdUserGroup dtoGetbyId)
        {
            return await _crudHelper.GetSingleRecord<UserGroupMaster>("Usp_GetUserGroupById", dtoGetbyId);
        }

        public async Task<List<SelectListItem>> GetUserGroupDDL()
        {
            return await _crudHelper.GetListddl<SelectListItem>("GetUserGroupDdl", null);

        }

        public async Task<List<GetScreenByRole>> GetScreenByRole()
        {
            return await _crudHelper.GetListddl<GetScreenByRole>("GetAllScreenByRole", null);
        }

        //public async Task<Response<UserRoleMaster>> InsertUserRole(SaveRoleScreensModel objUserRole)
        //{

        //    var data = await _crudHelper.Insert<string>("AddOrUpdateRoleScreens", objUserRole);
        //    return new Response<UserRoleMaster>
        //    {
        //        Status = data.Status,
        //        Message = data.Message,


        //        Data = new UserRoleMaster
        //        {
        //            Id = Convert.ToInt32(data.Data),
        //            GroupId = objUserRole.GroupId,
        //            ScreenId = string.Join(",", objUserRole.Screens.Select(s => s.ScreenId)),
        //            //ParentId = objUserRole.ParentId,
        //            CreatedBy = objUserRole.Createdby
        //        }
        //    };
        //}



        public async Task<Response<UserRoleMaster>> InsertUserRole(SaveRoleScreensModel objUserRole)
        {
            // Convert List<ScreenSelection> → DataTable
            var dt = new DataTable();
            dt.Columns.Add("ScreenId", typeof(int));
            dt.Columns.Add("ParentId", typeof(int));

            foreach (var scr in objUserRole.Screens)
            {
                dt.Rows.Add(scr.ScreenId, scr.ParentId);
            }

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("AddOrUpdateRoleScreens", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@GroupId", objUserRole.GroupId);

                var tvpParam = cmd.Parameters.AddWithValue("@Screens", dt);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = "dbo.ScreenSelectionType"; // 👈 must match SQL UDT

                cmd.Parameters.AddWithValue("@CreatedBy", objUserRole.CreatedBy ?? (object)DBNull.Value);


                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Response<UserRoleMaster>
                        {
                            Status = Convert.ToInt32(reader["Status"]) == 1,
                            Message = reader["Message"].ToString(),
                            Data = new UserRoleMaster
                            {
                                GroupId = objUserRole.GroupId,
                                ScreenId = string.Join(",", objUserRole.Screens.Select(s => s.ScreenId)),
                                CreatedBy = (int)objUserRole.CreatedBy
                            }
                        };
                    }
                }
            }

            return new Response<UserRoleMaster>
            {
                Status = false,
                Message = "No response from AddOrUpdateRoleScreens"
            };
        }




        public async Task<List<ScreenroleMaster>> GetRoleScreens(GetByIdUserGroup dtoGetbyIdScreen)
        {
            return await _crudHelper.GetListddl<ScreenroleMaster>("GetRoleScreens", dtoGetbyIdScreen);
        }
    }
}
