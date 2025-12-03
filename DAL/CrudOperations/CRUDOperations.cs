using BOL;
using DAL.Extensions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.CrudOperations
{
    public class CRUDOperations : ICRUDOperations
    {
        private readonly IConfiguration _configuration;
        private static string? _connectionString;

        public CRUDOperations(IConfiguration configuration, string connectionString)
        {
            _configuration = configuration;
            _connectionString = connectionString;

        }


        public static IDbConnection Connection
        {
            get
            {
                return new SqlConnection(_connectionString!);
            }
        }

        public static void SetupDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IConfigurationSection GetConfigurationSection(string key)
        {
            return _configuration.GetSection(key);
        }

        public string AppSettingsKeys(string nodeName, string key)
        {
            return _configuration["" + nodeName + ":" + key + ""]!;
        }


        private async Task<TResult> ExecuteAsync<TResult>(string storedProcedureName, object? parameters, Func<SqlMapper.GridReader, Task<TResult>> process)
        {
            using var connection = Connection;
            try
            {
                var reader = await connection.QueryMultipleAsync(
                    storedProcedureName,
                    (parameters == null) ? null : GenricsDynamicParamterMapper(parameters),
                    commandType: CommandType.StoredProcedure);
                return await process(reader);
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
        }


        //public async Task<Response<T>> Insert<T>(string storedProcedureName, object? parameters)
        //{
        //    try
        //    {
        //        using var connection = Connection;

        //        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
        //            sql: storedProcedureName,
        //            param: GenricsDynamicParamterMapper(parameters),
        //            commandType: CommandType.StoredProcedure
        //        );

        //        if (result == null)
        //            return new Response<T> { Status = false, Message = "No response from stored procedure" };

        //        bool status = Convert.ToBoolean(result.Status);
        //        string message = Convert.ToString(result.Message) ?? string.Empty;

        //        return new Response<T> { Status = status, Message = message };
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("❌ Error in Insert<T>: " + ex.Message);
        //        return new Response<T> { Status = false, Message = ex.Message };
        //    }
        //}

        public async Task<Response<T>> Insert<T>(string storedProcedureName, object? parameters)
        {
            Response<T> response;
            using var connections = Connection;
            try
            {
                response = await connections.QueryFirstOrDefaultAsync<Response<T>>(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    )!;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
            return response!;

        }





        public async Task<Response<T>> Update<T>(string storedProcedureName, object? parameters)
        {
            Response<T> response;
            using var connections = Connection;
            try
            {
                response = await connections.QueryFirstOrDefaultAsync<Response<T>>(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    )!;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
            return response!;

        }

        public async Task<Response<T>> Delete<T>(string storedProcedureName, object? parameters)
        {

            Response<T> response;
            using var connections = Connection;
            try
            {
                response = await connections.QueryFirstOrDefaultAsync<Response<T>>(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    )!;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
            return response!;

        }

        public async Task<Response<T>> InsertUpdateDelete<T>(string storedProcedureName, object? parameters)
        {
            Response<T> response;
            using var connections = Connection;
            try
            {
                response = await connections.QueryFirstOrDefaultAsync<Response<T>>(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    )!;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
            return response!;
        }

        public async Task<Response<T>> GetSingleRecord<T>(string storedProcedureName, object? parameters) //=>await ExecuteAsync(storedProcedureName, parameters, reader => reader.ReadResponse<T>());
        {
            //Response<T> response;
            using var connections = Connection;
            try
            {
                var result = await connections.QueryMultipleAsync(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    );

                var user = await result.ReadFirstOrDefaultAsync<T>();
                var response = await result.ReadFirstOrDefaultAsync<Response<T>>();
            
                if (user != null && response != null)
                {
                    response.Data = user;

                }
                else
                {
                    response.Status = false;
                    response.Message = "Authentication failed.";
                }
                return response;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
        }

        public async Task<ResponseGetList<T>> GetList<T>(string storedProcedureName, object? parameters) // =>await ExecuteAsync(storedProcedureName, parameters, reader => reader.ReadGetListResponse<T>());
        {
            using var connections = Connection;
            ResponseGetList<T> response = new ResponseGetList<T>();
            try
            {
                object? param=null;
                if(parameters != null)
                {
                    param = GenricsDynamicParamterMapper(parameters);
                }
                var result = await connections.QueryMultipleAsync(
                    sql: storedProcedureName,
                    param: param,
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    );


                var data = (await result.ReadAsync<T>()).ToList();
                var statusMessage = await result.ReadFirstOrDefaultAsync<StatusMessage>();

                if (data != null && statusMessage != null)
                {
                    response.Data = data;
                    response.Message = statusMessage.Message;
                    response.Status = statusMessage.Status;
                    //response.Total_count = data.Count();
                    //response.TotalRecords = statusMessage.TotalRecords;

                }
                else
                {
                    response.Status = false;
                    response.Message = "Authentication failed.";
                }
                return response;


            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
        }

        public async Task<List<T>> GetListddl<T>(string storedProcedureName, object? parameters) // =>await ExecuteAsync(storedProcedureName, parameters, reader => reader.ReadGetListResponse<T>());
        {
            using var connections = Connection;
            List<T> response = new List<T>();
            try
            {
                //object? param = null;
                //if (parameters != null)
                //{
                //    param = GenricsDynamicParamterMapper(parameters);
                //}

                var result = await connections.QueryMultipleAsync(
                    sql: storedProcedureName,
                    param: parameters,
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    );


                var data = (await result.ReadAsync<T>()).ToList();
                //var statusMessage = await result.ReadFirstOrDefaultAsync<StatusMessage>();

                //if (data != null && statusMessage != null)
                if (data != null )
                {
                    response = data;
                    //response.Message = statusMessage.Message;
                    //response.Status = statusMessage.Status;
                    ////response.Total_count = data.Count();
                    ////response.TotalRecords = statusMessage.TotalRecords;

                }
                else
                {
                    //response.Status = false;
                    //response.Message = "Authentication failed.";
                }
                return response;


            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
        }

        //public async Task<ResponseGetList<T>> GetJsonList<T>(string storedProcedureName, object? parameters = null)
        //{
        //    using var connection = Connection;
        //    var response = new ResponseGetList<T>();

        //    try
        //    {
        //        var result = await connection.QueryMultipleAsync(
        //            sql: storedProcedureName,
        //            param: parameters,
        //            commandTimeout: null,
        //            commandType: CommandType.StoredProcedure
        //        );

        //        // ✅ Read full JSON (assuming it's NVARCHAR(MAX))
        //        var jsonResult = await result.ReadFirstOrDefaultAsync<string>();

        //        var list = !string.IsNullOrEmpty(jsonResult)
        //            ? JsonConvert.DeserializeObject<List<T>>(jsonResult)
        //            : new List<T>();

        //        var statusMsg = await result.ReadFirstOrDefaultAsync<StatusMessage>();

        //        response.Data = list;
        //        response.Status = statusMsg?.Status ?? false;
        //        response.Message = statusMsg?.Message ?? "No message returned";


        //        return response;
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }
        //    finally
        //    {
        //        if (connection.State == ConnectionState.Open)
        //            connection.Close();
        //    }
        //}

        //public async Task<ResponseBind<T1, T2>> GetJsonWithBind<T1, T2>(string storedProcedureName, object? parameters = null)
        //{
        //    using var connection = Connection;
        //    var response = new ResponseBind<T1, T2>();

        //    try
        //    {
        //        var result = await connection.QueryMultipleAsync(
        //            sql: storedProcedureName,
        //            param: parameters,
        //            commandTimeout: null,
        //            commandType: CommandType.StoredProcedure
        //        );

        //        // JSON block
        //        var json = await result.ReadFirstOrDefaultAsync<string>();
        //        var list1 = !string.IsNullOrEmpty(json)
        //            ? JsonConvert.DeserializeObject<List<T1>>(json)
        //            : new List<T1>();

        //        // Status or List2 block
        //        var list2 = (await result.ReadAsync<T2>()).ToList();

        //        response.List1 = list1;
        //        response.List2 = list2;
        //        response.Status = true;
        //        response.Message = "Data fetched successfully.";
        //    }
        //    catch (Exception ex)
        //    {
        //        response.Status = false;
        //        response.Message = ex.Message;
        //    }
        //    finally
        //    {
        //        if (connection.State == ConnectionState.Open)
        //            connection.Close();
        //    }

        //    return response;
        //}

        //public async Task<ResponseList<T>> GetPaginatedJsonList<T>(string storedProcedureName, object? parameters = null)
        //{
        //    using var connection = Connection;
        //    var response = new ResponseList<T>();

        //    try
        //    {
        //        var result = await connection.QueryMultipleAsync(
        //            sql: storedProcedureName,
        //            param: parameters,
        //            commandTimeout: null,
        //            commandType: CommandType.StoredProcedure
        //        );

        //        var json = await result.ReadFirstOrDefaultAsync<string>();
        //        var list = !string.IsNullOrEmpty(json)
        //            ? JsonConvert.DeserializeObject<List<T>>(json)
        //            : new List<T>();

        //        var statusMsg = await result.ReadFirstOrDefaultAsync<StatusMessage>();

        //        response.Data = list;
        //        response.Status = statusMsg?.Status ?? false;
        //        response.Message = statusMsg?.Message ?? "No status returned";
        //        response.TotalRecords = statusMsg?.TotalRecords ?? list.Count;
        //        response.RecordsFiltered = list.Count;

        //        return response;
        //    }
        //    catch (Exception ex)
        //    {
        //        response.Status = false;
        //        response.Message = ex.Message;
        //        return response;
        //    }
        //    finally
        //    {
        //        if (connection.State == ConnectionState.Open)
        //            connection.Close();
        //    }
        //}

        public async Task<ResponseList<T>> GetPaginatedList<T>(string storedProcedureName, object? parameters) =>
            await ExecuteAsync(storedProcedureName, parameters, async reader =>
            {
                var response = await reader.ReadListResponse<T>();
                response.TotalRecords = response.RecordsFiltered = response.Status ? (await reader.ReadAsync<int>()).SingleOrDefault() : 0;
                return response;
            });

        public async Task<Response<Tuple<T1, List<T2>>>> GetSingleRecord<T1, T2>(string sp, object? parameters) =>
            await ExecuteAsync(sp, parameters, async reader =>
            {
                var first = await reader.ReadFirstOrDefaultAsync<T1>();
                var second = (await reader.ReadAsync<T2>()).ToList();
                var response = await reader.ReadFirstOrDefaultAsync<Response<Tuple<T1, List<T2>>>>();
                response!.Data = new Tuple<T1, List<T2>>(first!, second);
                return response;
            });

        public async Task<Response<Tuple<T1, List<T2>, List<T3>>>> GetRecord<T1, T2, T3>(string sp, object? parameters) =>
            await ExecuteAsync(sp, parameters, async reader =>
            {
                var one = await reader.ReadFirstOrDefaultAsync<T1>();
                var two = (await reader.ReadAsync<T2>()).ToList();
                var three = (await reader.ReadAsync<T3>()).ToList();
                var response = await reader.ReadFirstOrDefaultAsync<Response<Tuple<T1, List<T2>, List<T3>>>>();
                response!.Data = new Tuple<T1, List<T2>, List<T3>>(one!, two, three);
                return response;
            });

       
        private static DynamicParameters GenricsDynamicParamterMapper(object tmodelObj)
        {
            var parameter = new DynamicParameters();
            var props = tmodelObj.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (!prop.CustomAttributes.Any(x => x.AttributeType.Name.Contains("Ignore")))
                {
                    // Skip IFormFile and Stream-like objects
                    if (typeof(IFormFile).IsAssignableFrom(prop.PropertyType))
                        continue;
                    if (prop.PropertyType == typeof(DataTable))
                        parameter.Add("@" + prop.Name, ((DataTable)prop.GetValue(tmodelObj)!).AsTableValuedParameter());
                    else
                        parameter.Add("@" + prop.Name, prop.GetValue(tmodelObj));
                }
            }
            return parameter;
        }

        public async Task<Response<T>> BulkUpload<T>(string storedProcedureName, object? parameters)
        {
            Response<T> response;
            using var connections = Connection;
            try
            {
                response = await connections.QueryFirstOrDefaultAsync<Response<T>>(
                    sql: storedProcedureName,
                    param: GenricsDynamicParamterMapper(parameters),
                    commandTimeout: null,
                    commandType: CommandType.StoredProcedure
                    )!;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
            return response!;

        }

        // >>> Add inside CRUDOperations class (private helper)
        private static DataTable BuildTvpTable<T>(
            string tvpTypeName,
            IEnumerable<T> items,
            params (string ColumnName, Type DataType, Func<T, object?> Selector)[] columns)
        {
            var tvp = new DataTable { TableName = tvpTypeName };

            // Define schema (order matters and must match the TVP type)
            foreach (var col in columns)
                tvp.Columns.Add(col.ColumnName, Nullable.GetUnderlyingType(col.DataType) ?? col.DataType);

            // Fill rows
            foreach (var item in items)
            {
                var row = tvp.NewRow();
                for (int i = 0; i < columns.Length; i++)
                    row[i] = columns[i].Selector(item) ?? DBNull.Value;
                tvp.Rows.Add(row);
            }

            return tvp;
        }

        // >>> Add inside CRUDOperations class (public API)
        public async Task<Response<TResp>> ExecuteTvpAsync<TResp>(
            string storedProcedureName,
            string tvpParamName,
            DataTable tvp,
            string tvpTypeName,
            object? extraParams = null)
        {
            using var connections = Connection;
            try
            {
                var dp = new DynamicParameters();

                // Add extra scalar params if any
                if (extraParams != null)
                {
                    foreach (var p in extraParams.GetType().GetProperties())
                    {
                        if (!p.CustomAttributes.Any(x => x.AttributeType.Name.Contains("Ignore")))
                            dp.Add("@" + p.Name, p.GetValue(extraParams));
                    }
                }

                // Add TVP (Dapper)
                dp.Add(tvpParamName.StartsWith("@") ? tvpParamName : "@" + tvpParamName,
                       tvp.AsTableValuedParameter(tvpTypeName));

                var response = await connections.QueryFirstOrDefaultAsync<Response<TResp>>(
                    sql: storedProcedureName,
                    param: dp,
                    commandType: CommandType.StoredProcedure
                );

                return response!;
            }
            catch
            {
                throw;
            }
            finally
            {
                if (connections.State == ConnectionState.Open)
                    connections.Close();
            }
        }

        // >>> Add inside CRUDOperations class (public convenience)
        public async Task<Response<TResp>> ExecuteTvpAsync<TItem, TResp>(
            string storedProcedureName,
            string tvpParamName,
            string tvpTypeName,
            IEnumerable<TItem> items,
            (string ColumnName, Type DataType, Func<TItem, object?> Selector)[] columnMap,
            object? extraParams = null)
        {
            var tvp = BuildTvpTable(tvpTypeName, items, columnMap);
            return await ExecuteTvpAsync<TResp>(storedProcedureName, tvpParamName, tvp, tvpTypeName, extraParams);
        }
    }
}
