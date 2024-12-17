namespace ApiDocumentationExtractor.Models
{
    public class EndpointInfo
    {
        public string Tag { get; set; }
        public string OperationId { get; set; }
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public List<string> Consumes { get; set; } = new List<string>();
        public List<string> Produces { get; set; } = new List<string>();
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
        public Dictionary<int, string> Responses { get; set; } = new Dictionary<int, string>();
    }
}
