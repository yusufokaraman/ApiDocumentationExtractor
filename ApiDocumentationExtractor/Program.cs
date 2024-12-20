﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.Rendering;
using PdfSharpCore.Pdf;
using ApiDocumentationExtractor.Models;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Doküman başlığı (örn. 'API Dokümantasyonu'):");
        string docTitle = Console.ReadLine();

        Console.WriteLine("Doküman konusu (örn. 'Otomatik oluşturulan API dokümantasyonu'):");
        string docSubject = Console.ReadLine();

        Console.WriteLine("Yazar adı (örn. 'Şirketiniz'):");
        string docAuthor = Console.ReadLine();

        Console.WriteLine("Versiyon bilgisi (örn. '1.0.0'):");
        string docVersion = Console.ReadLine();

        Console.WriteLine("Swagger JSON dosya yolu (örn. 'Netas_v1_0.json'):");
        string swaggerPath = Console.ReadLine();

        if (!File.Exists(swaggerPath))
        {
            Console.WriteLine($"'{swaggerPath}' dosyası bulunamadı. Program sonlandırılıyor.");
            return;
        }

        var endpoints = ParseSwagger(swaggerPath);
        var groupedByTag = endpoints.GroupBy(e => e.Tag).ToList();

        // PDF oluştur
        CreatePdfDocumentation(groupedByTag, "output.pdf", docTitle, docSubject, docAuthor, docVersion);
        Console.WriteLine("PDF 'output.pdf' oluşturuldu.");

        // HTML oluştur (spec embed)
        CreateSwaggerUIHtml("index.html", swaggerPath, docTitle);
        Console.WriteLine("'index.html' oluşturuldu. HTML dosyasını açarak Swagger UI görebilirsiniz.");

        Console.WriteLine("Tamamlandı.");
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
                var method = methodProp.Name; // get, post vb.
                var methodObj = methodProp.Value as JObject;
                if (methodObj == null) continue;

                var tags = methodObj["tags"]?.ToObject<List<string>>() ?? new List<string>();
                var operationId = methodObj["operationId"]?.ToString();
                var summary = methodObj["summary"]?.ToString();
                var description = methodObj["description"]?.ToString();
                var consumes = methodObj["consumes"]?.ToObject<List<string>>() ?? new List<string>();
                var produces = methodObj["produces"]?.ToObject<List<string>>() ?? new List<string>();

                var parameters = new List<ParameterInfo>();
                if (methodObj["parameters"] is JArray paramArray)
                {
                    foreach (var p in paramArray)
                    {
                        var paramObj = p as JObject;
                        var name = paramObj["name"]?.ToString();
                        var @in = paramObj["in"]?.ToString();
                        var required = paramObj["required"]?.ToObject<bool>() ?? false;
                        var paramDescription = paramObj["description"]?.ToString();
                        var type = paramObj["type"]?.ToString();

                        if (type == null && paramObj["schema"] != null)
                        {
                            type = "object";
                        }

                        parameters.Add(new ParameterInfo
                        {
                            Name = name,
                            In = @in,
                            Required = required,
                            Description = paramDescription,
                            Type = type
                        });
                    }
                }

                var responses = new List<ResponseInfo>();
                if (methodObj["responses"] is JObject respObj)
                {
                    foreach (var respProp in respObj.Properties())
                    {
                        if (int.TryParse(respProp.Name, out int statusCode))
                        {
                            var respDesc = respProp.Value["description"]?.ToString();
                            responses.Add(new ResponseInfo
                            {
                                Code = statusCode,
                                Description = respDesc
                            });
                        }
                    }
                }

                endpoints.Add(new EndpointInfo
                {
                    Tag = tags.FirstOrDefault() ?? "Uncategorized",
                    OperationId = operationId,
                    HttpMethod = method.ToUpper(),
                    Path = route,
                    Summary = summary,
                    Description = description,
                    Consumes = consumes,
                    Produces = produces,
                    Parameters = parameters,
                    Responses = responses
                });
            }
        }

        return endpoints;
    }

    public static void CreatePdfDocumentation(List<IGrouping<string, EndpointInfo>> groupedEndpoints, string outputPath,
                                              string docTitle, string docSubject, string docAuthor, string docVersion)
    {
        var doc = new Document();
        doc.Info.Title = docTitle;
        doc.Info.Subject = docSubject;
        doc.Info.Author = docAuthor;

        // Stil ayarları
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

        doc.Styles["Heading3"].Font.Size = 12;
        doc.Styles["Heading3"].Font.Bold = true;
        doc.Styles["Heading3"].ParagraphFormat.SpaceBefore = "0.3cm";
        doc.Styles["Heading3"].ParagraphFormat.SpaceAfter = "0.2cm";

        // Kapak sayfası
        var section = doc.AddSection();
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        var titleParagraph = section.AddParagraph(docTitle);
        titleParagraph.Format.Font.Size = 20;
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.SpaceAfter = "2cm";
        titleParagraph.Format.Alignment = ParagraphAlignment.Center;

        var versionPara = section.AddParagraph("Versiyon: " + docVersion);
        versionPara.Format.SpaceAfter = "0.5cm";
        versionPara.Format.Alignment = ParagraphAlignment.Center;

        var datePara = section.AddParagraph($"Oluşturulma Tarihi: {DateTime.Now}");
        datePara.Format.Alignment = ParagraphAlignment.Center;

        // İçindekiler sayfası (Sadece paths)
        var tocSection = doc.AddSection();
        tocSection.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.RightMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.TopMargin = Unit.FromCentimeter(2);
        tocSection.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        var tocTitle = tocSection.AddParagraph("İçindekiler");
        tocTitle.Style = "Heading1";

        // İçindekilerde sadece endpointler gösterilir
        // Tüm endpointleri flat bir liste halinde göstermek için:
        var allEndpoints = groupedEndpoints.SelectMany(g => g).ToList();
        foreach (var ep in allEndpoints)
        {
            var displayName = $"{ep.HttpMethod} {ep.Path}";
            var bookmarkId = !string.IsNullOrEmpty(ep.OperationId) ? ep.OperationId : ep.Path;

            var epLine = tocSection.AddParagraph();
            var link = epLine.AddHyperlink(bookmarkId, HyperlinkType.Local);
            link.AddText(displayName);
            epLine.Format.LeftIndent = Unit.FromCentimeter(1);
            epLine.Format.SpaceAfter = "0.1cm";
        }

        // Endpoints bölümü (detaylar)
        var endpointSection = doc.AddSection();
        endpointSection.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        endpointSection.PageSetup.RightMargin = Unit.FromCentimeter(2);
        endpointSection.PageSetup.TopMargin = Unit.FromCentimeter(2);
        endpointSection.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        var endpointsTitle = endpointSection.AddParagraph("Endpoints");
        endpointsTitle.Style = "Heading1";

        foreach (var group in groupedEndpoints)
        {
            var groupTitle = endpointSection.AddParagraph(group.Key);
            groupTitle.Style = "Heading2";

            foreach (var ep in group)
            {
                var epDisplayName = !string.IsNullOrEmpty(ep.Summary) ? ep.Summary :
                                    !string.IsNullOrEmpty(ep.OperationId) ? ep.OperationId :
                                    $"{ep.HttpMethod} {ep.Path}";

                var bookmarkId = !string.IsNullOrEmpty(ep.OperationId) ? ep.OperationId : ep.Path;
                var epPathTitle = endpointSection.AddParagraph($"{ep.HttpMethod} {ep.Path}");
                epPathTitle.Style = "Heading3";
                epPathTitle.AddBookmark(bookmarkId);

                if (!string.IsNullOrEmpty(ep.Summary) && ep.Summary != ep.Path && ep.Summary != ep.OperationId)
                {
                    var summaryPara = endpointSection.AddParagraph(ep.Summary);
                    summaryPara.Format.Font.Italic = true;
                    summaryPara.Format.SpaceAfter = "0.2cm";
                }

                // description
                if (!string.IsNullOrEmpty(ep.Description))
                {
                    var descPara = endpointSection.AddParagraph(ep.Description);
                    descPara.Format.SpaceAfter = "0.3cm";
                }

                // Parameters
                if (ep.Parameters.Any())
                {
                    var paramTitle = endpointSection.AddParagraph("Parameters");
                    paramTitle.Format.Font.Bold = true;
                    paramTitle.Format.SpaceAfter = "0.3cm";

                    var paramTable = endpointSection.AddTable();
                    paramTable.Borders.Width = 0.5;
                    paramTable.Borders.Color = Colors.Gray;
                    paramTable.Format.SpaceAfter = "0.5cm";

                    // Sütun genişlikleri ayarla
                    // name(2.5cm), in(2cm), description(7cm), type(2cm), required(1.5cm)
                    paramTable.AddColumn(Unit.FromCentimeter(2.5));
                    paramTable.AddColumn(Unit.FromCentimeter(2));
                    paramTable.AddColumn(Unit.FromCentimeter(7));
                    paramTable.AddColumn(Unit.FromCentimeter(2));
                    paramTable.AddColumn(Unit.FromCentimeter(1.5));

                    var headerRow = paramTable.AddRow();
                    headerRow.Shading.Color = Colors.LightGray;
                    headerRow.Cells[0].AddParagraph("name").Format.Font.Bold = true;
                    headerRow.Cells[1].AddParagraph("in").Format.Font.Bold = true;
                    headerRow.Cells[2].AddParagraph("description").Format.Font.Bold = true;
                    headerRow.Cells[3].AddParagraph("type").Format.Font.Bold = true;
                    headerRow.Cells[4].AddParagraph("required").Format.Font.Bold = true;

                    foreach (var p in ep.Parameters)
                    {
                        var pRow = paramTable.AddRow();
                        pRow.Cells[0].AddParagraph(p.Name ?? "");
                        pRow.Cells[1].AddParagraph(p.In ?? "");
                        pRow.Cells[2].AddParagraph(p.Description ?? "");
                        pRow.Cells[3].AddParagraph(p.Type ?? "");
                        pRow.Cells[4].AddParagraph(p.Required ? "*" : "");
                    }
                }

                // Responses
                if (ep.Responses.Any())
                {
                    var respTitle = endpointSection.AddParagraph("Responses");
                    respTitle.Format.Font.Bold = true;
                    respTitle.Format.SpaceAfter = "0.3cm";

                    var respTable = endpointSection.AddTable();
                    respTable.Borders.Width = 0.5;
                    respTable.Borders.Color = Colors.Gray;
                    respTable.Format.SpaceAfter = "0.5cm";

                    respTable.AddColumn(Unit.FromCentimeter(2));
                    respTable.AddColumn(Unit.FromCentimeter(14)); // geniş description alanı

                    var respHeader = respTable.AddRow();
                    respHeader.Shading.Color = Colors.LightGray;
                    respHeader.Cells[0].AddParagraph("code").Format.Font.Bold = true;
                    respHeader.Cells[1].AddParagraph("description").Format.Font.Bold = true;

                    foreach (var r in ep.Responses)
                    {
                        var rRow = respTable.AddRow();
                        rRow.Cells[0].AddParagraph(r.Code.ToString());
                        rRow.Cells[1].AddParagraph(r.Description ?? "");
                    }
                }

                endpointSection.AddParagraph("\n");
            }
        }

        var renderer = new PdfDocumentRenderer(true) { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
    }

    public static void CreateSwaggerUIHtml(string htmlFilePath, string swaggerJsonPath, string docTitle)
    {
        var jsonContent = File.ReadAllText(swaggerJsonPath);
        var htmlContent = @$"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <title>{docTitle}</title>
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
