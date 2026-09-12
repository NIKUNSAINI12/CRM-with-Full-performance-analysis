# External API Requirement Specification Document
**Document Purpose:** Interface Requirements for Company ERP/TMS APIs to be consumed by CRM  
**Target Audience:** Company ERP / TMS Engineering & Development Team  
**Version:** 2.0 (Enhanced with Full Docket Tracking History & Customer Consignment APIs)  
**Date:** September 2026  
**Status:** Approved for Implementation  

---

## 1. Executive Summary & Objective

The CRM Ticketing & Customer Experience Platform requires integration with the Company's core logistics system (ERP / TMS) to enable real-time operational workflows:
1. **Customer 360 & Consignment Visibility**: On the **Customer Details Page**, customer service agents and Key Account Managers (KAMs) must see all active/past dockets and open an interactive **Full Journey Tracking Timeline** for any docket without switching systems.
2. **Instant Docket Lookup in Tickets**: When a complaint or service request is created against a docket number, CRM immediately pulls the shipment summary and full milestone checkpoint history.
3. **Automated Ticket Assignment & Escalation**: Routing issues to the correct operational executive, branch manager, or KAM.
4. **Team & Hierarchy Performance Metrics**: Measuring resolution times, ticket throughput, and workload across management reporting lines (`managerId`).

To achieve this, the company ERP/TMS team will develop and provide **four (4) core RESTful APIs**:
- **API 1: User / Employee Master API** (Employees, hierarchy, roles)
- **API 2: Customer Master API** (Corporate accounts, billing, KAMs)
- **API 3: Docket / Consignment Master API** (Summary, weights, status, booking info)
- **API 4: Full Docket Tracking & Milestone History API** (End-to-end journey scans, transshipments, vehicle/tripsheet info, DRS/OFD, and POD)

---

## 2. General Technical Standards & Integration Guidelines

The company's APIs must adhere to the following standard web protocols:

### 2.1 Communication & Data Format
- **Protocol:** HTTPS (TLS 1.2 or higher required).
- **Format:** All requests and responses must use `application/json; charset=utf-8`.
- **Date/Time Format:** ISO 8601 UTC standard (`YYYY-MM-DDTHH:mm:ssZ` or `YYYY-MM-DDTHH:mm:ss`), e.g., `2026-09-06T10:30:00Z`.

### 2.2 Authentication & Security
The company can provide authentication using either of the following standard methods:
- **Option A (Recommended - API Key):** An HTTP Header `X-API-Key: <secret_api_key>`.
- **Option B (Bearer Token / OAuth2):** An HTTP Header `Authorization: Bearer <jwt_token>` (with a dedicated token generation endpoint `/api/auth/token`).

### 2.3 Incremental Sync (Delta Ingestion)
To optimize network bandwidth and server performance, the CRM background worker will poll for changes periodically (e.g., every 5 to 15 minutes).
- Bulk list endpoints must support an **`updatedSince`** (or `lastModifiedDate`) query parameter.
- When passed, the API must return only records created or modified on or after that timestamp.

### 2.4 Pagination Standard
All list endpoints must support page-based pagination:
- **Request parameters:** `page` (1-indexed, default: `1`), `pageSize` (default: `100`, max allowed: `500`).
- **Response wrapper:** Must contain pagination metadata (`page`, `pageSize`, `totalRecords`, `totalPages`, and `data` array).

---

## 3. API 1: User / Employee Master API

### 3.1 Purpose & CRM Usage
- Populates the CRM User Master (`CL_Master_User`).
- Manages organizational hierarchy: who reports to whom via `managerId`.
- Used to populate ticket assignees, role-based access control, team managers, and performance drill-down analytics.

### 3.2 Recommended Endpoints
| Action | HTTP Method | Endpoint Path |
| :--- | :---: | :--- |
| Get Users List (Incremental / Batch) | `GET` | `/api/v1/users` |
| Get Single User Details | `GET` | `/api/v1/users/{userId}` |

### 3.3 Query Parameters (`GET /api/v1/users`)
| Parameter | Type | Required | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `updatedSince` | DateTime | No | `null` | Returns records created/modified on or after this ISO timestamp. |
| `branchCode` | String | No | `null` | Filters employees by specific branch/hub code (e.g. `DEL`, `MUM`). |
| `isActive` | Boolean | No | `null` | Filter by user status (`true` for active, `false` for inactive). |
| `page` | Integer | No | `1` | Current page number (1-indexed). |
| `pageSize` | Integer | No | `100` | Number of items per page (Max `500`). |

### 3.4 Response Field Requirements (`data` array item)
| Field Name | Type | Required | Description | CRM Database Target | Example |
| :--- | :---: | :---: | :--- | :--- | :--- |
| `userId` | Integer | **YES** | Unique employee identifier in ERP | `UserId` (PK) | `105` |
| `name` | String | **YES** | Full legal name of the employee | `Name` | `"BIMLESH TIWARI"` |
| `email` | String | **YES** | Official company email address | `Email` | `"bimlesh.tiwari@cjdarcl.com"` |
| `mobileNo` | String | **YES** | 10-digit primary mobile contact | `MobileNo` | `"9876543210"` |
| `userNumber` | String | No | Employee code / badge ID | `UserNumber` | `"EMP1005"` |
| `designation` | String | No | Job title / Role title | `Designation` | `"Branch Operations Lead"` |
| `branchCode` | String | No | Branch / Location code assigned | `LocationCode` | `"DEL"` |
| `managerId` | Integer | **YES** | **User ID of direct reporting manager** | `ManagerId` | `12` *(Crucial for Team Hierarchy)* |
| `role` | String | No | Role name (`Admin`, `Manager`, `Assignee`, `Agent`) | `Role` | `"Assignee"` |
| `isActive` | Boolean | **YES** | Active account flag (`true` or `false`) | `IsActive` | `true` |
| `lastUpdatedDate` | DateTime | **YES** | Last modification timestamp in ERP | `LastSyncDate` | `"2026-09-06T10:15:30Z"` |

> [!IMPORTANT]
> **Manager ID Hierarchy:** The `managerId` field is critical. It must match another valid employee's `userId`. If an employee is the top-level HOD/Director with no manager, `managerId` should be `null` or `0`.

---

## 4. API 2: Customer Master API

### 4.1 Purpose & CRM Usage
- Populates the CRM Customer Master (`CL_Master_Customer`) and servicing details (`CL_Master_Customer_Detail`).
- Links tickets to corporate customer accounts for corporate SLA monitoring.
- Powers the **Customer Details Page** in CRM, linking assigned Key Account Managers (`executiveId`) to customer accounts.

### 4.2 Recommended Endpoints
| Action | HTTP Method | Endpoint Path | Description |
| :--- | :---: | :--- | :--- |
| **Get Customers List (Incremental)** | `GET` | `/api/v1/customers` | Background sync & bulk search |
| **Get Single Customer Details** | `GET` | `/api/v1/customers/{customerCode}` | Detailed profile view |
| **Get Customer's Dockets** | `GET` | `/api/v1/customers/{customerCode}/dockets` | **Directly populates consignments on the Customer Details Page** |

### 4.3 Query Parameters (`GET /api/v1/customers`)
| Parameter | Type | Required | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `updatedSince` | DateTime | No | `null` | Returns records created/modified on or after this ISO timestamp. |
| `branchCode` | String | No | `null` | Filter by servicing branch code (e.g. `DEL`, `BOM`). |
| `search` | String | No | `null` | Search by customer code or customer name. |
| `isActive` | Boolean | No | `null` | Filter by active customer status (`true` / `false`). |
| `page` | Integer | No | `1` | Current page number (1-indexed). |
| `pageSize` | Integer | No | `100` | Number of records per page (Max `500`). |

### 4.4 Query Parameters for Customer Dockets (`GET /api/v1/customers/{customerCode}/dockets`)
| Parameter | Type | Required | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `status` | String | No | `null` | Filter consignments (`Booked`, `In Transit`, `Out For Delivery`, `Delivered`). |
| `fromDate` | DateTime | No | `null` | Consignment booking start date filter. |
| `toDate` | DateTime | No | `null` | Consignment booking end date filter. |
| `page` | Integer | No | `1` | Page number. |
| `pageSize` | Integer | No | `20` | Number of dockets per page for the Customer Details Page. |

### 4.5 Response Field Requirements (`data` array item)
| Field Name | Type | Required | Description | CRM Database Target | Example |
| :--- | :---: | :---: | :--- | :--- | :--- |
| `customerId` | Integer | **YES** | Unique Customer Primary Key ID | `CustomerId` | `240` |
| `customerCode` | String | **YES** | Unique ERP Account Code | `CustomerNumber` | `"CUST-TATA-001"` |
| `customerName` | String | **YES** | Registered Business Name | `CustomerName` | `"Tata Motors Limited"` |
| `email` | String | No | Official billing/communication email | `Email` | `"logistics@tatamotors.com"` |
| `mobileNo` | String | No | Primary contact phone number | `MobileNo` | `"9822001122"` |
| `branchCode` | String | No | Servicing Branch / Hub code | `BranchCode` | `"MUM"` |
| `executiveId` | Integer | No | **Assigned KAM / Account Manager User ID** | `ExecutiveId` | `105` *(Maps to User.userId)* |
| `groupName` | String | No | Business Group / Parent corporate | `GroupName` | `"Tata Group"` |
| `gstNo` | String | No | GSTIN Tax Registration Number | `GstTinNo` | `"27AAACT2727Q1ZW"` |
| `panNo` | String | No | Permanent Account Number (PAN) | `PanNo` | `"AAACT2727Q"` |
| `billingType` | String | No | Payment basis (`Credit`, `TBB`, `Paid`, `ToPay`) | `PayBasName` | `"Credit"` |
| `isActive` | Boolean | **YES** | Customer status (`true` for active) | `IsActive` | `true` |
| `lastUpdatedDate` | DateTime | **YES** | Last modification timestamp in ERP | `LastSyncDate` | `"2026-09-06T10:15:30Z"` |

---

## 5. API 3: Docket / Consignment Master API

### 5.1 Purpose & CRM Usage
- Populates and updates consignment records in `CL_Docket`.
- Provides instant lookup when customers or agents file a ticket against a specific consignment number.
- Supplies origin, destination, current transit status, weight, and delivery timestamps.

### 5.2 Recommended Endpoints
| Action | HTTP Method | Endpoint Path | Description |
| :--- | :---: | :--- | :--- |
| **Get Dockets List (Batch / Delta)** | `GET` | `/api/v1/dockets` | Periodic scheduled background sync |
| **Get Single Docket Summary** | `GET` | `/api/v1/dockets/{docketNo}` | Instant lookup in ticket creation forms |

### 5.3 Response Field Requirements (Docket Summary)
| Field Name | Type | Required | Description | Example |
| :--- | :---: | :---: | :--- | :--- |
| `docketNo` | String | **YES** | Unique Consignment / Docket Number | `"DKT-2026-98102"` |
| `docketId` | Integer | No | Internal ERP primary key | `10542` |
| `bookingDate` | DateTime | **YES** | Booking timestamp | `"2026-09-05T14:20:00Z"` |
| `origin` | String | **YES** | Origin Branch / City code | `"DEL"` |
| `destination` | String | **YES** | Destination Branch / City code | `"BLR"` |
| `customerCode` | String | No | Billing Customer Account Code | `"CUST-TATA-001"` |
| `consignorName` | String | No | Sender Entity Name | `"Delhi Auto Parts Hub"` |
| `consigneeName` | String | No | Receiver Entity Name | `"Bengaluru Assembly Plant"` |
| `totalPackages` | Integer | No | Total quantity of cartons/boxes | `85` |
| `actualWeight` | Decimal | No | Actual gross weight (kg) | `1250.50` |
| `chargedWeight` | Decimal | No | Billed chargeable weight (kg) | `1300.00` |
| `currentStatus` | String | **YES** | Current shipment status | `"In Transit"` |
| `currentLocation` | String | No | Last scanned hub or transit location | `"Nagpur Transshipment Hub"` |
| `edd` | DateTime | No | Estimated Delivery Date | `"2026-09-09T18:00:00Z"` |
| `deliveryDate` | DateTime | No | Actual Delivery timestamp (if delivered) | `null` |
| `lastUpdatedDate` | DateTime | **YES** | Last modification timestamp in ERP | `"2026-09-06T10:15:30Z"` |

---

## 6. API 4: Full Docket Tracking & Milestone History API (CRITICAL FOR CUSTOMER DETAILS PAGE)

### 6.1 Purpose & CRM Usage
On the **Customer Details Page**, users need to see the complete journey lifecycle and checkpoint history of any selected docket right on their screen. This includes:
- Booking / Pickup event
- Hub arrival and departure scans
- Vehicle transshipment milestones (Tripsheet / Vehicle No)
- Out for Delivery (OFD) / Delivery Run Sheet (DRS) issuance with delivery agent details
- Final Delivery with receiver name, signature, and **POD (Proof of Delivery) document URL**
- Non-delivery / exception details (e.g. consignee door locked, address untraceable)

### 6.2 Recommended Endpoints
| Action | HTTP Method | Endpoint Path | Description |
| :--- | :---: | :--- | :--- |
| **Get Full Docket Journey & Tracking History** | `GET` | `/api/v1/dockets/{docketNo}/tracking` | **Returns complete timeline of milestones, scans, and POD info** |

### 6.3 Query Parameters (`GET /api/v1/dockets/{docketNo}/tracking`)
| Parameter | Type | Required | Description |
| :--- | :---: | :---: | :--- |
| `includePod` | Boolean | No (Default: `true`) | Whether to include POD signed document / photo URLs. |

### 6.4 Response Schema: Full Tracking History Specification

The tracking response contains two major structures:
1. **`summary`**: Basic docket information, current status, origin, destination, and POD links.
2. **`checkpoints` (Array)**: Ordered chronological list of all scan events, transit hops, and delivery attempts.

#### Checkpoint Item Fields Dictionary:
| Field Name | Type | Required | Description | Example |
| :--- | :---: | :---: | :--- | :--- |
| `eventSequence` | Integer | **YES** | Chronological order index (1, 2, 3...) | `1` |
| `eventCode` | String | **YES** | Event Code (`BOOKED`, `ARRIVED_HUB`, `MANIFESTED`, `IN_TRANSIT`, `OUT_FOR_DELIVERY`, `DELIVERED`, `UNDELIVERED`) | `"IN_TRANSIT"` |
| `eventTitle` | String | **YES** | Human-readable title for UI timeline | `"Departed in Transit"` |
| `eventDateTime` | DateTime | **YES** | Date and time when scan occurred (ISO 8601) | `"2026-09-06T04:15:00Z"` |
| `locationCode` | String | **YES** | Branch or Hub code | `"NAG"` |
| `locationName` | String | **YES** | Branch or Hub descriptive name | `"Nagpur Transshipment Hub"` |
| `city` | String | No | City where scan occurred | `"Nagpur"` |
| `state` | String | No | State | `"Maharashtra"` |
| `statusDescription` | String | No | Detailed activity or scan remark | `"Loaded in Vehicle MH-31-AP-9901 for Hyderabad Hub"` |
| `vehicleNo` | String | No | Transport Vehicle / Truck registration number | `"MH-31-AP-9901"` |
| `tripsheetNo` | String | No | Tripsheet / THC / Manifest number | `"THC-DEL-NAG-8821"` |
| `drsNo` | String | No | Delivery Run Sheet number (for OFD) | `"DRS-BLR-00918"` |
| `deliveryBoyName` | String | No | Delivery Executive / Driver name | `"Suresh Verma"` |
| `deliveryBoyPhone`| String | No | Contact phone of delivery executive | `"9811223344"` |
| `scannedBy` | String | No | Username / Employee ID who scanned the docket | `"emp_nag_04"` |

#### POD & Delivery Info Structure (`podInfo`):
| Field Name | Type | Description | Example |
| :--- | :---: | :--- | :--- |
| `isDelivered` | Boolean | `true` if successfully delivered | `true` |
| `deliveryDateTime`| DateTime | Actual delivery timestamp | `"2026-09-07T16:30:00Z"` |
| `receivedBy` | String | Person who accepted the delivery | `"R. K. Raman (Store Manager)"` |
| `receiverPhone` | String | Contact number of receiver | `"9845012345"` |
| `receiverRelation`| String | Relation (`Self`, `Authorized Staff`, `Security`) | `"Store In-Charge"` |
| `podDocumentUrl` | String | Direct HTTPS link to signed POD image or PDF | `"https://cdn.company.com/pod/2026/09/DKT-98102.pdf"` |
| `podThumbnailUrl`| String | Preview thumbnail URL for POD | `"https://cdn.company.com/pod/thumb/DKT-98102.jpg"` |
| `undeliveredReason`| String | Reason if delivery attempt failed | `"Consignee premises closed on Sunday"` |

---

### 6.5 Sample Response Payload: Full Docket Tracking History

```http
GET /api/v1/dockets/DKT-2026-98102/tracking
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "docketNo": "DKT-2026-98102",
    "customerCode": "CUST-TATA-001",
    "customerName": "Tata Motors Limited",
    "origin": "DEL",
    "originName": "Delhi Main Hub",
    "destination": "BLR",
    "destinationName": "Bengaluru Central Hub",
    "bookingDate": "2026-09-05T10:30:00Z",
    "edd": "2026-09-08T18:00:00Z",
    "totalPackages": 85,
    "actualWeight": 1250.50,
    "chargedWeight": 1300.00,
    "currentStatus": "Out For Delivery",
    "currentLocation": "Bengaluru Central Hub",
    "podInfo": {
      "isDelivered": false,
      "deliveryDateTime": null,
      "receivedBy": null,
      "receiverRelation": null,
      "podDocumentUrl": null,
      "undeliveredReason": null
    },
    "checkpoints": [
      {
        "eventSequence": 1,
        "eventCode": "BOOKED",
        "eventTitle": "Consignment Booked",
        "eventDateTime": "2026-09-05T10:30:00Z",
        "locationCode": "DEL",
        "locationName": "Delhi Branch Office",
        "city": "Delhi",
        "statusDescription": "Consignment booked by Delhi Auto Parts Hub",
        "vehicleNo": null,
        "tripsheetNo": null,
        "scannedBy": "EMP012"
      },
      {
        "eventSequence": 2,
        "eventCode": "ARRIVED_HUB",
        "eventTitle": "Arrived at Origin Hub",
        "eventDateTime": "2026-09-05T16:45:00Z",
        "locationCode": "DEL_HUB",
        "locationName": "Delhi Transshipment Hub",
        "city": "Delhi",
        "statusDescription": "Inward scan completed at origin facility. Sorted to Bangalore bin.",
        "vehicleNo": "DL-01-EA-1200",
        "tripsheetNo": null,
        "scannedBy": "EMP088"
      },
      {
        "eventSequence": 3,
        "eventCode": "IN_TRANSIT",
        "eventTitle": "Departed in Linehaul Transit",
        "eventDateTime": "2026-09-05T21:00:00Z",
        "locationCode": "DEL_HUB",
        "locationName": "Delhi Transshipment Hub",
        "city": "Delhi",
        "statusDescription": "Dispatched in Linehaul vehicle via Nagpur Hub",
        "vehicleNo": "HR-55-AB-9871",
        "tripsheetNo": "THC-DEL-BLR-0941",
        "scannedBy": "EMP088"
      },
      {
        "eventSequence": 4,
        "eventCode": "ARRIVED_HUB",
        "eventTitle": "Arrived at Transshipment Hub",
        "eventDateTime": "2026-09-06T14:30:00Z",
        "locationCode": "NAG_HUB",
        "locationName": "Nagpur Transshipment Hub",
        "city": "Nagpur",
        "statusDescription": "Mid-transit scan completed. Vehicle transshipment in progress.",
        "vehicleNo": "HR-55-AB-9871",
        "tripsheetNo": "THC-DEL-BLR-0941",
        "scannedBy": "EMP240"
      },
      {
        "eventSequence": 5,
        "eventCode": "ARRIVED_HUB",
        "eventTitle": "Arrived at Destination Hub",
        "eventDateTime": "2026-09-07T08:15:00Z",
        "locationCode": "BLR_HUB",
        "locationName": "Bengaluru Central Hub",
        "city": "Bengaluru",
        "statusDescription": "Inward scan at destination facility. Assigned to Whitefield route.",
        "vehicleNo": "KA-01-MJ-5520",
        "tripsheetNo": "THC-NAG-BLR-0412",
        "scannedBy": "EMP501"
      },
      {
        "eventSequence": 6,
        "eventCode": "OUT_FOR_DELIVERY",
        "eventTitle": "Out For Delivery",
        "eventDateTime": "2026-09-07T11:00:00Z",
        "locationCode": "BLR_HUB",
        "locationName": "Bengaluru Central Hub",
        "city": "Bengaluru",
        "statusDescription": "Out for delivery on Delivery Run Sheet #DRS-BLR-8820",
        "vehicleNo": "KA-03-TR-9182",
        "drsNo": "DRS-BLR-8820",
        "deliveryBoyName": "Suresh Verma",
        "deliveryBoyPhone": "9845123456",
        "scannedBy": "EMP501"
      }
    ]
  }
}
```

---

## 7. Sample Response Payload: Customer Dockets for Customer Details Page

```http
GET /api/v1/customers/CUST-TATA-001/dockets?page=1&pageSize=3
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "customerCode": "CUST-TATA-001",
  "customerName": "Tata Motors Limited",
  "page": 1,
  "pageSize": 3,
  "totalDockets": 1420,
  "totalPages": 474,
  "data": [
    {
      "docketNo": "DKT-2026-98102",
      "bookingDate": "2026-09-05T10:30:00Z",
      "origin": "DEL",
      "destination": "BLR",
      "consigneeName": "Bengaluru Assembly Plant",
      "totalPackages": 85,
      "chargedWeight": 1300.00,
      "currentStatus": "Out For Delivery",
      "currentLocation": "Bengaluru Central Hub",
      "edd": "2026-09-08T18:00:00Z",
      "hasTrackingHistory": true,
      "trackingUrl": "/api/v1/dockets/DKT-2026-98102/tracking"
    },
    {
      "docketNo": "DKT-2026-97990",
      "bookingDate": "2026-09-04T12:00:00Z",
      "origin": "PUN",
      "destination": "DEL",
      "consigneeName": "Delhi Component Depot",
      "totalPackages": 24,
      "chargedWeight": 450.00,
      "currentStatus": "Delivered",
      "currentLocation": "Delhi Hub",
      "edd": "2026-09-06T18:00:00Z",
      "deliveryDate": "2026-09-06T15:20:00Z",
      "hasTrackingHistory": true,
      "trackingUrl": "/api/v1/dockets/DKT-2026-97990/tracking"
    }
  ]
}
```

---

## 8. Standard Error Response Format

When an error occurs, the company's API should return appropriate HTTP status codes accompanied by a clean JSON payload:

```json
{
  "success": false,
  "errorCode": "DOCKET_NOT_FOUND",
  "message": "Docket 'DKT-999999' does not exist in the logistics ERP system.",
  "timestamp": "2026-09-06T10:35:00Z"
}
```

### Standard Status Codes:
- `200 OK`: Request was processed successfully.
- `400 Bad Request`: Missing mandatory parameters or invalid format.
- `401 Unauthorized`: Missing or expired API Key / Bearer Token.
- `404 Not Found`: Docket number or Customer code not found.
- `500 Internal Server Error`: Backend database or processing exception.

---

## 9. Deliverables Requested from the Company Engineering Team

To finalize the integration and connect the CRM Customer Details & Ticketing screens:
1. **Staging / Sandbox API Base URL** (e.g., `https://staging-api.cjdarcl.com/api/v1`).
2. **Authentication Credentials** (API Key or Bearer Token credentials).
3. **Swagger (OpenAPI) documentation or Postman Collection** containing sample calls for:
   - `GET /api/v1/users`
   - `GET /api/v1/customers`
   - `GET /api/v1/customers/{customerCode}/dockets`
   - `GET /api/v1/dockets/{docketNo}`
   - `GET /api/v1/dockets/{docketNo}/tracking`
4. **Target Deployment Date** for staging environment availability.
