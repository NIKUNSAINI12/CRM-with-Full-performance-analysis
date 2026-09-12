# Ticketing System Project Architecture Skill

This document outlines the high-level architecture, technology stack, and structure of both the Frontend and Backend of the Ticketing System project.

## 1. Overall Architecture

The application is built using a decoupled client-server architecture:
- **Frontend**: A modern Single Page Application (SPA) built with Angular that connects to the backend API.
- **Backend**: A .NET 8 Web API that handles ticketing logic, file uploads, barcode generation, and SQL Server connectivity.

## 2. Frontend Analysis (Angular)

**Path:** `c:\Users\USER\Desktop\TICKETING`

**Core Technologies:**
- **Framework:** Angular 20 (`@angular/core` ^20.1.0)
- **Name:** empower-logics-portal

**Key Libraries & Dependencies:**
- **UI Components:** Heavily utilizes **Syncfusion EJ2 Angular** components (`@syncfusion/ej2-angular-grids`, `ej2-angular-inputs`, `ej2-angular-dropdowns`, etc.) for rich interactive UI elements.
- **Styling:** Angular Material and SCSS.
- **Other utilities:** `rxjs` for reactive programming.

**Structure:**
- Follows standard modern Angular CLI structure. The global API URL is defined in `src/app/app.ts` as `API_BASE_URL`.
- Has distinct folders for different entities like `customer`, `pm-form`, `assignee-form`, etc., found under `src/app/masterform`.

## 3. Backend Analysis (.NET Web API)

**Path:** `c:\Users\USER\Desktop\TICKETING\Backend\ticketing system backend\ticketing system backend`

**Core Technologies:**
- **Framework:** .NET 8 (`net8.0`)
- **Data Access:** Dapper (`Dapper` Version 2.1.66) along with `Microsoft.Data.SqlClient`. This ensures fast data access using raw SQL mapping. (It also references `MySql.Data` which suggests potential cross-database capabilities or migrations).

**Key Libraries & Dependencies:**
- **Barcode/QR Generation:** Uses `BarcodeLib` and `ZXing.Net` for creating and reading barcodes/QR codes (likely for the tickets).
- **Document Processing:** Uses `Syncfusion.DocIO.Net.Core` and `Syncfusion.Pdf.Net.Core` for generating Word and PDF documents. Also uses `iTextSharp`.
- **API Documentation:** Swagger via `Swashbuckle.AspNetCore`.
- **File Uploads:** Custom directory configurations setup in `appsettings.json` pointing to `C:\EmpowerLogics\Uploads`.

**Structure:**
- Contains typical API folders: `Controllers`, `Services`, `Repository`, `Model`, `Interface`, and `Helper`.
- Includes a dedicated `DynamicDescriptionImages` folder, probably related to ticket attachments.

## Summary
The Ticketing System is built for efficiency and enterprise features. The frontend uses the latest Angular 20 and Syncfusion components for a dense, feature-rich interface. The backend API is highly specialized with Dapper for fast querying, robust barcode integration for tickets, and Syncfusion for generating ticket-related PDF documents and reports.
