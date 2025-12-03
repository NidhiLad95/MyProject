using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class SQLAnalyticsConfiguration
    {
        public int ConfigID { get; set; }
        public string? DatabaseType { get; set; }
        public string? DatabaseName { get; set; }
        public string? Connectionstring { get; set; }
        public string? ServerName { get; set; }
        public string? DbUserName { get; set; }
        public string? DbPassword { get; set; }
        public string? PortNum { get; set; }
        public string? SchemaName { get; set; }
        public string? TablesSelected { get; set; }
        public string? ViewsSelected { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }

        public int? CompanyID { get; set; }

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

    public class SQLAnalyticsCreate
    {
        public string? DatabaseType { get; set; }
        public string? DatabaseName { get; set; }
        public string? Connectionstring { get; set; }
        public string? ServerName { get; set; }
        public string? DbUserName { get; set; }
        public string? DbPassword { get; set; }
        public string? PortNum { get; set; }
        public string? SchemaName { get; set; }
        public string? TablesSelected { get; set; }
        public string? ViewsSelected { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public int? CompanyID { get; set; }
        public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? CreatedBy { get; set; }

    }

    public class GetSchemaDdl
    {
        public string ConnectionString { get; set; }
        public string? DatabaseType { get; set; }
    }

    public class ConnectionDto
    {
        public string? ConnectionString { get; set; }
        public string? Database { get; set; }
        public string? DatabaseType { get; set; } // only for schemas endpoint
    }
    public class DatabaseDDL
    {
        public int Value { get; set; }
        public string Text { get; set; }
    }

    public class DatabaseInfo
    {
        public int SchemaID { get; set; }
        public string SchemaName { get; set; }
        public int ObjectID { get; set; }
        public string ObjectName { get; set; }
        public char ObjectType { get; set; }
    }

    public class TableViewInfo
    {
        public string SchemaName { get; set; }
        public int ObjectID { get; set; }
        public string ObjectName { get; set; }
    }

    //suchita
    public class SQLAnalyticsUpdate
    {
        public string? DatabaseType { get; set; }
        public string? DatabaseName { get; set; }
        public string? Connectionstring { get; set; }
        public string? ServerName { get; set; }
        public string? DbUserName { get; set; }
        public string? DbPassword { get; set; }
        public string? PortNum { get; set; }
        public string? SchemaName { get; set; }
        public string? TablesSelected { get; set; }
        public string? ViewsSelected { get; set; }
        public string? Description { get; set; }
        public string? PromptConfiguration { get; set; }
        public int? CompanyID { get; set; }
        public int flgSave { get; set; }   // (1 - temp, 2 - permanent)
        public int? UpdatedBy { get; set; }
    }
}
