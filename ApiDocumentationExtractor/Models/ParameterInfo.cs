using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiDocumentationExtractor.Models
{
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string In { get; set; }
        public bool Required { get; set; }
        public string SchemaRef { get; set; }
    }
}
