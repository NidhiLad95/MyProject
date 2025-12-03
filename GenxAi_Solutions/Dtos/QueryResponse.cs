namespace GenxAi_Solutions.Dtos
{
    public record QueryResponse(
        string AssistantReply,
        string Context,
        IEnumerable<string> TopBooks
    );
    public record pdfQueryResponse(
       //string AssistantReply,
       string Context,
       IEnumerable<string> TopBooks
   );
}
