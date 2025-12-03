using BOL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.CrudOperations
{
    public interface ICRUDOperations
    {

        Task<Response<T>> GetSingleRecord<T>(string storedProcedureName, object parameters);
        Task<ResponseGetList<T>> GetList<T>(string storedProcedureName, object? parameters);
        Task<List<T>> GetListddl<T>(string storedProcedureName, object? parameters);
        Task<ResponseList<T>> GetPaginatedList<T>(string storedProcedureName, object parameters);
        Task<Response<Tuple<T1, List<T2>>>> GetSingleRecord<T1, T2>(string storedProcedureName, object parameters);
        Task<Response<Tuple<T1, List<T2>, List<T3>>>> GetRecord<T1, T2, T3>(string storedProcedureName, object parameters);
        Task<Response<T>> Insert<T>(string storedProcedureName, object parameters);
        Task<Response<T>> Update<T>(string storedProcedureName, object parameters);
        Task<Response<T>> Delete<T>(string storedProcedureName, object parameters);
        Task<Response<T>> InsertUpdateDelete<T>(string storedProcedureName, object parameters);
        Task<Response<TResp>> ExecuteTvpAsync<TResp>(
            string storedProcedureName,
            string tvpParamName,
            DataTable tvp,
            string tvpTypeName,
            object? extraParams = null);

        Task<Response<TResp>> ExecuteTvpAsync<TItem, TResp>(
                string storedProcedureName,
                string tvpParamName,
                string tvpTypeName,
                IEnumerable<TItem> items,
                (string ColumnName, Type DataType, Func<TItem, object?> Selector)[] columnMap,
                object? extraParams = null);

        //Task<ResponseGetList<T>> GetJsonList<T>(string storedProcedureName, object? parameters = null);
        //Task<Response> BulkCopy(string destinationTableName, DataTable orderItems);
        //Task<Response<T>> BulkUpload<T>(string storedProcedureName, object parameters);
        //Task<Response<T>> BulkFileUpload<T>(string storedProcedureName, object parameters);

    }
}
