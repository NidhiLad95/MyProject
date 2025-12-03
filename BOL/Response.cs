namespace BOL
{
    
        public class Response
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public string? Data { get; set; }
        }
        public class Response<T>
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public T? Data { get; set; }
        }


        public class ResponseList<T> : Response<List<T>>
        {
            public int RecordsFiltered { get; set; }
            public int TotalRecords { get; set; }
        }

        public class ResponseGetList<T> : Response<List<T>> { }

        public class ResponseBind<T1, T2>
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public List<T1>? List1 { get; set; }
            public List<T2>? List2 { get; set; }
        }

        public class ResponseBindDropdownListMulti<T1, T2, T3, T4, T5, T6>
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public List<T1>? List1 { get; set; }
            public List<T2>? List2 { get; set; }
            public List<T3>? List3 { get; set; }
            public List<T4>? List4 { get; set; }
            public List<T5>? List5 { get; set; }
            public List<T6>? List6 { get; set; }
        }

        public class StatusMessage
        {
            public int TotalRecords { get; set; }
            public bool Status { get; set; }
            public string? Message { get; set; }
        }
    }

