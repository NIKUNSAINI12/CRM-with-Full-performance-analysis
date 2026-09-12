# Database Connection & Architecture Skill (Ticketing System)

This document outlines how the database and API connections are configured across the Ticketing project.

## 1. Backend Database Connection (WebAPI)

The backend connects to the database using Dapper as a micro-ORM, relying on a connection string defined in the `appsettings.json` file.

**File Location:**
`c:\Users\USER\Desktop\TICKETING\Backend\ticketing system backend\ticketing system backend\appsettings.json`

**Connection String Configuration:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=localhost ;Initial Catalog=Ticketing System;Persist Security Info=True;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;User Id=sa;Password=123456;"
}
```
- **Data Source:** `localhost`
- **Initial Catalog:** `Ticketing System`
- **User Id:** `sa`
- **Password:** `123456`

The connection string `DefaultConnection` dictates where the .NET API looks for data.

## 2. Frontend Connection to Backend (Angular)

The frontend communicates with the .NET WebAPI through HTTP endpoints.

**File Location:**
`c:\Users\USER\Desktop\TICKETING\src\app\app.ts`

**API Base URL Configuration:**
```typescript
export const API_BASE_URL = 'https://localhost:7050/api';
```
- The Angular frontend routes all its requests to `https://localhost:7050/api`, which is where the backend runs locally.

## Summary of the Flow:
1. **Frontend (Angular)** makes an HTTP request to `https://localhost:7050/api/...`
2. **Backend (WebAPI)** receives the request on port `7050`.
3. **Backend** reads the `DefaultConnection` string from `appsettings.json`.
4. **Backend** executes the SQL query against the `Ticketing System` database.
5. Data is returned to the Frontend as a JSON response.
