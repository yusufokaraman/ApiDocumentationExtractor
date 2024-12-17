using Newtonsoft.Json.Linq;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.Rendering;

class Program
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

    public class ParameterInfo
    {
        public string Name { get; set; }
        public string In { get; set; }
        public bool Required { get; set; }
        public string SchemaRef { get; set; }
    }

    static void Main(string[] args)
    {
        string swaggerPath = "Netas_v1_0.json";
        if (!File.Exists(swaggerPath))
        {
            Console.WriteLine($"'{swaggerPath}' dosyası bulunamadı. Lütfen path'i kontrol edin.");
            return;
        }

        var endpoints = ParseSwagger(swaggerPath);
        var groupedByTag = endpoints.GroupBy(e => e.Tag).ToList();

        // PDF oluşturma
        CreatePdfDocumentation(groupedByTag, "output.pdf");
        Console.WriteLine("PDF dokümantasyonu 'output.pdf' olarak üretildi.");

        // HTML oluşturma (Swagger UI spec embed)
        CreateSwaggerUIHtml("index.html", swaggerPath);
        Console.WriteLine("Swagger UI arayüzü 'index.html' dosyasında oluşturuldu (spec embed ile).");

        Console.WriteLine("İşlem tamamlandı.");
    }

    public static List<EndpointInfo> ParseSwagger(string jsonPath)
    {
        var endpoints = new List<EndpointInfo>();
        var json = JObject.Parse(File.ReadAllText(jsonPath));

        var paths = json["paths"] as JObject;
        if (paths == null)
            return endpoints;

        foreach (var pathProperty in paths.Properties())
        {
            string route = pathProperty.Name;
            var pathObj = pathProperty.Value as JObject;

            foreach (var methodProp in pathObj.Properties())
            {
                var method = methodProp.Name; // get, post, vb.
                var methodObj = methodProp.Value as JObject;

                var tags = methodObj["tags"]?.ToObject<List<string>>() ?? new List<string>();
                var operationId = methodObj["operationId"]?.ToString();
                var consumes = methodObj["consumes"]?.ToObject<List<string>>() ?? new List<string>();
                var produces = methodObj["produces"]?.ToObject<List<string>>() ?? new List<string>();

                var parameters = new List<ParameterInfo>();
                if (methodObj["parameters"] is JArray paramArray)
                {
                    foreach (var p in paramArray)
                    {
                        parameters.Add(new ParameterInfo
                        {
                            Name = p["name"]?.ToString(),
                            In = p["in"]?.ToString(),
                            Required = p["required"]?.ToObject<bool>() ?? false,
                            SchemaRef = p["schema"]?["$ref"]?.ToString()
                        });
                    }
                }

                var responses = new Dictionary<int, string>();
                if (methodObj["responses"] is JObject respObj)
                {
                    foreach (var respProp in respObj.Properties())
                    {
                        if (int.TryParse(respProp.Name, out int statusCode))
                        {
                            responses[statusCode] = respProp.Value["description"]?.ToString();
                        }
                    }
                }

                endpoints.Add(new EndpointInfo
                {
                    Tag = tags.FirstOrDefault() ?? "Uncategorized",
                    OperationId = operationId,
                    HttpMethod = method.ToUpper(),
                    Path = route,
                    Consumes = consumes,
                    Produces = produces,
                    Parameters = parameters,
                    Responses = responses
                });
            }
        }

        return endpoints;
    }

    public static void CreatePdfDocumentation(List<IGrouping<string, EndpointInfo>> groupedEndpoints, string outputPath)
    {
        var doc = new Document();
        doc.Info.Title = "API Dokümantasyonu";
        doc.Info.Subject = "Otomatik oluşturulan API dokümantasyonu";
        doc.Info.Author = "Şirketiniz";

        // Genel stiller
        var normalStyle = doc.Styles["Normal"];
        normalStyle.Font.Name = "Arial";
        normalStyle.Font.Size = 10;

        doc.Styles["Heading1"].Font.Size = 16;
        doc.Styles["Heading1"].Font.Bold = true;
        doc.Styles["Heading1"].ParagraphFormat.SpaceBefore = "1cm";
        doc.Styles["Heading1"].ParagraphFormat.SpaceAfter = "0.5cm";

        doc.Styles["Heading2"].Font.Size = 14;
        doc.Styles["Heading2"].Font.Bold = true;
        doc.Styles["Heading2"].ParagraphFormat.SpaceBefore = "0.5cm";
        doc.Styles["Heading2"].ParagraphFormat.SpaceAfter = "0.3cm";

        // Kapak sayfası
        var section = doc.AddSection();
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        var titleParagraph = section.AddParagraph("API Dokümantasyonu");
        titleParagraph.Format.Font.Size = 20;
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.SpaceAfter = "2cm";
        titleParagraph.Format.Alignment = ParagraphAlignment.Center;

        var versionPara = section.AddParagraph("Versiyon: Bilinmiyor");
        versionPara.Format.SpaceAfter = "0.5cm";
        versionPara.Format.Alignment = ParagraphAlignment.Center;

        var datePara = section.AddParagraph($"Oluşturulma Tarihi: {DateTime.Now}");
        datePara.Format.Alignment = ParagraphAlignment.Center;

        // İçindekiler sayfası
        var tocSection = doc.AddSection();
        tocSection.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.RightMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.TopMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        var tocTitle = tocSection.AddParagraph("İçindekiler");
        tocTitle.Format.Font.Bold = true;
        tocTitle.Format.Font.Size = 16;
        tocTitle.Format.SpaceAfter = "1cm";

        foreach (var group in groupedEndpoints)
        {
            var groupTitle = tocSection.AddParagraph(group.Key);
            groupTitle.Style = "Heading2";
            groupTitle.Format.Font.Italic = true;
            foreach (var ep in group)
            {
                var epLine = tocSection.AddParagraph($"- {ep.OperationId} ({ep.HttpMethod} {ep.Path})");
                epLine.Format.LeftIndent = Unit.FromCentimeter(1);
                epLine.Format.SpaceAfter = "0.1cm";
            }
        }

        // Detay sayfaları
        foreach (var group in groupedEndpoints)
        {
            var epSection = doc.AddSection();
            epSection.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            epSection.PageSetup.RightMargin = Unit.FromCentimeter(2);
            epSection.PageSetup.TopMargin = Unit.FromCentimeter(2);
            epSection.PageSetup.BottomMargin = Unit.FromCentimeter(2);

            var tagTitle = epSection.AddParagraph(group.Key);
            tagTitle.Style = "Heading1";

            foreach (var ep in group)
            {
                var epTitle = epSection.AddParagraph($"{ep.HttpMethod} {ep.Path}");
                epTitle.Style = "Heading2";

                var opIdPara = epSection.AddParagraph("OperationId: " + ep.OperationId);
                opIdPara.Format.SpaceAfter = "0.2cm";

                var consumesStr = ep.Consumes.Any() ? string.Join(", ", ep.Consumes) : "Yok";
                var producesStr = ep.Produces.Any() ? string.Join(", ", ep.Produces) : "Yok";

                epSection.AddParagraph("Consumes: " + consumesStr);
                epSection.AddParagraph("Produces: " + producesStr);
                epSection.AddParagraph("");

                // Parametre Tablosu
                if (ep.Parameters.Any())
                {
                    var paramTitle = epSection.AddParagraph("Parametreler:");
                    paramTitle.Format.SpaceAfter = "0.3cm";

                    var paramTable = epSection.AddTable();
                    paramTable.Borders.Width = 0.5;
                    paramTable.Borders.Color = Colors.Gray;
                    paramTable.Format.SpaceAfter = "0.5cm";

                    paramTable.AddColumn(Unit.FromCentimeter(4));
                    paramTable.AddColumn(Unit.FromCentimeter(3));
                    paramTable.AddColumn(Unit.FromCentimeter(3));

                    var headerRow = paramTable.AddRow();
                    headerRow.Shading.Color = Colors.LightGray;
                    headerRow.Cells[0].AddParagraph("Name").Format.Font.Bold = true;
                    headerRow.Cells[1].AddParagraph("In").Format.Font.Bold = true;
                    headerRow.Cells[2].AddParagraph("Required").Format.Font.Bold = true;

                    foreach (var p in ep.Parameters)
                    {
                        var pRow = paramTable.AddRow();
                        pRow.Cells[0].AddParagraph(p.Name ?? "");
                        pRow.Cells[1].AddParagraph(p.In ?? "");
                        pRow.Cells[2].AddParagraph(p.Required.ToString());
                    }
                    epSection.AddParagraph("");
                }

                // Responses
                var respTitle = epSection.AddParagraph("Responses:");
                respTitle.Format.SpaceAfter = "0.3cm";

                if (ep.Responses.Any())
                {
                    foreach (var resp in ep.Responses)
                    {
                        var respPara = epSection.AddParagraph($"{resp.Key}: {resp.Value}");
                        respPara.Format.LeftIndent = Unit.FromCentimeter(0.5);
                    }
                }
                else
                {
                    var noResp = epSection.AddParagraph("Yanıt tanımlı değil.");
                    noResp.Format.LeftIndent = Unit.FromCentimeter(0.5);
                }

                epSection.AddParagraph("\n");
            }
        }

        var renderer = new PdfDocumentRenderer(true) { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
    }

    public static void CreateSwaggerUIHtml(string htmlFilePath, string swaggerJsonPath)
    {
        // Swagger JSON içeriğini oku
        var jsonContent = File.ReadAllText(swaggerJsonPath);

        // HTML içerisine JSON'u göm
        // JSON halihazırda geçerli bir JS nesnesi formatındadır, doğrudan spec içine yerleştirilebilir.
        // Ekstra bir parse gerekmiyor, ancak JSON'un tam olarak `{ "swagger": ... }` gibi başladığından emin olun.
        // Swagger genelde geçerli JSON olduğu için sorun olmaz.

        // HTML içeriği: spec değişkenini jsonContent ile dolduruyoruz.
        var htmlContent = @$"<!DOCTYPE html>
            <html lang=""en"">
            <head>
              <meta charset=""UTF-8"" />
              <title>API Dokümantasyonu</title>
              <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@3/swagger-ui.css"" />
              <style>
                body {{
                  margin: 0;
                  padding: 0;
                }}
                #swagger-ui {{
                  box-sizing: border-box;
                }}
              </style>
            </head>
            <body>
              <div id=""swagger-ui""></div>
  
              <script src=""https://unpkg.com/swagger-ui-dist@3/swagger-ui-bundle.js""></script>
              <script src=""https://unpkg.com/swagger-ui-dist@3/swagger-ui-standalone-preset.js""></script>
              <script>
                const spec = {jsonContent};

                const ui = SwaggerUIBundle({{
                  spec: spec,
                  dom_id: '#swagger-ui',
                  presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                  ],
                  layout: 'BaseLayout'
                }});
              </script>
            </body>
            </html>";

        File.WriteAllText(htmlFilePath, htmlContent);
    }
}
