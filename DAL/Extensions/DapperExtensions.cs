using BOL;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Extensions
{
    public static class DapperExtensions
    {
        public static async Task<Response<T>> ReadResponse<T>(this SqlMapper.GridReader reader)
        {
            var response = await reader.ReadFirstOrDefaultAsync<Response<T>>() ?? new Response<T>();
            if (response.Status)
                response.Data = await reader.ReadFirstOrDefaultAsync<T>();
            return response;
        }

        public static async Task<ResponseList<T>> ReadListResponse<T>(this SqlMapper.GridReader reader)
        {
            var response = await reader.ReadFirstOrDefaultAsync<ResponseList<T>>() ?? new ResponseList<T>();
            if (response.Status)
                response.Data = [.. (await reader.ReadAsync<T>())];
            return response;
        }

        public static async Task<ResponseGetList<T>> ReadGetListResponse<T>(this SqlMapper.GridReader reader)
        {
            var response = await reader.ReadFirstOrDefaultAsync<ResponseGetList<T>>() ?? new ResponseGetList<T>();
            if (response.Status)
                response.Data = [.. (await reader.ReadAsync<T>())];
            return response;
        }
    }
}
