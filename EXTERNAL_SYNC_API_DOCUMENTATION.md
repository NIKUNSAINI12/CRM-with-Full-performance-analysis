# Enterprise Data Synchronization API Specification
**Version:** 2.0  
**Target Systems:** Logistics TMS / ERP System & CRM Ticketing Platform  
**Base URL (Local/Dev):** `http://localhost:5058/api/external/sync`  
**Base URL (Staging/Prod):** `https://<crm-domain>/api/external/sync`  
**Content-Type:** `application/json`  

---

## 1. Executive Summary & Integration Architecture

This specification document outlines the integration contract between the **Company's Core Systems (TMS / ERP / Logistics Engine)** and the **CRM Ticketing & Performance Analytics Hub**. 

To deliver automated ticket routing, real-time docket traceability, customer service SLAs, and organizational team performance analytics, three primary data entities must be synchronized:

1. **Master Users / Employees (`CL_Master_User`)**: Powers organizational hierarchies, manager-to-subordinate reporting lines, and staff ticket resolution performance.
2. **Master Customers & Details (`CL_Master_Customer`, `CL_Master_Customer_Detail`)**: Powers customer identification, dedicated executive/KAM ticket assignments, and customer-specific SLA dashboards.
3. **Consignments / Dockets (`CL_Docket`)**: Enables automatic consignment lookups, origin/destination tracking, and delivery issue ticketing.

```
┌──────────────────────────────┐                 ┌──────────────────────────────┐
│  Company TMS / ERP Platform  │                 │     Logics CRM Platform      │
│  (Users, Dockets, Customers) │                 │      Ticketing Hub & DB      │
└──────────────┬───────────────┘                 └──────────────▲───────────────┘
               │                                                │
               │   Approach 1: Push Webhook Sync (Recommended)   │
               ├────────────────────────────────────────────────┤
               │   POST /api/external/sync/user                 │
               │   POST /api/external/sync/customer             │
               │   POST /api/external/sync/docket               │
               │   POST /api/external/sync/all (Batch)          │
               │                                                │
               │   Approach 2: Scheduled Polling Ingestion      │
               │   CRM calls Company TMS Endpoints              │
               └────────────────────────────────────────────────┘
```

### Supported Integration Approaches:
- **Approach 1 (Push / Webhook Model - Recommended)**: Whenever an employee joins/changes role, a customer account is created/updated, or a docket is booked in the Company TMS, the TMS invokes our REST Sync APIs.
- **Approach 2 (Pull / Scheduled Polling Model)**: If the Company prefers providing outbound REST APIs, the Company will implement the endpoints specified in **Section 4**, and our background worker will poll them incrementally.

---

## 2. Security & Authentication

All requests to the Synchronization API must provide valid authentication credentials configured in `appsettings.json`. The API supports four flexible authentication mechanisms:

### Method 1: Payload Credentials (Recommended for JSON Webhooks)
Include `username` and `password` properties directly in the JSON root:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  ... data fields ...
}
```

### Method 2: Custom HTTP Headers
Include the following request headers:
```http
X-Username: cjdarcl_sync_service
X-Password: CJDarcl@Sync#2026!Secure
```

### Method 3: HTTP Basic Authentication
Include standard Basic Auth in the `Authorization` header:
```http
Authorization: Basic Y2pkYXJjbF9zeW5jX3NlcnZpY2U6Q0pEYXJjbEBTeW5jIzIwMjYhU2VjdXJl
```
*(Base64 encoding of `cjdarcl_sync_service:CJDarcl@Sync#2026!Secure`)*

### Method 4: Query String Parameters (Testing only)
```http
POST /api/external/sync/customer?username=cjdarcl_sync_service&password=CJDarcl@Sync#2026!Secure
```

---

## 3. Incoming Sync API Specifications (CRM Endpoints)

All endpoints perform **intelligent upserts (INSERT or UPDATE)**. If an entity with the given primary key exists, it is updated; otherwise, a new record is created. It is completely safe to resend records.

---

### 3.1 User / Staff Master Synchronization

Manages organizational employees, hub executives, field officers, and team managers.

#### A. Single User Sync
- **Endpoint:** `POST /api/external/sync/user`
- **Method:** `POST`
- **Content-Type:** `application/json`

#### Field Dictionary:
| Field | Type | Required | Description | Example |
| :--- | :---: | :---: | :--- | :--- |
| `userId` | Integer | **Yes** | Unique Employee ID in Company TMS | `5` |
| `name` | String | **Yes** | Full Name of the employee | `"BIMLESH TIWARI"` |
| `email` | String | No | Official company email address | `"bimlesh.tiwari@cjdarcl.com"` |
| `mobileNo` | String | No | 10-digit primary contact number | `"9876543210"` |
| `managerId` | Integer | No | `userId` of the employee's reporting manager | `1` (or `null` for HOD/Directors) |
| `role` | String | No | Designation or operational role | `"HUB MANAGER"` |
| `userNumber` | String | No | Employee code / badge identifier | `"EMP-00542"` |
| `isActive` | Boolean | No | Active employment status (Default: `true`) | `true` |

> [!IMPORTANT]
> **Hierarchy Linking (`managerId`)**: The `managerId` field is critical for the CRM's **Team Hierarchy & Resolution Analytics**. It maps direct reports to team managers so executive dashboards can calculate team ticket resolution metrics.

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "userId": 5,
  "name": "BIMLESH TIWARI",
  "email": "dist_bengaluru_hub@cjdarcl.com",
  "mobileNo": "7899086621",
  "managerId": 1,
  "role": "HUB MANAGER",
  "userNumber": "CJ-BLR-05",
  "isActive": true
}
```

#### Response Sample (200 OK):
```json
{
  "success": true,
  "message": "User 'BIMLESH TIWARI' (ID 5) synced successfully."
}
```

---

#### B. Batch Users Sync
- **Endpoint:** `POST /api/external/sync/users/batch`
- **Method:** `POST`

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "users": [
    {
      "userId": 5,
      "name": "BIMLESH TIWARI",
      "email": "bimlesh.t@cjdarcl.com",
      "mobileNo": "7899086621",
      "managerId": 1,
      "role": "HUB MANAGER",
      "isActive": true
    },
    {
      "userId": 3,
      "name": "SATISH",
      "email": "satish.ops@cjdarcl.com",
      "mobileNo": "9845012345",
      "managerId": 5,
      "role": "FIELD OFFICER",
      "isActive": true
    },
    {
      "userId": 4,
      "name": "DEEPAK",
      "email": "deepak.hub@cjdarcl.com",
      "mobileNo": "9845098765",
      "managerId": 5,
      "role": "HUB EXECUTIVE -V1",
      "isActive": true
    }
  ]
}
```

#### Response Sample (200 OK):
```json
{
  "success": true,
  "message": "Processed 3 records. Success: 3, Failures: 0",
  "totalProcessed": 3,
  "successCount": 3,
  "failureCount": 0,
  "errors": []
}
```

---

### 3.2 Customer Master Synchronization

Synchronizes corporate accounts, billing customers, and assigns dedicated Key Account Managers (Executive ID).

#### A. Single Customer Sync
- **Endpoint:** `POST /api/external/sync/customer`
- **Method:** `POST`

#### Field Dictionary:
| Field | Type | Required | Description | Example |
| :--- | :---: | :---: | :--- | :--- |
| `customerId` | Integer | **Yes** | Master Customer ID in Company TMS | `102` |
| `customerName` | String | **Yes** | Registered Company / Account Name | `"PUMA SPORTS INDIA PVT LTD"` |
| `customerNumber` | String | No | Unique Customer Account Code | `"CUST-PUMA-01"` |
| `email` | String | No | Primary operational / billing contact email | `"logistics@puma.com"` |
| `mobileNo` | String | No | Contact telephone or mobile | `"080-45678900"` |
| `branchCode` | String | No | Associated serving hub / branch location code | `"BNG"` |
| `executiveId` | Integer | No | Assigned Key Account Manager / Executive (`userId`) | `5` |
| `isActive` | Boolean | No | Active client status (Default: `true`) | `true` |

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "customerId": 102,
  "customerName": "PUMA SPORTS INDIA PVT LTD",
  "customerNumber": "CUST-PUMA-01",
  "email": "support@puma.com",
  "mobileNo": "9812345678",
  "branchCode": "BNG",
  "executiveId": 5,
  "isActive": true
}
```

#### Response Sample (200 OK):
```json
{
  "success": true,
  "message": "Customer 'PUMA SPORTS INDIA PVT LTD' (ID 102) synced successfully."
}
```

---

#### B. Batch Customers Sync
- **Endpoint:** `POST /api/external/sync/customers/batch`
- **Method:** `POST`

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "customers": [
    {
      "customerId": 101,
      "customerName": "ADITYA BIRLA FASHION AND RETAIL LIMITED",
      "customerNumber": "CUST-ABFRL-01",
      "email": "dispatch@abfrl.com",
      "branchCode": "MUM",
      "executiveId": 5
    },
    {
      "customerId": 102,
      "customerName": "PUMA SPORTS INDIA PVT LTD",
      "customerNumber": "CUST-PUMA-01",
      "email": "support@puma.com",
      "branchCode": "BNG",
      "executiveId": 3
    }
  ]
}
```

---

### 3.3 Consignment / Docket Synchronization

Synchronizes waybill / docket records to enable real-time consignment lookup in the CRM.

#### A. Single Docket Sync
- **Endpoint:** `POST /api/external/sync/docket`
- **Method:** `POST`

#### Field Dictionary:
| Field | Type | Required | Description | Example |
| :--- | :---: | :---: | :--- | :--- |
| `docketNo` | String | **Yes** | Unique Docket / Consignment Note Number | `"DKT-2026-9901"` |
| `docketId` | Integer | No | Internal database identity key | `15402` |
| `bookingDate` | DateTime (ISO 8601) | No | Date and time the consignment was booked | `"2026-09-06T10:30:00Z"` |
| `origin` | String | No | Origin Hub / City code | `"BENGALURU"` |
| `destination` | String | No | Destination Hub / City code | `"MUMBAI"` |

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "docketNo": "DKT-2026-9901",
  "docketId": 15402,
  "bookingDate": "2026-09-06T09:15:00",
  "origin": "BENGALURU",
  "destination": "DELHI"
}
```

#### Response Sample (200 OK):
```json
{
  "success": true,
  "message": "Docket 'DKT-2026-9901' synced successfully."
}
```

---

#### B. Batch Dockets Sync
- **Endpoint:** `POST /api/external/sync/dockets/batch`
- **Method:** `POST`

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "dockets": [
    {
      "docketNo": "DKT-2026-9901",
      "bookingDate": "2026-09-06T09:15:00",
      "origin": "BENGALURU",
      "destination": "DELHI"
    },
    {
      "docketNo": "DKT-2026-9902",
      "bookingDate": "2026-09-06T10:00:00",
      "origin": "MUMBAI",
      "destination": "CHENNAI"
    }
  ]
}
```

---

### 3.4 Unified Batch Synchronization (All Entities in One Request)

Useful for daily midnight synchronization, data migrations, or bulk updates.

- **Endpoint:** `POST /api/external/sync/all`
- **Method:** `POST`

#### Request Sample:
```json
{
  "username": "cjdarcl_sync_service",
  "password": "CJDarcl@Sync#2026!Secure",
  "users": [
    { "userId": 5, "name": "BIMLESH TIWARI", "role": "HUB MANAGER", "managerId": 1 }
  ],
  "customers": [
    { "customerId": 102, "customerName": "PUMA SPORTS INDIA PVT LTD", "branchCode": "BNG" }
  ],
  "dockets": [
    { "docketNo": "DKT-2026-9901", "origin": "BENGALURU", "destination": "DELHI" }
  ]
}
```

#### Response Sample (200 OK):
```json
{
  "success": true,
  "message": "Unified batch sync completed. Total records processed: 3",
  "totalProcessed": 3,
  "successCount": 3,
  "failureCount": 0,
  "errors": []
}
```

---

## 4. Outgoing Contract Requirements (If Company Provides APIs)

If the Company hosts its own API services that our CRM backend will consume via periodic background worker jobs, the Company's APIs should conform to the specifications below:

### 4.1 Company Users API: `GET /api/v1/users`
- **Query Parameters:**
  - `modifiedSince` (optional): ISO 8601 timestamp (e.g. `2026-09-01T00:00:00Z`). Returns only employees created or updated after this timestamp.
  - `page` (optional, default: `1`): Page number.
  - `pageSize` (optional, default: `100`): Number of records per page.
- **Expected Response Format:**
```json
{
  "page": 1,
  "pageSize": 100,
  "totalCount": 450,
  "data": [
    {
      "userId": 5,
      "employeeCode": "CJ-00542",
      "name": "BIMLESH TIWARI",
      "email": "bimlesh.t@cjdarcl.com",
      "mobileNo": "7899086621",
      "role": "HUB MANAGER",
      "managerId": 1,
      "locationCode": "BNG",
      "isActive": true,
      "lastModified": "2026-09-06T08:30:00Z"
    }
  ]
}
```

---

### 4.2 Company Customers API: `GET /api/v1/customers`
- **Query Parameters:**
  - `modifiedSince` (optional): ISO 8601 timestamp for delta updates.
  - `page` / `pageSize`: Pagination controls.
- **Expected Response Format:**
```json
{
  "page": 1,
  "pageSize": 100,
  "totalCount": 1200,
  "data": [
    {
      "customerId": 102,
      "customerCode": "CUST-PUMA-01",
      "customerName": "PUMA SPORTS INDIA PVT LTD",
      "email": "support@puma.com",
      "mobileNo": "9812345678",
      "branchCode": "BNG",
      "accountManagerId": 5,
      "isActive": true,
      "lastModified": "2026-09-06T08:30:00Z"
    }
  ]
}
```

---

### 4.3 Company Dockets API: `GET /api/v1/dockets`
- **Query Parameters:**
  - `bookingDateFrom` (optional): Filter bookings from date (`YYYY-MM-DD`).
  - `bookingDateTo` (optional): Filter bookings to date (`YYYY-MM-DD`).
  - `docketNo` (optional): Lookup a specific single consignment.
- **Expected Response Format:**
```json
{
  "totalCount": 850,
  "data": [
    {
      "docketNo": "DKT-2026-9901",
      "docketId": 15402,
      "bookingDate": "2026-09-06T09:15:00Z",
      "origin": "BENGALURU",
      "destination": "DELHI",
      "status": "In Transit"
    }
  ]
}
```

---

## 5. HTTP Status Codes & Error Responses

| HTTP Status Code | Meaning | Cause |
| :---: | :--- | :--- |
| **`200 OK`** | Request Successful | Entity or batch has been successfully processed and upserted. |
| **`400 Bad Request`** | Validation Error | Required field missing (e.g. `docketNo`, `userId`, `customerName`) or invalid JSON format. |
| **`401 Unauthorized`** | Authentication Failure | Missing or invalid credentials (username/password or headers). |
| **`500 Internal Server Error`** | Server / Database Exception | Database connection failure or constraint violation (logged with details). |

### Error Response Schema:
```json
{
  "success": false,
  "message": "CustomerId and CustomerName are required."
}
```

---

## 6. Implementation Code Examples

### 6.1 cURL Example (Single User Sync)
```bash
curl -X POST "http://localhost:5058/api/external/sync/user" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "cjdarcl_sync_service",
    "password": "CJDarcl@Sync#2026!Secure",
    "userId": 5,
    "name": "BIMLESH TIWARI",
    "email": "bimlesh.t@cjdarcl.com",
    "mobileNo": "7899086621",
    "managerId": 1,
    "role": "HUB MANAGER",
    "isActive": true
  }'
```

---

### 6.2 C# / .NET Example (`HttpClient`)
```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class CrmSyncClient
{
    private readonly HttpClient _client = new HttpClient();
    private const string BaseUrl = "http://localhost:5058/api/external/sync";

    public async Task SyncUserAsync(int userId, string name, string role, int? managerId)
    {
        var payload = new
        {
            username = "cjdarcl_sync_service",
            password = "CJDarcl@Sync#2026!Secure",
            userId = userId,
            name = name,
            role = role,
            managerId = managerId,
            isActive = true
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{BaseUrl}/user", content);
        var result = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Sync Response: {result}");
    }
}
```

---

### 6.3 Python Example (`requests`)
```python
import requests

SYNC_URL = "http://localhost:5058/api/external/sync/docket"

payload = {
    "username": "cjdarcl_sync_service",
    "password": "CJDarcl@Sync#2026!Secure",
    "docketNo": "DKT-2026-9901",
    "bookingDate": "2026-09-06T10:00:00",
    "origin": "BENGALURU",
    "destination": "MUMBAI"
}

response = requests.post(SYNC_URL, json=payload)
print(response.status_code, response.json())
```

---

## 7. Support & Integration Contact

For technical clarifications, custom payload mappings, or staging environment credentials, please contact the CRM Engineering Team:
- **Email:** `support@empowerlogics.com` / `tech-support@cjdarcl.com`
- **Dev Portal:** `http://localhost:5058/swagger`
