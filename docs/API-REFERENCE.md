# Idfy.Api reference

The request and response for every Idfy.Api endpoint.

Concepts that apply to all endpoints are covered in the
[integration guide](INTEGRATION.md): image input and limits, the response
envelope, sync vs async verification, error handling, and retries. This page
shows the exact request and response for each endpoint.

> [!NOTE]
> The response bodies on this page were captured from the running API, with
> IDfy replaced by a stub that returns sample results. The shape, field names,
> `null` handling and status codes are real. The personal data is fictional.

## Conventions

- **Base URL:** the environment's host (locally `http://localhost:5142`).
  Paths below are relative to it.
- **Requests** are JSON with camelCase fields, or `multipart/form-data` for
  `/upload` endpoints.
- **Responses** use snake_case fields. **Every field in the examples is always
  present.** A field without a value is `null` rather than missing. Fields that
  IDfy returns but that aren't shown here are not passed through.
- **`taskId` / `groupId`** are optional GUIDs on every JSON request. They're
  generated when you leave them out and are returned as `task_id` / `group_id`.
  `/upload` endpoints always generate them.
- **`/upload` variants** return exactly the same response as their JSON
  counterpart.
- **Errors** are `application/problem+json` with a `traceId`. The errors listed
  for each endpoint are the ones specific to it. Errors every endpoint can
  return (`413`, `415`, `422`, `429`, `502`, `504`, IDfy's own `400`/`422`) are
  covered in [Errors](INTEGRATION.md#errors).

## Endpoints

| Method | Route | Section |
|--------|-------|---------|
| POST | `/api/documents/validate` | [Document validation](#document-validation) |
| POST | `/api/documents/validate/upload` | [Document validation](#document-validation) |
| POST | `/api/pan/extract` | [PAN extraction](#pan-extraction) |
| POST | `/api/pan/extract/upload` | [PAN extraction](#pan-extraction) |
| POST | `/api/aadhaar/extract` | [Aadhaar extraction](#aadhaar-extraction) |
| POST | `/api/aadhaar/extract/upload` | [Aadhaar extraction](#aadhaar-extraction) |
| POST | `/api/aadhaar/mask` | [Aadhaar masking](#aadhaar-masking) |
| POST | `/api/aadhaar/mask/upload` | [Aadhaar masking](#aadhaar-masking) |
| POST | `/api/driving-license/extract` | [Driving licence extraction](#driving-licence-extraction) |
| POST | `/api/driving-license/extract/upload` | [Driving licence extraction](#driving-licence-extraction) |
| POST | `/api/driving-license/verify/sync` | [Driving licence verification](#driving-licence-verification) |
| POST | `/api/driving-license/verify` | [Driving licence verification](#driving-licence-verification) |
| GET | `/api/driving-license/verify/{requestId}` | [Driving licence verification](#driving-licence-verification) |
| POST | `/api/passport/extract` | [Passport extraction](#passport-extraction) |
| POST | `/api/passport/extract/upload` | [Passport extraction](#passport-extraction) |
| POST | `/api/passport/verify/sync` | [Passport verification](#passport-verification) |
| POST | `/api/passport/verify` | [Passport verification](#passport-verification) |
| GET | `/api/passport/verify/{requestId}` | [Passport verification](#passport-verification) |
| POST | `/api/voter-id/extract` | [Voter ID extraction](#voter-id-extraction) |
| POST | `/api/voter-id/extract/upload` | [Voter ID extraction](#voter-id-extraction) |
| POST | `/api/voter-id/verify/sync` | [Voter ID verification](#voter-id-verification) |
| POST | `/api/voter-id/verify` | [Voter ID verification](#voter-id-verification) |
| GET | `/api/voter-id/verify/{requestId}` | [Voter ID verification](#voter-id-verification) |
| POST | `/api/pan-aadhaar-link/verify/sync` | [PAN-Aadhaar link](#pan-aadhaar-link) |
| POST | `/api/pan-aadhaar-link/verify` | [PAN-Aadhaar link](#pan-aadhaar-link) |
| GET | `/api/pan-aadhaar-link/verify/{requestId}` | [PAN-Aadhaar link](#pan-aadhaar-link) |
| POST | `/api/face/compare` | [Face compare](#face-compare) |
| POST | `/api/face/compare/upload` | [Face compare](#face-compare) |
| GET | `/health` | [Health](#health) |

---

## Document validation

Checks that an image is a readable identity document. It can also detect the
document type, the side shown, a face, or a scanned copy.

### Request

`POST /api/documents/validate`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Image URL or Base64. |
| `docType` | string | no | Expected type: `ind_pan`, `ind_aadhaar`, `ind_voter_id`, `ind_driving_license` or `ind_passport`. |
| `detectDocSide` | bool | no | Detect front, back or both sides. |
| `detectFace` | bool | no | Detect a face on the document. |
| `detectScanned` | bool | no | Detect a scanned copy. |

The `detect*` flags return `501` unless the server has their IDfy key names
configured.

```http
POST /api/documents/validate
Content-Type: application/json

{
  "document": "https://example.com/pan.jpg",
  "docType": "ind_pan"
}
```

**Upload:** `POST /api/documents/validate/upload` takes the form fields `file`,
`docType`, `detectDocSide`, `detectFace` and `detectScanned`.

```sh
curl -X POST "$BASE/api/documents/validate/upload" \
  -F "file=@pan.jpg;type=image/jpeg" \
  -F "docType=ind_pan"
```

### Response: `200 OK`

```json
{
  "action": "validate",
  "type": "document",
  "status": "completed",
  "task_id": "d55fc858-a0da-48aa-9893-6dca6e21d4f9",
  "group_id": "e0dad938-62fd-4afd-a7a1-cf480ebe99f5",
  "request_id": "b4ee8fc9-10e0-4d34-90b3-f4adb9989721",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "detected_doc_side": "front",
    "detected_doc_type": "ind_pan",
    "face_details": {
      "detected": true
    },
    "is_readable": true,
    "is_scanned": false,
    "readability": {
      "confidence": 87
    }
  }
}
```

### Errors

| Status | When |
|--------|------|
| `400` | `docType` isn't one of the allowed values. |
| `501` | A `detect*` flag was requested but isn't configured on the server. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "DocType": ["DocType must be one of: ind_pan, ind_aadhaar, ind_voter_id, ind_driving_license, ind_passport."]
  },
  "traceId": "b8186855d144aed317c46dc2a5776d2f"
}
```

```json
{
  "title": "Advanced feature not configured.",
  "status": 501,
  "detail": "No IDfy key configured for: Face. Set Idfy:AdvancedFeatureKeys (key names are provided by the IDfy sales SPOC).",
  "traceId": "d7028b9181966b5eeee1ef43efb21f20"
}
```

---

## PAN extraction

Reads the details printed on a PAN card.

### Request

`POST /api/pan/extract`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Image URL or Base64. |

This example also sets the optional `taskId` and `groupId`. They come back
unchanged in the response.

```http
POST /api/pan/extract
Content-Type: application/json

{
  "document": "https://example.com/pan.jpg",
  "taskId": "8f0c6e1a-5b0e-4d5a-9a4c-0d6f3b8e2a11",
  "groupId": "1d2b7c9e-3f4a-4b6c-8d0e-2a1b3c4d5e6f"
}
```

**Upload:** `POST /api/pan/extract/upload` takes the form field `file`.

```sh
curl -X POST "$BASE/api/pan/extract/upload" -F "file=@pan.jpg;type=image/jpeg"
```

### Response: `200 OK`

```json
{
  "action": "extract",
  "type": "ind_pan",
  "status": "completed",
  "task_id": "8f0c6e1a-5b0e-4d5a-9a4c-0d6f3b8e2a11",
  "group_id": "1d2b7c9e-3f4a-4b6c-8d0e-2a1b3c4d5e6f",
  "request_id": "fadda248-e4d7-4de1-b6da-7e7a3744bea4",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "extraction_output": {
      "age": 36,
      "date_of_birth": "1990-04-12",
      "date_of_issue": "",
      "fathers_name": "RAMESH SHARMA",
      "id_number": "ABCDE1234F",
      "is_scanned": false,
      "minor": false,
      "name_on_card": "ANIL SHARMA",
      "pan_type": "Individual"
    }
  }
}
```

- `age` is a number here. In voter ID extraction it's a string.
- Dates are strings, and are `""` when they aren't printed on the card
  (`date_of_issue` above).

---

## Aadhaar extraction

Reads the details on an Aadhaar card. **The holder's consent is required.**

### Request

`POST /api/aadhaar/extract`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Image URL or Base64. |
| `consent` | bool | yes | Must be `true`. |

```http
POST /api/aadhaar/extract
Content-Type: application/json

{
  "document": "https://example.com/aadhaar.jpg",
  "consent": true
}
```

**Upload:** `POST /api/aadhaar/extract/upload` takes the form fields `file` and
`consent=true`.

```sh
curl -X POST "$BASE/api/aadhaar/extract/upload" \
  -F "file=@aadhaar.jpg;type=image/jpeg" \
  -F "consent=true"
```

### Response: `200 OK`

```json
{
  "action": "extract",
  "type": "ind_aadhaar",
  "status": "completed",
  "task_id": "6e98c599-c447-4cf3-ada4-71bd0a769034",
  "group_id": "188d1a67-938c-46f5-9bce-3f99321875bb",
  "request_id": "f248fe69-690c-471e-a3ba-f55c7c20d259",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "extraction_output": {
      "address": "45 LAKE VIEW, JAIPUR, RAJASTHAN 302001",
      "date_of_birth": "1992-08-21",
      "district": "JAIPUR",
      "fathers_name": "",
      "gender": "Female",
      "house_number": "45",
      "id_number": "XXXXXXXX9012",
      "is_scanned": false,
      "name_on_card": "PRIYA SINGH",
      "pincode": "302001",
      "state": "Rajasthan",
      "street_address": "LAKE VIEW",
      "year_of_birth": ""
    },
    "qr_output": null
  }
}
```

- `extraction_output` is read from the printed text.
- `qr_output` has the same fields, decoded from the card's QR code. It's `null`
  when there's no readable QR code.

### Errors

| Status | When |
|--------|------|
| `400` | `consent` is missing or not `true`. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Consent": ["Consent is required to process an Aadhaar document."]
  },
  "traceId": "a7583cc92a93feac03f3a65ed856dbd3"
}
```

On `/upload`, a missing consent returns a plain problem instead of an `errors`
map:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Consent must be given (consent=true) to process an Aadhaar document.",
  "traceId": "ee7907d6fd76354fffebd6b305f96c9e"
}
```

---

## Aadhaar masking

Masks the Aadhaar number on the card image and returns links to the masked and
original images. **The holder's consent is required.**

### Request

`POST /api/aadhaar/mask`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Image URL or Base64. |
| `consent` | bool | yes | Must be `true`. |
| `advancedFeatures` | object | no | Masking options as `string → bool`, forwarded to IDfy unchanged. The key names come from IDfy. |

```http
POST /api/aadhaar/mask
Content-Type: application/json

{
  "document": "https://example.com/aadhaar.jpg",
  "consent": true
}
```

**Upload:** `POST /api/aadhaar/mask/upload` takes the form fields `file` and
`consent=true`. It doesn't accept `advancedFeatures`.

```sh
curl -X POST "$BASE/api/aadhaar/mask/upload" \
  -F "file=@aadhaar.jpg;type=image/jpeg" \
  -F "consent=true"
```

### Response: `200 OK`

```json
{
  "action": "mask",
  "type": "ind_aadhaar",
  "status": "completed",
  "task_id": "91efab70-6525-4984-8533-b9e3e773424a",
  "group_id": "e99bbaf9-9b2c-4a19-ae0c-e2fa5df1bfa5",
  "request_id": "56dce661-1e48-43f7-9f31-8979ca7ecc05",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "document_url": "https://storage.idfy.com/masked/3f1c.jpg?signature=...",
    "id_number_found": true,
    "original_document_url": "https://storage.idfy.com/original/3f1c.jpg?signature=...",
    "self_link": "https://eve.idfy.com/v3/tasks?request_id=..."
  }
}
```

- `document_url` is a signed link to the **masked** image.
- `original_document_url` links to the **unmasked** image. Treat it as
  sensitive.
- `id_number_found: false` means no Aadhaar number was found, so nothing was
  masked.

### Errors

Same consent errors as [Aadhaar extraction](#aadhaar-extraction).

---

## Driving licence extraction

Reads the details printed on a driving licence.

### Request

`POST /api/driving-license/extract`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Image URL or Base64. |

```http
POST /api/driving-license/extract
Content-Type: application/json

{
  "document": "https://example.com/dl.jpg"
}
```

**Upload:** `POST /api/driving-license/extract/upload` takes the form field
`file`.

```sh
curl -X POST "$BASE/api/driving-license/extract/upload" -F "file=@dl.jpg;type=image/jpeg"
```

### Response: `200 OK`

```json
{
  "action": "extract",
  "type": "ind_driving_license",
  "status": "completed",
  "task_id": "3166b67f-1865-4b36-80ca-3fac1c4995aa",
  "group_id": "8eacc351-75ea-4066-9699-72dabbea93e6",
  "request_id": "77b73e6d-63a3-461e-b02c-214ab703e935",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "extraction_output": {
      "address": "12 MG ROAD, PATNA, BIHAR 800001",
      "date_of_birth": "1985-02-15",
      "date_of_validity": "2035-02-14",
      "district": "PATNA",
      "fathers_name": "SURESH KUMAR",
      "id_number": "BR0120150052869",
      "is_scanned": false,
      "issue_dates": {
        "LMV": "2015-06-10",
        "MCWG": "2015-06-10"
      },
      "name_on_card": "RAHUL KUMAR",
      "pincode": "800001",
      "state": "Bihar",
      "street_address": "12 MG ROAD",
      "type": [
        "LMV",
        "MCWG"
      ],
      "validity": {
        "non_transport": "2035-02-14",
        "transport": ""
      }
    }
  }
}
```

`type` lists the vehicle classes on the licence. `issue_dates` and `validity`
are objects whose keys come from IDfy (vehicle class and licence category), so
read them as maps rather than fixed fields.

---

## Driving licence verification

Checks a licence number against the government source. The same
request works for the sync call and the async submit.

### Request

`POST /api/driving-license/verify/sync` (sync), or
`POST /api/driving-license/verify` (async submit)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `idNumber` | string | yes | Licence number, 5–32 characters. |
| `dateOfBirth` | string | yes | `YYYY-MM-DD`. |
| `stateInfo` | bool | no | Also return the issuing `state`. |
| `ageInfo` | bool | no | Also return `is_minor`. |

```http
POST /api/driving-license/verify/sync
Content-Type: application/json

{
  "idNumber": "BR0120150052869",
  "dateOfBirth": "1985-02-15",
  "stateInfo": true,
  "ageInfo": true
}
```

### Response (sync): `200 OK`

```json
{
  "action": "verify_with_source",
  "type": "ind_driving_license",
  "status": "completed",
  "task_id": "ec798df1-eb5a-445d-8ee0-4bd69f8a8c20",
  "group_id": "942d1208-8f3c-495c-a6a8-f2177c59c861",
  "request_id": "871cfccd-155b-42d0-9e00-5ac034cbf603",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "source_output": {
      "address": "12 MG ROAD, PATNA, BIHAR 800001",
      "badge_details": null,
      "card_serial_no": "BR01/2015/0052869",
      "city": "PATNA",
      "cov_details": [
        {
          "category": "NT",
          "cov": "LMV",
          "issue_date": "2015-06-10"
        },
        {
          "category": "NT",
          "cov": "MCWG",
          "issue_date": "2015-06-10"
        }
      ],
      "date_of_issue": "2015-06-10",
      "date_of_last_transaction": "2023-02-01",
      "dl_status": "Active",
      "dob": "1985-02-15",
      "face_image": "<base64 photo>",
      "gender": "Male",
      "hazardous_valid_till": null,
      "hill_valid_till": null,
      "id_number": "BR0120150052869",
      "issuing_rto_name": "PATNA, BIHAR",
      "last_transacted_at": "PATNA, BIHAR",
      "name": "RAHUL KUMAR",
      "nt_validity_from": "2015-06-10",
      "nt_validity_to": "2035-02-14",
      "relatives_name": "SURESH KUMAR",
      "source": "SARATHI",
      "status": "id_found",
      "t_validity_from": null,
      "t_validity_to": null,
      "state": "Bihar",
      "is_minor": false
    }
  }
}
```

- `state` and `is_minor` have values only when you request them with
  `stateInfo` and `ageInfo`. Otherwise they're `null`.
- `nt_*` fields are non-transport validity. `t_*` fields are transport
  validity.
- `face_image` is the photo on record.

### Async submit: `200 OK`

`POST /api/driving-license/verify` with the same body returns right away:

```json
{
  "request_id": "393b83e1-0207-454d-98dd-1493307045f7"
}
```

### Async poll

`GET /api/driving-license/verify/{requestId}`

While IDfy is still working, the poll returns `202 Accepted`:

```json
{
  "requestId": "a957d82c-987c-4fa9-b69f-6765dc3c1035",
  "status": "processing"
}
```

When it's done, the poll returns `200 OK` with the same body as the
[sync response](#response-sync-200-ok).

> [!NOTE]
> The submit response has `request_id` (snake_case, from IDfy). The `202` has
> `requestId` (camelCase, from Idfy.Api). See
> [Sync vs async verification](INTEGRATION.md#sync-vs-async-verification) for
> polling guidance.

### Errors

| Status | When |
|--------|------|
| `400` | `idNumber` is missing or has the wrong length, or `dateOfBirth` is missing. |
| `400` (no `detail`) | `dateOfBirth` isn't in `YYYY-MM-DD` format. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "IdNumber": ["The field IdNumber must be a string with a minimum length of 5 and a maximum length of 32."]
  },
  "traceId": "41c1467c09268d3ce3dec3858a745b10"
}
```

---

## Passport extraction

Reads the details on a passport's front page, and optionally its back page.

### Request

`POST /api/passport/extract`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Front page: image URL or Base64. |
| `document2` | string | no | Back page: image URL or Base64. |

```http
POST /api/passport/extract
Content-Type: application/json

{
  "document": "https://example.com/passport-front.jpg",
  "document2": "https://example.com/passport-back.jpg"
}
```

**Upload:** `POST /api/passport/extract/upload` takes the form fields `file`
(front) and an optional `file2` (back).

```sh
curl -X POST "$BASE/api/passport/extract/upload" \
  -F "file=@front.jpg;type=image/jpeg" \
  -F "file2=@back.jpg;type=image/jpeg"
```

### Response: `200 OK`

```json
{
  "action": "extract",
  "type": "ind_passport",
  "status": "completed",
  "task_id": "16a2e64f-aa14-493a-83e1-4468d288ab0c",
  "group_id": "84fbe4f8-4451-4428-81d5-51f29bf8402b",
  "request_id": "b90f2158-0c08-4e66-a65b-64a75d90f145",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "extraction_output": {
      "address": "22 PARK STREET, KOLKATA, WEST BENGAL",
      "date_of_birth": "1988-11-03",
      "date_of_expiry": "2031-07-18",
      "date_of_issue": "2021-07-19",
      "district": "KOLKATA",
      "fathers_name": "AMIT DAS",
      "file_number": "CA1234567890123",
      "first_name": "NEHA",
      "gender": "Female",
      "id_number": "K1234567",
      "is_scanned": false,
      "last_name": "DAS",
      "mothers_name": "RITA DAS",
      "name_of_spouse": "",
      "name_on_card": "NEHA DAS",
      "nationality": "INDIAN",
      "pincode": "700016",
      "place_of_birth": "KOLKATA",
      "place_of_issue": "KOLKATA",
      "state": "West Bengal"
    }
  }
}
```

The address, parents' names, spouse and `file_number` are printed on the back
page. Send `document2` if you need them.

---

## Passport verification

Checks a passport file number against the government source.

### Request

`POST /api/passport/verify/sync` (sync), or `POST /api/passport/verify`
(async submit)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `passportFileNumber` | string | yes | Passport **file** number (not the passport number), 5–32 characters. |
| `dateOfBirth` | string | yes | `YYYY-MM-DD`. |

```http
POST /api/passport/verify/sync
Content-Type: application/json

{
  "passportFileNumber": "AB1234567890",
  "dateOfBirth": "1985-02-15"
}
```

### Response (sync): `200 OK`

```json
{
  "action": "verify_with_source",
  "type": "ind_passport",
  "status": "completed",
  "task_id": "3bc194e6-674f-40fd-9192-ef5e6b4a1a14",
  "group_id": "d7563ff0-f0f5-4d62-b0c2-200d1254f7ee",
  "request_id": "89019fd0-0657-4808-abfd-45fda3a2a9b1",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "source_output": {
      "application_date": "2021-06-02",
      "date_of_birth": "1985-02-15",
      "file_number": "AB1234567890",
      "name": "RAHUL",
      "passport_status": "Passport K1234567 dispatched on 2021-07-25.",
      "status": "id_found",
      "surname": "KUMAR"
    }
  }
}
```

`passport_status` is free text from the source. It can contain the passport
number and a postal tracking number.

### Async submit and poll

These work the same way as
[driving licence verification](#async-submit-200-ok):

- `POST /api/passport/verify` returns `{"request_id": "..."}`.
- `GET /api/passport/verify/{requestId}` returns `202` while processing, then
  `200` with the sync response body.

### Errors

| Status | When |
|--------|------|
| `400` | `passportFileNumber` is missing or has the wrong length, or `dateOfBirth` is missing. |
| `400` (no `detail`) | `dateOfBirth` isn't in `YYYY-MM-DD` format. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "DateOfBirth": ["The DateOfBirth field is required."]
  },
  "traceId": "a80fc3b85d1dd61d94b1052f1d7159e2"
}
```

---

## Voter ID extraction

Reads the details on a voter ID (EPIC) card, from the front side and
optionally the back side.

### Request

`POST /api/voter-id/extract`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | Front side: image URL or Base64. |
| `document2` | string | no | Back side: image URL or Base64. |

```http
POST /api/voter-id/extract
Content-Type: application/json

{
  "document": "https://example.com/voter-front.jpg",
  "document2": "https://example.com/voter-back.jpg"
}
```

**Upload:** `POST /api/voter-id/extract/upload` takes the form fields `file`
(front) and an optional `file2` (back).

```sh
curl -X POST "$BASE/api/voter-id/extract/upload" \
  -F "file=@voter-front.jpg;type=image/jpeg" \
  -F "file2=@voter-back.jpg;type=image/jpeg"
```

### Response: `200 OK`

The values in this example come from IDfy's published sample.

```json
{
  "action": "extract",
  "type": "ind_voter_id",
  "status": "completed",
  "task_id": "a6447b21-175f-41ed-b903-488202cc3764",
  "group_id": "e94c8680-844a-4e4c-b404-922bfe689b19",
  "request_id": "a88c3852-05c7-4a67-acd7-3f76ba16b3a2",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "extraction_output": {
      "address": "ABC DEF",
      "age": "28",
      "date_of_birth": "1996-01-01",
      "district": "NAGAUR",
      "fathers_name": "ABC DEF",
      "gender": "Male",
      "house_number": "713",
      "id_number": "T*****0275",
      "is_scanned": false,
      "name_on_card": "ABC DEF",
      "pincode": "341001",
      "state": "Rajasthan",
      "street_address": "ABC DEF",
      "year_of_birth": ""
    }
  }
}
```

- `age` is a **string** here, unlike PAN's number.
- IDfy may return `id_number` partly masked.

---

## Voter ID verification

Checks an EPIC number against the government source.

### Request

`POST /api/voter-id/verify/sync` (sync), or `POST /api/voter-id/verify`
(async submit)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `idNumber` | string | yes | EPIC number, 5–20 characters. |

```http
POST /api/voter-id/verify/sync
Content-Type: application/json

{
  "idNumber": "ABC1234567"
}
```

### Response (sync): `200 OK`

```json
{
  "action": "verify_with_source",
  "type": "ind_voter_id",
  "status": "completed",
  "task_id": "2bda26fe-e686-4b8d-83cc-b1a508334ef3",
  "group_id": "95ff61e6-c2a6-484a-a4ec-e9a9056be302",
  "request_id": "30dcd1a2-8c85-48d9-8def-2986e5cca561",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "match_output": null,
    "source_output": {
      "ac_no": "112",
      "date_of_birth": "",
      "district": "NAGAUR",
      "gender": "M",
      "house_no": "713",
      "id_number": "ABC1234567",
      "last_update": "2025-11-20",
      "name_on_card": "VIKRAM SINGH",
      "part_no": "45",
      "ps_lat_long": "27.2,73.7",
      "ps_name": "GOVT SCHOOL, NAGAUR",
      "rln_name": "MAHENDRA SINGH",
      "section_no": "3",
      "source": "NVSP",
      "st_code": "S20",
      "state": "Rajasthan",
      "status": "id_found"
    }
  }
}
```

- `rln_name` is the relative's name.
- `ac_no`, `part_no` and `section_no` identify the electoral roll entry.
- `ps_name` and `ps_lat_long` identify the polling station.
- `match_output` holds IDfy's name-match score (`name_on_card`, an int) when
  IDfy returns one. Otherwise it's `null`.

### Async submit and poll

These work the same way as
[driving licence verification](#async-submit-200-ok):

- `POST /api/voter-id/verify` returns `{"request_id": "..."}`.
- `GET /api/voter-id/verify/{requestId}` returns `202` while processing, then
  `200` with the sync response body.

### Errors

| Status | When |
|--------|------|
| `400` | `idNumber` is missing or isn't 5–20 characters. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "IdNumber": ["The field IdNumber must be a string with a minimum length of 5 and a maximum length of 20."]
  },
  "traceId": "c4631ee6a94e833ff71f0a1e63cdc714"
}
```

---

## PAN-Aadhaar link

Checks whether a PAN is linked to an Aadhaar number.

### Request

`POST /api/pan-aadhaar-link/verify/sync` (sync), or
`POST /api/pan-aadhaar-link/verify` (async submit)

| Field | Type | Required | Format |
|-------|------|----------|--------|
| `panNumber` | string | yes | `AAAAA9999A`: 5 letters, 4 digits, 1 letter. |
| `aadhaarNumber` | string | yes | 12 digits, with no spaces. |

```http
POST /api/pan-aadhaar-link/verify/sync
Content-Type: application/json

{
  "panNumber": "ABCDE1234F",
  "aadhaarNumber": "234567890123"
}
```

### Response (sync): `200 OK`

```json
{
  "action": "verify_with_source",
  "type": "pan_aadhaar_link",
  "status": "completed",
  "task_id": "507047bd-e936-473e-b756-f88d6ca37c24",
  "group_id": "b41cba0b-27fb-4db9-ad65-f9727b195f0b",
  "request_id": "674bd45c-8e0e-4f74-b568-7e7f80919c87",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "source_output": {
      "is_linked": true,
      "message": "Your PAN is linked to Aadhaar.",
      "status": "id_found"
    }
  }
}
```

Use `is_linked` for the decision. `message` is the source's text.

### Async submit and poll

These work the same way as
[driving licence verification](#async-submit-200-ok):

- `POST /api/pan-aadhaar-link/verify` returns `{"request_id": "..."}`.
- `GET /api/pan-aadhaar-link/verify/{requestId}` returns `202` while
  processing, then `200` with the sync response body.

### Errors

| Status | When |
|--------|------|
| `400` | `panNumber` or `aadhaarNumber` is missing or in the wrong format. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PanNumber": ["PanNumber must be a valid PAN (e.g. ABCDE1234F)."],
    "AadhaarNumber": ["AadhaarNumber must be 12 digits."]
  },
  "traceId": "d8cf25ab65130707c123eb6057c22365"
}
```

---

## Face compare

Compares the faces in two images, for example a selfie and a document photo.

### Request

`POST /api/face/compare`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `document` | string | yes | First image: URL or Base64. |
| `document2` | string | yes | Second image: URL or Base64. |

Both images must be **150–4,096 px** in each dimension. This limit is lower
than on the other endpoints.

```http
POST /api/face/compare
Content-Type: application/json

{
  "document": "https://example.com/selfie.jpg",
  "document2": "https://example.com/id-photo.jpg"
}
```

**Upload:** `POST /api/face/compare/upload` takes the form fields `file` and
`file2`. Both are required.

```sh
curl -X POST "$BASE/api/face/compare/upload" \
  -F "file=@selfie.jpg;type=image/jpeg" \
  -F "file2=@id-photo.jpg;type=image/jpeg"
```

### Response: `200 OK`

```json
{
  "action": "compare",
  "type": "face",
  "status": "completed",
  "task_id": "8ea61533-ee82-4ee9-8778-7f6bd7038b3c",
  "group_id": "8f22081c-2e08-48c3-8386-150d0979b519",
  "request_id": "e9b6ee39-dfff-47e6-9183-55604f7351dc",
  "created_at": "2026-09-26T11:04:51+05:30",
  "completed_at": "2026-09-26T11:04:53+05:30",
  "result": {
    "image_1": {
      "face_detected": true,
      "face_quality": "Good"
    },
    "image_2": {
      "face_detected": true,
      "face_quality": "Good"
    },
    "is_a_match": true,
    "match_score": 92,
    "review_recommended": false
  }
}
```

- `is_a_match` is IDfy's decision.
- Send the case to manual review when `review_recommended` is `true`, or when
  either image has `face_detected: false`.

### Errors

| Status | When |
|--------|------|
| `400` | The second image is missing (`document2` in JSON, `file2` on upload). |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Document2": ["The Document2 field is required."]
  },
  "traceId": "53a484368df685d0ffb5596eaa884018"
}
```

---

## Health

`GET /health`

| Status | Body (`text/plain`) | Meaning |
|--------|---------------------|---------|
| `200` | `Healthy` | Idfy.Api is up and can reach its log database. |
| `503` | `Unhealthy` | The log database is unreachable. API calls still work, but aren't logged. |

The check doesn't call IDfy, so a `200` doesn't mean IDfy is available.

---

## Upload errors (all `/upload` endpoints)

| Status | When |
|--------|------|
| `400` | No `file` part was sent. |
| `413` | The file is over about 2.25 MB. |
| `415` | The part's `Content-Type` isn't `image/*`. |
| `422` | A JPEG or PNG is outside the resolution limits. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "File": ["The File field is required."]
  },
  "traceId": "e82525927ae4a248f24568f7a40d4388"
}
```
