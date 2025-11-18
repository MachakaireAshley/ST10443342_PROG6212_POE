**Claims Management System (CMCS)**
*Project Overview*
The Claims Management System (CMCS) is a comprehensive web application built with ASP.NET Core MVC that streamlines the process of managing teaching claims for academic institutions. The system provides role-based access control for HR administrators, lecturers, program coordinators, and academic managers.

*Key Features*

HR Administrator Features
1. User Management: Create and manage all system users (lecturers, coordinators, managers)
2. Hourly Rate Management: Set and update hourly rates for lecturers
3. Reporting: Generate PDF and CSV reports for payment processing
4. System Statistics: View comprehensive system analytics and usage data

Lecturer Features
1. Claim Submission: Submit monthly teaching claims with auto-calculation
2. Document Upload: Upload supporting documents for claims
3. Claim Tracking: View claim status and history
4. Real-time Notifications: Receive updates on claim status changes

Program Coordinator Features
1. Claim Review: Review and approve/reject pending claims
2. Validation: Verify claims against institutional criteria
3. Dashboard: Monitor all claims requiring coordinator attention

Academic Manager Features
1. Final Approval: Provide final approval on coordinator-approved claims
2. Quality Control: Perform final verification checks
3. Oversight: Monitor all claims in the approval pipeline

**Technical Specifications**
*Technology Stack*
Backend: ASP.NET Core 8.0 MVC
Database: SQL Server with Entity Framework Core
Authentication: ASP.NET Core Identity with Role Management
Frontend: Bootstrap 5.3, jQuery, Razor Pages
Reporting: QuestPDF for PDF generation, CSV export
Session Management: Distributed memory cache

*Architecture*
MVC Pattern: Clean separation of concerns
Repository Pattern: Data access abstraction
Service Layer: Business logic encapsulation
Role-based Authorization: Secure access control

**Workflow Process**
*Claim Submission & Approval Flow*
1. HR Setup: HR creates lecturer accounts with hourly rates
2. Claim Submission: Lecturers submit monthly claims with workload hours
3. Auto-calculation: System calculates amount using HR-set hourly rates
4. Coordinator Review: Program coordinators validate and approve claims
5. Manager Approval: Academic managers provide final approval
6. Payment Processing: HR generates reports for approved claims

