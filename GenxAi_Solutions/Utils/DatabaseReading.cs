using BOL;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Data;
using System.Threading.Tasks;

namespace GenxAi_Solutions.Utils
{
    public static class DatabaseReading
    {
        public static async Task<Dictionary<string, List<Dictionary<string, string>>>> GetSchemaDict(string connectionString)
        {
            var schema = new Dictionary<string, List<Dictionary<string, string>>>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var tables = conn.GetSchema("Tables");
                foreach (DataRow row in tables.Rows)
                {
                    string tableName = row["TABLE_NAME"].ToString();
                    var columns = conn.GetSchema("Columns", new[] { null, null, tableName });
                    var cols = new List<Dictionary<string, string>>();
                    foreach (DataRow col in columns.Rows)
                    {
                        cols.Add(new Dictionary<string, string>
                            {
                                { "name", col["COLUMN_NAME"].ToString() },
                                { "type", col["DATA_TYPE"].ToString() }
                            });
                    }
                    schema[tableName] = cols;
                }
            }

            return schema;
        }

        //async Task<Dictionary<string, List<Dictionary<string, string>>>> GetSchemaDict_Up(string connectionString,string db)
        public static  Dictionary<string, List<Dictionary<string, string>>> GetSchemaDict_Up(string connectionString,string db,List<string>tbl,List<string> vw)
        {
            //var schema = new Dictionary<string, List<Dictionary<string, string>>>();
            var map = new Dictionary<string, List<Dictionary<string, string>>>();

            using (var conn = new SqlConnection(connectionString))
            {
                
                conn.Open();
                conn.ChangeDatabase(db);
                
                // Tables
                var tables = conn.GetSchema("Tables");
                bool alltbl = true;
                bool allvw = true;
                if (tbl.Count() > 1) alltbl = false;
                DataTable tblsel;
                if (!alltbl)
                {
                   tblsel = tables.AsEnumerable()
                     .Where(row => tbl.Contains(row.Field<string>("TABLE_NAME")))
                     .CopyToDataTable();

                }
                else
                {
                    tblsel = tables;
                }
                foreach (DataRow row in tblsel.Rows)
                {
                    var tableSchema = row["TABLE_SCHEMA"]?.ToString();
                    var tableName = row["TABLE_NAME"]?.ToString();
                    if (string.IsNullOrEmpty(tableName)) continue;

                    var key = !string.IsNullOrEmpty(tableSchema) ? $"{tableSchema}.{tableName}" : tableName;

                    // restrictions: { Catalog, Owner/Schema, Table, Column }
                    var columns = conn.GetSchema("Columns", new[] { null, tableSchema, tableName, null });
                    var cols = new List<Dictionary<string, string>>();
                    foreach (DataRow col in columns.Rows)
                    {
                        cols.Add(new Dictionary<string, string>
                        {
                            ["name"] = col["COLUMN_NAME"]?.ToString(),
                            ["type"] = col["DATA_TYPE"]?.ToString()
                        });
                    }
                    map[key] = cols;
                }

                // Views
                var views = conn.GetSchema("Views");
                if (vw.Count() > 1) alltbl = false;
                DataTable vwsel;
                if (!allvw)
                {
                    vwsel = views.AsEnumerable()
                      .Where(row => vw.Contains(row.Field<string>("TABLE_NAME")))
                      .CopyToDataTable();

                }
                else
                {
                    vwsel = views;
                }
                foreach (DataRow row in vwsel.Rows)
                {
                    var viewSchema = row["TABLE_SCHEMA"]?.ToString(); // not VIEW_NAME
                    var viewName = row["TABLE_NAME"]?.ToString();
                    if (string.IsNullOrEmpty(viewName)) continue;

                    var key = !string.IsNullOrEmpty(viewSchema) ? $"{viewSchema}.{viewName}" : viewName;

                    var columns = conn.GetSchema("Columns", new[] { null, viewSchema, viewName, null });
                    var cols = new List<Dictionary<string, string>>();
                    foreach (DataRow col in columns.Rows)
                    {
                        cols.Add(new Dictionary<string, string>
                        {
                            ["name"] = col["COLUMN_NAME"]?.ToString(),
                            ["type"] = col["DATA_TYPE"]?.ToString()
                        });
                    }
                    map[key] = cols;
                }
            }

            return map;
        }
        /// <summary>
        /// Returns user databases (excludes master, model, msdb, tempdb and offline databases).
        /// IMPORTANT: connectionString must point to the server (catalog can be anything, we’ll switch to master).
        /// </summary>
        public static async Task<List<DatabaseDDL>> GetDatabasesAsync(string connectionString)
        {
            var result = new List<DatabaseDDL>();
            using (var conn = new SqlConnection(connectionString))
            {
                
                try
                {
                    await conn.OpenAsync();
                    // always ensure we are querying master
                    conn.ChangeDatabase("master");
                }
                catch(Exception ex)
                { Console.WriteLine(ex.ToString()); }

                var sql = @"
SELECT database_id Value,name Text
FROM sys.databases
WHERE database_id > 4      -- excludes master(1), tempdb(2), model(3), msdb(4)
  AND state = 0            -- ONLINE
ORDER BY name;";

                using (var cmd = new SqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        // 0: INT, 1: NVARCHAR
                        result.Add(new DatabaseDDL
                        {
                            Value = rdr.GetInt32(0),
                            Text = rdr.GetString(1)
                        });
                    }

                }
            }
            return result;
        }

        /// <summary>
        /// Returns schema names from a chosen database (excluding system schemas).
        /// </summary>
        public static async Task<List<DatabaseInfo>> GetDatabseInfoAsync(string connectionString, string databaseName)
        {
            var result = new List<DatabaseInfo>();
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                conn.ChangeDatabase(databaseName);
                

                //                var sql = @"
                //SELECT name
                //FROM sys.schemas
                //WHERE name NOT IN ('sys','INFORMATION_SCHEMA')
                //ORDER BY name;";

                var sql = @"
SELECT  
s.schema_id AS SchemaID,
    s.name       AS SchemaName,
    o.object_id  AS ObjectID,
    o.name       AS ObjectName,
    rtrim(o.type)       AS ObjectType
FROM sys.objects o
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('U', 'V')   -- U = Table, V = View
ORDER BY s.name, o.type, o.name;;";

                try
                {
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            result.Add(new DatabaseInfo
                            {
                                SchemaID = rdr.GetInt32(0),
                                SchemaName = rdr.GetString(1),
                                ObjectID = rdr.GetInt32(2),
                                ObjectName = rdr.GetString(3),
                                ObjectType =Convert.ToChar(rdr.GetString(4).Trim())

                            });
                        }
                    }
                }
                catch (Exception ex) {
                    throw ex;
                }
                
            }
            return result;
        }


        // ---------------------------- MYSQL ----------------------------

        /// <summary>
        /// MySQL: returns user databases (excludes system schemas).
        /// </summary>
        public static async Task<List<DatabaseDDL>> GetDatabasesAsync_MySql(string connectionString)
        {
            var result = new List<DatabaseDDL>();
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();

                const string sql = @"
SELECT 
    /* generate a stable int key using CRC32 */
    CRC32(SCHEMA_NAME) AS Value,
    SCHEMA_NAME        AS Text
FROM information_schema.SCHEMATA
WHERE SCHEMA_NAME NOT IN ('mysql','information_schema','performance_schema','sys')
ORDER BY SCHEMA_NAME;";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        result.Add(new DatabaseDDL
                        {
                            Value = Convert.ToInt32(rdr["Value"]),
                            Text = rdr["Text"].ToString()
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// MySQL: tables & views (ObjectType: 'U' table, 'V' view).
        /// SchemaID/ObjectID are derived via CRC32 (MySQL has no stable object id).
        /// </summary>
        public static async Task<List<DatabaseInfo>> GetDatabseInfoAsync_MySql(string connectionString, string databaseName)
        {
            var result = new List<DatabaseInfo>();
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                // you can also conn.ChangeDatabase(databaseName);
                using (var cmd = new MySqlCommand("USE `" + databaseName.Replace("`", "``") + "`;", conn))
                    await cmd.ExecuteNonQueryAsync();

                const string sql = @"
SELECT 
    CRC32(TABLE_SCHEMA)                           AS SchemaID,
    TABLE_SCHEMA                                  AS SchemaName,
    CRC32(CONCAT(TABLE_SCHEMA,'.',TABLE_NAME))    AS ObjectID,
    TABLE_NAME                                    AS ObjectName,
    'U'                                           AS ObjectType
FROM information_schema.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND TABLE_SCHEMA NOT IN ('mysql','information_schema','performance_schema','sys')

UNION ALL

 b
SELECT 
    CRC32(TABLE_SCHEMA)                           AS SchemaID,
    TABLE_SCHEMA                                  AS SchemaName,
    CRC32(CONCAT(TABLE_SCHEMA,'.',TABLE_NAME))    AS ObjectID,
    TABLE_NAME                                    AS ObjectName,
    'V'                                           AS ObjectType
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA NOT IN ('mysql','information_schema','performance_schema','sys')

ORDER BY SchemaName, ObjectType, ObjectName;";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        result.Add(new DatabaseInfo
                        {
                            SchemaID = Convert.ToInt32(rdr["SchemaID"]),
                            SchemaName = rdr["SchemaName"].ToString(),
                            ObjectID = Convert.ToInt32(rdr["ObjectID"]),
                            ObjectName = rdr["ObjectName"].ToString(),
                            ObjectType = Convert.ToChar(rdr["ObjectType"])
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// MySQL: table -> columns (name/type) map for the selected database.
        /// </summary>
        public static async Task<Dictionary<string, List<Dictionary<string, string>>>> GetSchemaDict_MySql(string connectionString, string databaseName)
        {
            var dict = new Dictionary<string, List<Dictionary<string, string>>>();

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand("USE `" + databaseName.Replace("`", "``") + "`;", conn))
                    await cmd.ExecuteNonQueryAsync();

                const string sql = @"
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        var table = rdr.GetString(0);
                        if (!dict.TryGetValue(table, out var list))
                            dict[table] = list = new List<Dictionary<string, string>>();

                        list.Add(new Dictionary<string, string>
                    {
                        { "name", rdr.GetString(1) },
                        { "type", rdr.GetString(2) }
                    });
                    }
                }
            }
            return dict;
        }

        // ---------------------------- POSTGRESQL ----------------------------

        /// <summary>
        /// PostgreSQL: user databases (excludes templates).
        /// </summary>
        public static async Task<List<DatabaseDDL>> GetDatabasesAsync_Pg(string connectionString)
        {
            var result = new List<DatabaseDDL>();
            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                const string sql = @"
SELECT 
    oid::int           AS Value,
    datname            AS Text
FROM pg_database
WHERE datistemplate = FALSE
ORDER BY datname;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        result.Add(new DatabaseDDL
                        {
                            Value = rdr.GetInt32(0),
                            Text = rdr.GetString(1)
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// PostgreSQL: tables & views with schema info.
        /// ObjectType: 'U' = table (incl. partitioned), 'V' = view (incl. materialized).
        /// Uses OIDs for SchemaID/ObjectID (int fits).
        /// </summary>
        public static async Task<List<DatabaseInfo>> GetDatabseInfoAsync_Pg(string connectionString, string databaseName)
        {
            var result = new List<DatabaseInfo>();
            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                conn.ChangeDatabase(databaseName);

                const string sql = @"
SELECT
    n.oid::int                                         AS SchemaID,
    n.nspname                                          AS SchemaName,
    c.oid::int                                         AS ObjectID,
    c.relname                                          AS ObjectName,
    CASE 
        WHEN c.relkind IN ('r','p') THEN 'U'           -- ordinary/partitioned tables
        WHEN c.relkind IN ('v','m') THEN 'V'           -- view/materialized view
        ELSE NULL
    END                                                AS ObjectType
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r','p','v','m')
  AND n.nspname NOT IN ('pg_catalog','information_schema')
ORDER BY n.nspname, ObjectType, c.relname;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        result.Add(new DatabaseInfo
                        {
                            SchemaID = rdr.GetInt32(0),
                            SchemaName = rdr.GetString(1),
                            ObjectID = rdr.GetInt32(2),
                            ObjectName = rdr.GetString(3),
                            ObjectType = Convert.ToChar(rdr.GetString(4))
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// PostgreSQL: table -> columns (name/type) map for selected DB.
        /// </summary>
        public static async Task<Dictionary<string, List<Dictionary<string, string>>>> GetSchemaDict_Pg(string connectionString, string databaseName)
        {
            var dict = new Dictionary<string, List<Dictionary<string, string>>>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                conn.ChangeDatabase(databaseName);

                const string sql = @"
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema NOT IN ('pg_catalog','information_schema')
ORDER BY table_schema, table_name, ordinal_position;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        var table = $"{rdr.GetString(0)}.{rdr.GetString(1)}"; // schema.table
                        if (!dict.TryGetValue(table, out var list))
                            dict[table] = list = new List<Dictionary<string, string>>();

                        list.Add(new Dictionary<string, string>
                    {
                        { "name", rdr.GetString(2) },
                        { "type", rdr.GetString(3) }
                    });
                    }
                }
            }
            return dict;
        }

        //static DataTable ExecuteQuery(string dbType, string connectionString,string databaseName, string sql)
        //{
        //    var dt = new DataTable();
        //    switch (dbType)
        //    {
        //        case "SQL Server" :
        //            using (var conn = new SqlConnection(connectionString))
        //            {
        //                conn.ChangeDatabase(databaseName);
        //                using (var cmd = new SqlCommand(sql, conn))
        //                {

        //                    cmd.CommandType = CommandType.Text;
        //                    using (var adapter = new SqlDataAdapter(cmd))
        //                    {
        //                        conn.Open();
        //                        adapter.Fill(dt);
        //                    }
        //                }
        //            }
        //            break;

        //        case "MySQL" :
        //            using (var conn = new MySqlConnection(connectionString))
        //            using (var cmd = new MySqlCommand(sql, conn))
        //            {
        //                cmd.CommandType = CommandType.Text;
        //                using (var adapter = new MySqlDataAdapter(cmd))
        //                {
        //                    conn.Open();
        //                    adapter.Fill(dt);
        //                }
        //            }
        //            break;

        //        case "PostgreSQL" :
        //            using (var conn = new NpgsqlConnection(connectionString))
        //            using (var cmd = new NpgsqlCommand(sql, conn))
        //            {
        //                cmd.CommandType = CommandType.Text;
        //                using (var adapter = new NpgsqlDataAdapter(cmd))
        //                {
        //                    conn.Open();
        //                    adapter.Fill(dt);
        //                }
        //            }
        //            break;
        //    }
        //    return dt;
        //}


        public static DataTable ExecuteQueryForChat(string dbType, string connectionString, string databaseName, string sql)
        {
            var dt = new DataTable();
            switch (dbType)
            {
                case "SQL Server":
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        conn.ChangeDatabase(databaseName);
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.CommandType = CommandType.Text;
                            using (var adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                    break;

                case "MySQL":
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        conn.ChangeDatabase(databaseName);
                        using (var cmd = new MySqlCommand(sql, conn))
                        {
                            cmd.CommandType = CommandType.Text;
                            using (var adapter = new MySqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                    break;

                case "PostgreSQL":
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        conn.ChangeDatabase(databaseName);
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.CommandType = CommandType.Text;
                            using (var adapter = new NpgsqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                    break;

                default:
                    throw new ArgumentException($"Unsupported database type: {dbType}");
            }
            return dt;
        }
    }
}
