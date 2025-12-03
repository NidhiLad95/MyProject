using GenxAi_Solutions_V1.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Text;

namespace GenxAi_Solutions_V1.Utils
{
    public static class PromptBuilder
    {
        public static List<ChatMessage> BuildMessages(
            string userQuestion,
            //string systemPrompt,
            string context,
            IReadOnlyList<ChatMessage> history)
        {
            var sb = new StringBuilder();
            //sb.AppendLine(systemPrompt);
            sb.AppendLine();
            sb.AppendLine("### CONTEXT");
            sb.AppendLine(context);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, sb.ToString())
            };

            if (history != null && history.Count > 0)
                messages.AddRange(history);

            messages.Add(new ChatMessage(ChatRole.User, userQuestion));

            return messages;
        }

        public static string AddUserInputInPrompt(string prompt, string userinput, List<VectorSearchResult<SchemaRecord>> topSchemas)
        {

            var input = new StringBuilder();
            var schema = new StringBuilder();
            input.AppendLine(userinput);
            if (topSchemas != null && topSchemas.Count>0)
            {
                foreach (var s in topSchemas)
                {
                    schema.AppendLine($"== {s.Record.Name} ==");
                    schema.AppendLine(s.Record.SchemaText);
                    schema.AppendLine();
                }
            }
            else
            {
                schema.AppendLine("");
                schema.AppendLine();
            }
            prompt = prompt.Replace("{{$input}}", input.ToString());
            prompt = prompt.Replace("{{$table_schema}}", schema.ToString());


            return prompt;

        }
    }
}
