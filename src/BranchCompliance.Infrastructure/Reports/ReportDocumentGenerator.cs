using BranchCompliance.Application.Dashboard;
using BranchCompliance.Application.Reports;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace BranchCompliance.Infrastructure.Reports;

public sealed class ReportDocumentGenerator : IReportDocumentGenerator
{
    public GeneratedReport Spreadsheet(SpreadsheetDocument document)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Results");
        sheet.Cell(1, 1).SetValue("Branch Compliance & Rating");
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(2, 1).SetValue(Safe(document.PeriodName));
        sheet.Cell(3, 1).SetValue("Generated (UTC)");
        sheet.Cell(3, 2).SetValue(document.GeneratedAtUtc);
        sheet.Cell(3, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        string[] headers = ["Rank", "Branch code", "Branch", "Region", "Status", "Score", "Rating", "Finalized (UTC)"];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(5, column + 1).SetValue(headers[column]);
            sheet.Cell(5, column + 1).Style.Font.Bold = true;
            sheet.Cell(5, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBEA");
        }
        for (var index = 0; index < document.Rows.Count; index++)
        {
            var row = document.Rows[index];
            var excelRow = index + 6;
            if (row.Rank is not null) sheet.Cell(excelRow, 1).SetValue(row.Rank.Value);
            sheet.Cell(excelRow, 2).SetValue(Safe(row.BranchCode));
            sheet.Cell(excelRow, 3).SetValue(Safe(row.BranchName));
            sheet.Cell(excelRow, 4).SetValue(Safe(row.Region));
            sheet.Cell(excelRow, 5).SetValue(Safe(WorkflowLabels.Assessment(row.Status)));
            if (row.Score is not null)
            {
                sheet.Cell(excelRow, 6).SetValue(row.Score.Value);
                sheet.Cell(excelRow, 6).Style.NumberFormat.Format = "0.00";
            }
            sheet.Cell(excelRow, 7).SetValue(Safe(row.Rating));
            if (row.FinalizedAtUtc is not null)
            {
                sheet.Cell(excelRow, 8).SetValue(row.FinalizedAtUtc.Value);
                sheet.Cell(excelRow, 8).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            }
        }
        sheet.SheetView.FreezeRows(5);
        sheet.Range(5, 1, Math.Max(5, document.Rows.Count + 5), headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents(4, 36);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new GeneratedReport(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "compliance-results.xlsx");
    }

    public GeneratedReport BranchSummary(BranchResultDocument source)
    {
        EmbeddedFontResolver.EnsureRegistered();
        var document = new Document();
        document.Info.Title = $"{source.BranchName} compliance result";
        document.Info.Subject = "Fictional portfolio branch compliance result summary";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Basic";
        normal.Font.Size = Unit.FromPoint(9);
        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        var title = section.AddParagraph("Branch Compliance & Rating");
        title.Format.Font.Size = Unit.FromPoint(18);
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(22, 55, 68);
        section.AddParagraph(source.PeriodName).Format.SpaceAfter = Unit.FromCentimeter(.5);

        var summary = section.AddTable();
        summary.Borders.Width = .5;
        summary.AddColumn(Unit.FromCentimeter(4));
        summary.AddColumn(Unit.FromCentimeter(12));
        AddSummary(summary, "Branch", $"{source.BranchName} ({source.BranchCode})");
        AddSummary(summary, "Region", source.Region);
        AddSummary(summary, "Final result", $"{source.FinalScore:F2} · {source.FinalRating} · rank {source.Rank}");
        AddSummary(summary, "Provisional result", $"{source.ProvisionalScore:F2} · {source.ProvisionalRating}");
        AddSummary(summary, "Finalized (UTC)", source.FinalizedAtUtc.ToString("dd MMM yyyy HH:mm"));

        var heading = section.AddParagraph("Criterion contributions and appeal effects");
        heading.Format.Font.Size = Unit.FromPoint(12);
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromCentimeter(.7);
        heading.Format.SpaceAfter = Unit.FromCentimeter(.25);
        var table = section.AddTable();
        table.Borders.Width = .35;
        foreach (var width in new[] { 2.1, 5.5, 1.7, 1.9, 1.9, 2.2, 3.1 })
            table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.Shading.Color = Color.FromRgb(221, 235, 234);
        string[] headers = ["Code", "Criterion", "Weight", "Provisional", "Final", "Contribution", "Appeal effect"];
        for (var i = 0; i < headers.Length; i++)
        {
            header.Cells[i].AddParagraph(headers[i]);
            header.Cells[i].Format.Font.Bold = true;
        }
        foreach (var item in source.Criteria)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Code);
            row.Cells[1].AddParagraph($"{item.Category}: {item.Title}");
            row.Cells[2].AddParagraph($"{item.Weight:F2}%");
            row.Cells[3].AddParagraph(item.ProvisionalScore.ToString("F2"));
            row.Cells[4].AddParagraph(item.FinalScore.ToString("F2"));
            row.Cells[5].AddParagraph(item.Contribution.ToString("F4"));
            row.Cells[6].AddParagraph(item.AppealEffect);
        }
        var footer = section.Footers.Primary.AddParagraph(
            $"Generated {source.GeneratedAtUtc:dd MMM yyyy HH:mm} UTC · Fictional portfolio data");
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = Unit.FromPoint(8);
        footer.Format.Font.Color = Colors.Gray;

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = document.Info.Title;
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return new GeneratedReport(stream.ToArray(), "application/pdf", $"{FileName(source.BranchCode)}-result.pdf");
    }

    private static void AddSummary(Table table, string label, string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label).Format.Font.Bold = true;
        row.Cells[1].AddParagraph(value);
    }

    private static string Safe(string value)
        => value.Length > 0 && "=+-@".Contains(value[0]) ? $"'{value}" : value;

    private static string FileName(string value)
    {
        var safe = new string(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return safe.Length == 0 ? "branch" : safe.ToLowerInvariant();
    }
}
