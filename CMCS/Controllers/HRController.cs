// In Controllers/HRController.cs
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Administrator")] // HR functionality for admins
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfReportService _pdfReportService;

        public HRController(ApplicationDbContext context, IPdfReportService pdfReportService)
        {
            _context = context;
            _pdfReportService = pdfReportService;
        }


        // AUTOMATED REPORT GENERATION - POE REQUIREMENT
        public async Task<IActionResult> GeneratePaymentReport(string period)
        {
            var approvedClaims = await _context.Claims
                .Include(c => c.User)
                .Where(c => c.Status == ClaimStatus.Approved &&
                           (string.IsNullOrEmpty(period) || c.Period == period))
                .OrderBy(c => c.User.LastName)
                .ThenBy(c => c.User.FirstName)
                .ToListAsync();

            // Generate CSV report - POE REQUIREMENT
            var csv = new StringBuilder();
            csv.AppendLine("Lecturer Name,Email,Period,Workload Hours,Hourly Rate,Total Amount,Approval Date");

            decimal totalAmount = 0;
            foreach (var claim in approvedClaims)
            {
                csv.AppendLine($"\"{claim.User.FirstName} {claim.User.LastName}\",{claim.User.Email},{claim.Period},{claim.Workload},R{claim.HourlyRate:N2},R{claim.Amount:N2},{claim.ApprovalDate?.ToString("yyyy-MM-dd")}");
                totalAmount += claim.Amount;
            }

            csv.AppendLine();
            csv.AppendLine($"Total Claims,{approvedClaims.Count}");
            csv.AppendLine($"Total Amount,R{totalAmount:N2}");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"PaymentReport_{period ?? "All"}_{DateTime.Now:yyyyMMdd}.csv");
        }

        // ADD PDF REPORT METHOD
        public async Task<IActionResult> GeneratePdfReport(string period)
        {
            var approvedClaims = await _context.Claims
                .Include(c => c.User)
                .Where(c => c.Status == ClaimStatus.Approved &&
                           (string.IsNullOrEmpty(period) || c.Period == period))
                .OrderBy(c => c.User.LastName)
                .ThenBy(c => c.User.FirstName)
                .ToListAsync();

            var pdfBytes = _pdfReportService.GeneratePaymentReport(approvedClaims, period);
            return File(pdfBytes, "application/pdf", $"PaymentReport_{period ?? "All"}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // LECTURER DATA MANAGEMENT - POE REQUIREMENT
        public async Task<IActionResult> ManageLecturers()
        {
            var lecturers = await _context.Users
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => new LecturerViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    HourlyRate = u.HourlyRate, // ADD THIS
                    DateRegistered = u.DateRegistered,
                    TotalClaims = u.Claims.Count,
                    ApprovedClaims = u.Claims.Count(c => c.Status == ClaimStatus.Approved),
                    TotalAmount = u.Claims.Where(c => c.Status == ClaimStatus.Approved).Sum(c => c.Amount)
                })
                .ToListAsync();

            return View(lecturers);
        }

        // UPDATE LECTURER INFORMATION - POE REQUIREMENT
        [HttpPost]
        public async Task<IActionResult> UpdateLecturerInfo(string id, string firstName, string lastName, string email, decimal hourlyRate)
        {
            var lecturer = await _context.Users.FindAsync(id);
            if (lecturer == null)
            {
                return NotFound();
            }

            lecturer.FirstName = firstName;
            lecturer.LastName = lastName;
            lecturer.Email = email;
            lecturer.UserName = email;
            lecturer.HourlyRate = hourlyRate; // ADD THIS

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Lecturer information updated successfully";
            return RedirectToAction(nameof(ManageLecturers));
        }
    }

    // VIEW MODEL FOR HR - POE REQUIREMENT
    public class LecturerViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; } // ADD THIS
        public DateTime DateRegistered { get; set; }
        public int TotalClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public decimal TotalAmount { get; set; }
    }
}