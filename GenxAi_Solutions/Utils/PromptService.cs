using BOL;
using Microsoft.AspNetCore.Http.HttpResults;
using Mysqlx.Crud;
using Mysqlx.Session;
using Org.BouncyCastle.Utilities;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using static Azure.Core.HttpHeader;

namespace GenxAi_Solutions.Utils
{
    public static class PromptService
    {
        // Supplier-Based Rejection Trends 
        public const string SupplierRejectedTrends =
            "**Supplier-Based Rejection Trends**:\r\n   - Provide rejection trends by supplier, both for quantity and value. Include segregated quantities, rejected values, and line rejection quantities or costs as applicable.\r\n   - If the prompt specifies rejection percentages, use the formulas for quantity and value as indicated.\r\n   - **Quantity-based Rejection Trend %**:(Rejected Quantity + Line Rejection Quantity) / Received Quantity\r\n   - **Value-based Rejection Trend %**: (Rejected Value + Total Line Rejection Cost) / Received Value\n\n";

        // Material Description & Supplier Rejection Analysis
        public const string MaterialAndMaterialTypeRejectedTrends =
            "**Material Description & Supplier Rejection Analysis**:\r\n   - Analyze trends by material descriptions and provide overall rejection trends by supplier and material, both for quantity and value.\r\n   - For combined supplier and material trends, calculate monthly rejection percentages as specified.\r\n   - **Quantity-based Rejection Trend %**:(Rejected Quantity + Line Rejection Quantity) / Received Quantity\r\n   - **Value-based Rejection Trend %**: (Rejected Value + Total Line Rejection Cost) / Received Value\n\n";

        //Rejection Trend Analysis
        public const string RejectionTrendAnalysis =
            "**creating Rejection Trend Analysis report based on given following instruction**:\r\n- monthly rejection trends for both quantity and value, detailing rejected quantities, values, and related costs.Use the formulas provided for accurate calculations:\r\n- **Monthly Quantity-based Rejection Trend %**:Month wise  (Rejected Quantity + Line Rejection Quantity) / Received Quantity\r\n- **Monthly Value-based Rejection Trend %**: Month wise(Rejected Value + Total Line Rejection Cost) / Received Value\n\n";

        //Quality PPM (Parts Per Million) Calculations
        public const string QualityPPMCalcution = "**Quality PPM (Parts Per Million) Calculations**:\r\n   - Calculate quality PPM by supplier, both scrubbed and unscrubbed, using quantity and value-based formulas:\r\n     - **Monthly Unscrubbed PPM by Quantity** = Month wise ((Rejected Quantity + Segregated Quantity + Line Rejection Quantity) / Received Quantity) x 1,000,000\r\n     - **Monthly Unscrubbed PPM by Value** = Month wise ((Rejected Value + Segregation Value + Line Rejection Cost) / Received Value) x 1,000,000\r\n     - **Monthly Scrubbed PPM by Quantity**= Month wise ((Rejected Quantity + Line Rejection Quantity) / Received Quantity) x 1,000,000\r\n     - **Monthly Scrubbed PPM by Value**= Month wise((Rejected Value + Line Rejection Cost) / Received Value) x 1,000,000\n\n";

        //Default
        public const string DefaultInstruction =
            "Only generate SQL. Do not explain anything.\n\n";

        // Common header (always same)
        public const string Header =
            "You are an expert SQL developer.\n\n" +
            "Given the user request and the table schema, generate a secure and efficient SQL SELECT query.\n\n";

        //        // Common header(always same)
        //        public const string Header12 = @"

        //GLOBAL RULES
        //- Return ONLY a single SQL statement. No commentary, no code fences.
        //- SELECT-only. Never use UPDATE/DELETE/INSERT/MERGE/TRUNCATE/ALTER/CREATE/DROP.
        //- Respect the provided SCHEMA and table/column names exactly (quote identifiers that contain spaces or symbols).
        //- If the row limit is not specified, cap results to 200 rows.
        //- Prefer simple, executable queries over elaborate ones.

        //NUMERIC FROM TEXT (critical)
        //- If an aggregated value (SUM/AVG/MIN/MAX), arithmetic (+/-/*//), ORDER BY numeric, or filtering uses a column that MAY be text-typed
        //  (char/varchar/nvarchar/untyped/unknown), FIRST normalize and cast to a numeric type.
        //- Normalization means: remove thousands separators and stray spaces; treat empty string as NULL.
        //- ALWAYS do this normalization+cast for aggregations and numeric comparisons UNLESS the SCHEMA explicitly marks the column as a numeric type.

        //IDENTIFIER QUOTING
        //- Quote/escape any identifier that has spaces or special characters.

        //VENDOR DIALECT
        //- Follow the DIALECT section below.
        //DIALECT (SQL Server)
        //- Row limit: SELECT TOP 200 ...
        //- Identifier quoting: [like this]
        //- Numeric-from-text pattern (use in SUM/AVG/etc. and numeric ORDER BY/WHERE):
        //  TRY_CAST(NULLIF(REPLACE(REPLACE(<expr>, ',', ''), ' ', ''), '') AS DECIMAL(18,2))
        //- Examples:
        //  SUM(TRY_CAST(NULLIF(REPLACE(REPLACE([Rejected Qty], ',', ''), ' ', ''), '') AS DECIMAL(18,2)))
        //  ORDER BY TRY_CAST(NULLIF(REPLACE(REPLACE([Amount], ',', ''), ' ', ''), '') AS DECIMAL(18,2)) DESC
        //- If inside CASE, cast INSIDE the CASE branch result.


        //OUTPUT
        //- Output: one SQL statement only (no fences).
        //SELECT AVG(TRY_CAST(NULLIF(REPLACE(REPLACE([Price], ',', ''), ' ', ''), '') AS DECIMAL(18,2))) AS AvgPrice
        //FROM dbo.Items
        //WHERE [Status] = 'Approved';

        //";


        //// Variable instruction (changeable)
        //public const string DefaultInstruction =
        //    "Only generate SQL. Do not explain anything.\n\n";


        //Nidhi Old
        // Rules (always same)
        //public const string Rules =
        //    "Rules:\n" +
        //    "- Only generate SELECT queries.\n" +
        //    "- Use WHERE or GROUP BY only if relevant.\n" +
        //    "- Never use DELETE, UPDATE, INSERT, or DROP.\n" +
        //    "- For name searches, use LIKE unless the user explicitly requests exact matching.\n" +
        //    "- Show some extra columns for better understanding.\n" +
        //    "- Do not return comments or explanations.\n" +
        //    "- Do not use markdown formatting or triple backticks.\n" +
        //    "- Always respond with only SQL code.\n" +
        //    "- Row limit: SELECT TOP 200 ...\r\n- Identifier quoting: [like this]\r\n- Numeric-from-text pattern (use in SUM/AVG/etc., ORDER BY numeric, WHERE numeric):\r\n  TRY_CAST(NULLIF(REPLACE(REPLACE(<expr>, ',', ''), ' ', ''), '') AS DECIMAL(18,2))\r\n- Examples:\r\n  SUM(TRY_CAST(NULLIF(REPLACE(REPLACE([Amount], ',', ''), ' ', ''), '') AS DECIMAL(18,2)))\r\n  ORDER BY TRY_CAST(NULLIF(REPLACE(REPLACE([Amount], ',', ''), ' ', ''), '') AS DECIMAL(18,2)) DESC\r\n- If inside CASE, cast inside the CASE branch result\n\n" +
        //    "Example format:\n" +
        //    "SELECT Column1, Column2 FROM Table WHERE Condition;\n";


        //Harsh
        public const string Rules1 =
    "Rules:\n" +
    "- If the user greets you or makes casual conversation (e.g., \"hi,\" \"hello,\" \"how are you\"), respond politely and naturally./n"+
    "- Only generate SELECT queries.\n" +
    "- Use WHERE or GROUP BY only if relevant.\n" +
    "- For name searches, use LIKE unless the user explicitly requests exact matching.\n" +
    "- Show some extra columns for better understanding.\n" +
    "- Never use DELETE, UPDATE, INSERT, or DROP.\n" +
    "- If user uses DELETE, UPDATE, INSERT, or DROP in their politely reply that those operationa are not allowed.\n" +
    "- Do not return comments or explanations.\n" +
    "- Do not use markdown formatting or triple backticks.\n" +
    "- Always respond with only Microsoft SQL server query.\n\n" +
    "Example format:\n" +
    "SELECT Column1, Column2, Column3 FROM Table WHERE Name LIKE '%value%';\n";


        public const string Rules =
 "Rules:\n" +
 "- If the user greets you or makes casual conversation (e.g., \"hi,\" \"hello,\" \"how are you\"), respond politely and naturally.\n" +
 "- Only generate SELECT queries.\n" +
 "- Use WHERE or GROUP BY only if relevant.\n" +
 "- For name searches, use LIKE unless the user explicitly requests exact matching.\n" +
 "- Show a few extra non-sensitive columns for context when helpful.\n" +
 "- Never use DELETE, UPDATE, INSERT, or DROP.\n" +
 "- If the user asks for DELETE, UPDATE, INSERT, or DROP, reply that those operations are not allowed in polite words.\n" +
 "- Do not return comments or explanations.\n" +
 "- Always respond with only a Microsoft SQL Server query (except for greetings).\n\n" +
 "Example format:\n" +
 "SELECT Column1, Column2, Column3 FROM [dbo].[Table] WHERE [Name] LIKE '%value%';\n";


        //public const string Pdfheader = "You are given raw text extracted from a PDF document.\r\nYour task is to analyze the text and return the information in the format specified.";
        //public const string Pdfrule = "Rules:\r\n- Normalize all dates into YYYY-MM-DD format.\r\n- Keep numeric values as numbers (not strings).\r\n- If a field is missing in the text, set its value to null or an empty array/object as appropriate.\r\n- Preserve the original wording for textual fields.\r\n- If the text contains tabular data, return it as an array (JSON) or rows (CSV).\r\n- Ignore headers, footers, and page numbers unless they are part of the actual content.";

        //// public const string sqlDefault = "You are an expert SQL developer. Given the user request and the table schema, generate a secure and efficient SQL SELECT query. \r\n\r\nUser Request: {{$input}} \r\nRelevant Table Schemas: {{$table_schema}} \r\n\r\nRules: \r\n\r\n- Only generate SELECT queries. \r\n- Use WHERE, GROUP BY, ORDER BY, or HAVING only if relevant. \r\n- Never use DELETE, UPDATE, INSERT, or DROP. \r\n- Do not return comments, explanations, or formatting. \r\n- Always respond with only SQL code. \r\nExample format: \r\nSELECT Column1, Column2 FROM Table WHERE Condition; \r\n\r\nInstructions (Calculations): \r\n\r\nRejection Trend Analysis: \r\n\r\n- Quantity % = (Rejected Quantity + Line Rejection Quantity) / Received Quantity \r\n- Value % = (Rejected Value + Total Line Rejection Cost) / Received Value \r\n- Supplier Trends: Group by supplier, show rejected vs received quantities/values. \r\n- Material/Material Type Trends: Group by material or type, apply same rejection % formulas. \r\n\r\nPPM (Parts Per Million): \r\n- Unscrubbed PPM Qty = ((Rejected Qty + Segregation Qty + Line Rejection Qty) / Received Qty) × 1,000,000 \r\n- Unscrubbed PPM Value = ((Rejected Value + Segregation Value + Line Rejection Cost) / Received Value) × 1,000,000\r\n- Scrubbed PPM Qty = ((Rejected Qty + Line Rejection Qty) / Received Qty) × 1,000,000 \r\n- Scrubbed PPM Value = ((Rejected Value + Line Rejection Cost) / Received Value) × 1,000,000 \r\n\r\nCOPQ (Cost of Poor Quality): \r\n- Rejected Value + Segregation Value + Line Rejection Material Cost + Line Rejection Processing Cost \r\n- Always aggregate (SUM) monthly before applying formulas. \r\n\r\nShow results ordered by month/year.";

        //public const string sqlDefault = "You are an expert SQL developer. Given the user request and the table schema, generate a secure and efficient SQL SELECT query. \r\n\r\nUser Request: \r\n{{$input}} \r\n\r\nRelevant Table Schemas: \r\n{{$table_schema}} \r\n\r\nShape \r\n\r\n🔹 Strict Rules \r\n\r\nOutput only one SQL SELECT query. \r\n\r\nDo not include comments, explanations, markdown, or formatting. \r\n\r\nNever generate INSERT, UPDATE, DELETE, DROP, or schema modifications. \r\n\r\nOnly use columns from the provided schema. Do not assume extra fields. \r\n\r\nIf the request mentions Top N, use: \r\n\r\nTOP N → SQL Server \r\n\r\nLIMIT N → MySQL/Postgres \r\n\r\nFor aggregations, always use SUM first, then apply formulas. Do not use AVG. \r\n\r\nFor monthly calculations, always group by Month and Year. \r\n\r\nNever generate more than one query at a time. \r\n\r\nIf the request is ambiguous, default to the safest interpretation (SELECT only). \r\n\r\nShape \r\n\r\n🔹 Calculation Rules (Enforced) \r\n\r\nRejection Trend Analysis: \r\n\r\nMonthly Quantity % = (SUM(Rejected Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty) \r\n\r\nMonthly Value % = (SUM(Rejected Value) + SUM(Total Line Rejection Cost)) / SUM(Received Value) \r\n\r\nSupplier Trends: \r\n\r\nGroup by supplier, show rejected vs received values/quantities. \r\n\r\nMaterial / Material Type Trends: \r\n\r\nGroup by material/type, apply same rejection % formulas. \r\n\r\nPPM (Parts Per Million): \r\n\r\nUnscrubbed PPM Qty = ((SUM(Rejected Qty) + SUM(Segregation Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty)) × 1,000,000 \r\n\r\nUnscrubbed PPM Value = ((SUM(Rejected Value) + SUM(Segregation Value) + SUM(Line Rejection Cost)) / SUM(Received Value)) × 1,000,000 \r\n\r\nScrubbed PPM Qty = ((SUM(Rejected Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty)) × 1,000,000 \r\n\r\nScrubbed PPM Value = ((SUM(Rejected Value) + SUM(Line Rejection Cost)) / SUM(Received Value)) × 1,000,000 \r\n\r\nCOPQ (Cost of Poor Quality): \r\n= SUM(Rejected Value) + SUM(Segregation Value) + SUM(Line Rejection Material Cost) + SUM(Line Rejection Processing Cost) \r\n\r\nAlways aggregate (SUM) by month/year before applying formulas. \r\n\r\nAlways order results by Month and Year in ascending order unless user specifies otherwise. \r\n\r\nShape \r\n\r\n🔹 Special Rule – Analysis Report \r\n\r\nIf the user asks for an Analysis Report (e.g., “Rejection Trend Analysis Report”): \r\n\r\nOutput must be exactly one row. \r\n\r\nInclude multiple calculated KPI columns (Quantity %, Value %, Scrubbed/Unscrubbed PPMs, COPQ). \r\n\r\nDo not return raw rows or transactional data. \r\n\r\nEnsure calculations are strictly aggregated by the requested Month/Year. \r\n\r\nShape \r\n\r\n🔹 Example Format \r\n\r\nSELECT Column1, Column2 FROM Table WHERE Condition; ";

        //public const string Pdfheader = "You are given raw text extracted from a PDF document.\r\nYour task is to analyze the text and return the information in the format specified.";
        public const string Pdfheader = "You are an AI assistant that answers user queries based on the given context chunks.\r\n";

        //public const string Pdfrule = "Rules:\r\n- Normalize all dates into YYYY-MM-DD format.\r\n- Keep numeric values as numbers (not strings).\r\n- If a field is missing in the text, set its value to null or an empty array/object as appropriate.\r\n- Preserve the original wording for textual fields.\r\n- If the text contains tabular data, return it as an array (JSON) or rows (CSV).\r\n- Ignore headers, footers, and page numbers unless they are part of the actual content.";

        public const string Pdfrule = "Rules:\r\n- 1. If the user greets you or makes casual conversation (e.g., \"hi,\" \"hello,\" \"how are you\"), respond politely and naturally.  \r\n\r\n2. If the user asks a question related to the document:\r\n   - Answer strictly using only the provided context chunks.\r\n   - If the context contains the answer → respond clearly and concisely.\r\n   - If the context does not contain the answer → reply with:\r\n     \"The provided context does not contain the answer.\"\r\n\r\n3. Never guess, never invent information, and never use outside knowledge.\r\n4. Do not answer questions unrelated to the provided context.\r\n";

        // public const string sqlDefault = "You are an expert SQL developer. Given the user request and the table schema, generate a secure and efficient SQL SELECT query. \r\n\r\nUser Request: {{$input}} \r\nRelevant Table Schemas: {{$table_schema}} \r\n\r\nRules: \r\n\r\n- Only generate SELECT queries. \r\n- Use WHERE, GROUP BY, ORDER BY, or HAVING only if relevant. \r\n- Never use DELETE, UPDATE, INSERT, or DROP. \r\n- Do not return comments, explanations, or formatting. \r\n- Always respond with only SQL code. \r\nExample format: \r\nSELECT Column1, Column2 FROM Table WHERE Condition; \r\n\r\nInstructions (Calculations): \r\n\r\nRejection Trend Analysis: \r\n\r\n- Quantity % = (Rejected Quantity + Line Rejection Quantity) / Received Quantity \r\n- Value % = (Rejected Value + Total Line Rejection Cost) / Received Value \r\n- Supplier Trends: Group by supplier, show rejected vs received quantities/values. \r\n- Material/Material Type Trends: Group by material or type, apply same rejection % formulas. \r\n\r\nPPM (Parts Per Million): \r\n- Unscrubbed PPM Qty = ((Rejected Qty + Segregation Qty + Line Rejection Qty) / Received Qty) × 1,000,000 \r\n- Unscrubbed PPM Value = ((Rejected Value + Segregation Value + Line Rejection Cost) / Received Value) × 1,000,000\r\n- Scrubbed PPM Qty = ((Rejected Qty + Line Rejection Qty) / Received Qty) × 1,000,000 \r\n- Scrubbed PPM Value = ((Rejected Value + Line Rejection Cost) / Received Value) × 1,000,000 \r\n\r\nCOPQ (Cost of Poor Quality): \r\n- Rejected Value + Segregation Value + Line Rejection Material Cost + Line Rejection Processing Cost \r\n- Always aggregate (SUM) monthly before applying formulas. \r\n\r\nShow results ordered by month/year.";

        //public const string sqlUserPrompt = "User Request: \r\n{{$input}} \r\n\r\nYou can use these Relevant Table Schemas that I am finding using semantic search on user request or any previous table schemas(if it is in chat history) based on the user request: \r\n{{$table_schema}}";
        public const string sqlUserPrompt = "User Request: \r\n{{$input}} \r\n\r\nYou can use these Relevant Table Schemas that I am finding using semantic search on user request or any previous table schemas(if it is in chat history) based on the user request: \r\n{{$table_schema}}." + "If user asks about generating chart or graph regarding old data then no need to make a new MS SQL query just return the previous MS SQL query.";

        public const string sqlDefault = "You are an expert SQL developer. Given the user request and the table schema, generate a secure and efficient SQL SELECT query. \r\n\r\nUser Request: \r\n{{$input}} \r\n\r\nRelevant Table Schemas: \r\n{{$table_schema}} \r\n\r\nShape \r\n\r\n🔹 Strict Rules \r\n\r\nOutput only one SQL SELECT query. \r\n\r\nDo not include comments, explanations, markdown, or formatting. \r\n\r\nNever generate INSERT, UPDATE, DELETE, DROP, or schema modifications. \r\n\r\nOnly use columns from the provided schema. Do not assume extra fields. \r\n\r\nIf the request mentions Top N, use: \r\n\r\nTOP N → SQL Server \r\n\r\nLIMIT N → MySQL/Postgres \r\n\r\nFor aggregations, always use SUM first, then apply formulas. Do not use AVG. \r\n\r\nFor monthly calculations, always group by Month and Year. \r\n\r\nNever generate more than one query at a time. \r\n\r\nIf the request is ambiguous, default to the safest interpretation (SELECT only). \r\n\r\nShape \r\n\r\n🔹 Calculation Rules (Enforced) \r\n\r\nRejection Trend Analysis: \r\n\r\nMonthly Quantity % = (SUM(Rejected Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty) \r\n\r\nMonthly Value % = (SUM(Rejected Value) + SUM(Total Line Rejection Cost)) / SUM(Received Value) \r\n\r\nSupplier Trends: \r\n\r\nGroup by supplier, show rejected vs received values/quantities. \r\n\r\nMaterial / Material Type Trends: \r\n\r\nGroup by material/type, apply same rejection % formulas. \r\n\r\nPPM (Parts Per Million): \r\n\r\nUnscrubbed PPM Qty = ((SUM(Rejected Qty) + SUM(Segregation Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty)) × 1,000,000 \r\n\r\nUnscrubbed PPM Value = ((SUM(Rejected Value) + SUM(Segregation Value) + SUM(Line Rejection Cost)) / SUM(Received Value)) × 1,000,000 \r\n\r\nScrubbed PPM Qty = ((SUM(Rejected Qty) + SUM(Line Rejection Qty)) / SUM(Received Qty)) × 1,000,000 \r\n\r\nScrubbed PPM Value = ((SUM(Rejected Value) + SUM(Line Rejection Cost)) / SUM(Received Value)) × 1,000,000 \r\n\r\nCOPQ (Cost of Poor Quality): \r\n= SUM(Rejected Value) + SUM(Segregation Value) + SUM(Line Rejection Material Cost) + SUM(Line Rejection Processing Cost) \r\n\r\nAlways aggregate (SUM) by month/year before applying formulas. \r\n\r\nAlways order results by Month and Year in ascending order unless user specifies otherwise. \r\n\r\nShape \r\n\r\n🔹 Special Rule – Analysis Report \r\n\r\nIf the user asks for an Analysis Report (e.g., “Rejection Trend Analysis Report”): \r\n\r\nOutput must be exactly one row. \r\n\r\nInclude multiple calculated KPI columns (Quantity %, Value %, Scrubbed/Unscrubbed PPMs, COPQ). \r\n\r\nDo not return raw rows or transactional data. \r\n\r\nEnsure calculations are strictly aggregated by the requested Month/Year. \r\n\r\nShape \r\n\r\n🔹 Example Format \r\n\r\nSELECT Column1, Column2 FROM Table WHERE Condition; ";

        public static string AddUserInputInPrompt(string prompt, string userinput, List<SchemaEntry> topSchemas)
        {

            var input = new StringBuilder();
            var schema = new StringBuilder();
            input.AppendLine(userinput);

            foreach (var s in topSchemas)
            {
                schema.AppendLine($"== {s.TableName} ==");
                schema.AppendLine(s.SchemaText);
                schema.AppendLine();
            }

            prompt = prompt.Replace("{{$input}}", input.ToString());
            prompt = prompt.Replace("{{$table_schema}}", schema.ToString());


            return prompt;

        }

        public const string SqlPreflightChecklist = """
PRE-FLIGHT CHECKLIST
- Are any aggregated / numeric-used columns text/unknown? If yes, normalize and cast to numeric using the DIALECT pattern.
- Are identifiers with spaces/special characters properly quoted for the DIALECT?
- Is the row limit applied (TOP/LIMIT 200)?
- Is this a single SELECT statement (no DDL/DML, no multiple statements)?
- If CAST/TRY_CAST is needed inside CASE, apply it inside the CASE branch result.
Then output the SQL only (no prose, no fences).
""";

        //public static string PDFAddUserInputInPrompt(string prompt, string userinput, List<(DocumentChunkEntry Entry,double score)> topchunks)
        //{

        //    var input = new StringBuilder();
        //    var schema = new StringBuilder();
        //    input.AppendLine(userinput);

        //    //foreach (var s in topSchemas)
        //    //{
        //    //    schema.AppendLine($"== {s.TableName} ==");
        //    //    schema.AppendLine(s.SchemaText);
        //    //   schema.AppendLine();
        //    //}

        //    prompt = prompt.Replace("{{$input}}", input.ToString());
        //    prompt = prompt.Replace("{{$rawtext}}", topchunks.ToArray();


        //    return prompt;

        //}

        public static string PDFAddUserInputInPrompt(
    string prompt,
    string userinput,
    List<(DocumentChunkEntry Entry, double score)> topchunks)
        {
            var input = new StringBuilder();
            input.AppendLine(userinput);

            // Collect raw text from top chunks
            var rawText = new StringBuilder();
            foreach (var chunk in topchunks)
            {
                // Assuming DocumentChunkEntry has a property like ChunkText or Content
                rawText.AppendLine(chunk.Entry.Text);
            }

            prompt = prompt.Replace("{{$input}}", input.ToString());
            prompt = prompt.Replace("{{$rawtext}}", rawText.ToString());

            return prompt;
        }

        public static string BuildSqlPrompt(string variableInstruction = null)
        {
            var sb = new StringBuilder();

            // Header (fixed)
            sb.AppendLine(Header);

            // Variable instructions (can be swapped per version)
            sb.AppendLine(variableInstruction ?? DefaultInstruction);

            // User request (dynamic)
            sb.AppendLine("User Request:");

            sb.AppendLine("{{$input}}");

            sb.AppendLine();

            // Schema (dynamic)
            sb.AppendLine("Relevant Table Schemas:");
            sb.AppendLine("{{$table_schema}}");

            sb.AppendLine(Rules);

            return sb.ToString();
        }

        public static string PDFAddUserInputBookInPrompt(
    string prompt,
    string userinput,
    string context,
    IEnumerable<string> books)
        {
            var input = new StringBuilder();
            input.AppendLine(userinput);

            // Collect raw text from top chunks
            var book = new StringBuilder();
            foreach (var chunk in books)
            {
                // Assuming DocumentChunkEntry has a property like ChunkText or Content
                book.AppendLine(chunk + "/n");
            }
            var cnxt = new StringBuilder();
            cnxt.AppendLine(context);

            prompt = prompt.Replace("{{$input}}", input.ToString());
            prompt = prompt.Replace("{{$topbooks}}", book.ToString());
            prompt = prompt.Replace("{{$context}}", cnxt.ToString());

            return prompt;
        }


        public static string BuildPDFPrompt(string variableInstruction = null)
        {
            var sb = new StringBuilder();

            //// Header (fixed)
            //sb.AppendLine(Header);

            // Variable instructions (can be swapped per version)
            sb.AppendLine(variableInstruction ?? DefaultInstruction);

            // User request (dynamic)
            sb.AppendLine("User Request:");

            sb.AppendLine("{{$input}}");

            sb.AppendLine();

            // Schema (dynamic)
            sb.AppendLine("Raw PDF Text:");
            sb.AppendLine("{{$rawtext}}");

            sb.AppendLine(Pdfrule);

            return sb.ToString();
        }

        public static string BuildPDFBookPrompt(string variableInstruction = null)
        {
            var sb = new StringBuilder();

            //// Header (fixed)
            //sb.AppendLine(Header);

            // Variable instructions (can be swapped per version)
            sb.AppendLine(variableInstruction ?? DefaultInstruction);

            // User request (dynamic)
            sb.AppendLine("User Request:");

            sb.AppendLine("{{$input}}");

            sb.AppendLine();

            // Schema (dynamic)
            sb.AppendLine("Top Books:");
            sb.AppendLine("{{$topbooks}}");

            sb.AppendLine();

            // Schema (dynamic)
            sb.AppendLine("Context:");
            sb.AppendLine("{{$context}}");
            sb.AppendLine();
            sb.AppendLine(Pdfrule);

            return sb.ToString();
        }
    }
}
