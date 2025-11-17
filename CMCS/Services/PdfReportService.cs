using CMCS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Document = QuestPDF.Fluent.Document; 
namespace CMCS.Services
{
    public interface IPdfReportService
    {
        byte[] GeneratePaymentReport(List<Claim> claims, string period);
    }

    public class PdfReportService : IPdfReportService
    {
        public byte[] GeneratePaymentReport(List<Claim> claims, string period)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Now 'Document' refers to QuestPDF.Fluent.Document
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .AlignCenter()
                        .Text("Payment Report - Claims Management System")
                        .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            // Report Info
                            column.Item().Text($"Period: {period ?? "All Periods"}");
                            column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                            column.Item().Text($"Total Claims: {claims.Count}");
                            column.Item().Text($"Total Amount: R{claims.Sum(c => c.Amount):N2}");

                            // Table Header
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2); // Name
                                    columns.RelativeColumn(2); // Email
                                    columns.RelativeColumn(2); // Period
                                    columns.RelativeColumn(1); // Hours
                                    columns.RelativeColumn(1); // Rate
                                    columns.RelativeColumn(2); // Amount
                                    columns.RelativeColumn(2); // Date
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Lecturer Name").SemiBold();
                                    header.Cell().Text("Email").SemiBold();
                                    header.Cell().Text("Period").SemiBold();
                                    header.Cell().Text("Hours").SemiBold();
                                    header.Cell().Text("Rate").SemiBold();
                                    header.Cell().Text("Amount").SemiBold();
                                    header.Cell().Text("Approval Date").SemiBold();
                                });

                                // Table Rows
                                foreach (var claim in claims)
                                {
                                    table.Cell().Text($"{claim.User.FirstName} {claim.User.LastName}");
                                    table.Cell().Text(claim.User.Email);
                                    table.Cell().Text(claim.Period);
                                    table.Cell().Text(claim.Workload.ToString("F1"));
                                    table.Cell().Text($"R{claim.HourlyRate:N2}");
                                    table.Cell().Text($"R{claim.Amount:N2}");
                                    table.Cell().Text(claim.ApprovalDate?.ToString("yyyy-MM-dd") ?? "N/A");
                                }
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }
    }
}
