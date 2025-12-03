namespace GenxAi_Solutions.Utils
{
    public class UserFriendlyException : Exception
    {
        public int StatusCode { get; }
        public string ErrorType { get; }

        public UserFriendlyException(string message, int statusCode = 400, string errorType = "bad_request")
            : base(message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
        }
    }

    public class ValidationException : UserFriendlyException
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(Dictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", 400, "validation_error")
        {
            Errors = errors;
        }
    }

    public class BusinessRuleException : UserFriendlyException
    {
        public BusinessRuleException(string message)
            : base(message, 422, "business_rule_violation")
        {
        }
    }
}
