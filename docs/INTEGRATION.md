# Idfy.Api integration guide

How to call Idfy.Api from another service: request formats, response shapes,
the async polling flow, errors, limits, and a reference entry for every
endpoint.

Idfy.Api is an internal wrapper around IDfy EVE v3. It checks your input,
forwards it to IDfy, and returns IDfy's task object. The details of that
object are covered under [Response envelope](#response-envelope).

- [Before you start](#before-you-start)
- [Sending images](#sending-images)
- [Response envelope](#response-envelope)
- [Sync vs async verification](#sync-vs-async-verification)
- [Errors](#errors)
- [Retries, timeouts and credits](#retries-timeouts-and-credits)
- [Endpoint reference](#endpoint-reference)
  - [Document validation](#document-validation)
  - [PAN](#pan)
  - [Aadhaar](#aadhaar)
  - [Driving licence](#driving-licence)
  - [Passport](#passport)
  - [Voter ID](#voter-id)
  - [PAN-Aadhaar link](#pan-aadhaar-link)
  - [Face compare](#face-compare)
  - [Health](#health)
- [Integration checklist](#integration-checklist)

---

## Before you start

| Topic | Details |
|-------|---------|
| **Base URL** | The environment's host, e.g. `https://idfy-api.internal`. Locally: `http://localhost:5142`. HTTP requests are redirected to HTTPS when HTTPS is configured, so use the `https://` URL. |
| **Authentication** | None. The API is only reachable from inside the network. |
| **Browser callers** | Your origin must be listed in the server's `Cors:AllowedOrigins`. Server-to-server calls are not affected by CORS. |
| **Rate limit** | 100 requests per 60 seconds per client IP by default (fixed window). Poll calls count toward the limit. Going over it returns `429`. |
| **Request bodies** | JSON (`Content-Type: application/json`) with **camelCase** fields, or `multipart/form-data` for `/upload` endpoints. |
| **Response bodies** | Successful task responses use **snake_case** fields (IDfy's format). Errors are `application/problem+json`. |
| **OpenAPI** | `GET /openapi/v1.json` is available in the Development environment only. |
| **Examples** | [`src/Idfy.Api/Idfy.Api.http`](../src/Idfy.Api/Idfy.Api.http) contains a runnable request for every endpoint. |

### JSON request rules

- **Unknown fields are rejected** with `400`. Send only the fields documented
  for the endpoint.
- **Duplicate fields are rejected** with `400`.
- **Dates** must be `YYYY-MM-DD` (e.g. `"1985-02-15"`).
- **`taskId` / `groupId`** (optional on every JSON endpoint) must be GUIDs. The
  server generates any you leave out. They are returned as `task_id` /
  `group_id`, so you can use them to match responses to your own records.
  `/upload` endpoints don't accept them and always generate new ones.

---

## Sending images

Image endpoints accept an image in one of three ways:

| Method | Field | Notes |
|--------|-------|-------|
| **Public URL** | `document` (JSON) | Must be an absolute `http://` or `https://` URL that IDfy can reach. IDfy downloads the image itself, so Idfy.Api doesn't check its size or resolution. |
| **Base64** | `document` (JSON) | Raw Base64 **without** a `data:image/...;base64,` prefix. A value with the prefix fails validation. |
| **File upload** | `file` (multipart) | Use the `/upload` variant of the endpoint. The part's `Content-Type` must be `image/*`, otherwise the API returns `415`. |

For endpoints that take a second image (passport or voter ID back side, face
compare), use `document2` in JSON or `file2` in multipart.

### Limits

Idfy.Api checks these for Base64 and uploaded images. It doesn't check images
sent as URLs.

| Limit | Value | Error |
|-------|-------|-------|
| Base64 length | 3,000,000 characters (an image of about **2.25 MB**) | `413` |
| Uploaded file size | Also limited by the Base64 cap: files over about **2.25 MB** are rejected | `413` |
| Request body | 11 MB (JSON); 10 MB (multipart) | `413` |
| Resolution: all endpoints except face compare | Width and height each **150–10,000 px** | `422` |
| Resolution: face compare | Width and height each **150–4,096 px** | `422` |

Idfy.Api checks resolution only for JPEG and PNG. It passes other formats
through, and IDfy decides whether to accept them.

---

## Response envelope

Every successful sync call, and every completed poll, returns IDfy's task
object with `200 OK`. Fields are snake_case:

```json
{
  "action": "extract",
  "type": "ind_pan",
  "status": "completed",
  "task_id": "8f0c6e1a-5b0e-4d5a-9a4c-0d6f3b8e2a11",
  "group_id": "1d2b7c9e-3f4a-4b6c-8d0e-2a1b3c4d5e6f",
  "request_id": "a957d82c-987c-4fa9-b69f-6765dc3c1035",
  "created_at": "2026-09-25T10:15:02+05:30",
  "completed_at": "2026-09-25T10:15:04+05:30",
  "result": { "...": "endpoint-specific, see the reference below" }
}
```

- Check `status` before you read `result`. IDfy reports task-level outcomes
  here, and a `200` means IDfy returned a task, not that the check passed.
- Fields with no value are returned as `null`. Date fields that IDfy extracts
  from a card are **strings**, and may be `""` when the date isn't printed on
  the card.
- Responses contain personal data (names, ID numbers, addresses, dates of
  birth). Store and log them according to your own PII rules.

---

## Sync vs async verification

The verify-with-source endpoints (driving licence, voter ID, passport and
PAN-Aadhaar link) each come in two forms:

| Form | Call | Returns |
|------|------|---------|
| **Sync** | `POST /api/{type}/verify/sync` | `200` with the task object when IDfy finishes. The call can take up to about 60 s. |
| **Async** | `POST /api/{type}/verify` | `200` with `{"request_id": "..."}` right away. |
| **Poll** | `GET /api/{type}/verify/{requestId}` | `202` while IDfy is still processing; `200` with the task object when it's done. |

Use async when you can't hold a request open for a minute, or when you're
verifying many IDs at once.

### Async flow

```text
POST /api/voter-id/verify            {"idNumber": "ABC1234567"}
  200  {"request_id": "a957d82c-987c-4fa9-b69f-6765dc3c1035"}

GET  /api/voter-id/verify/a957d82c-987c-4fa9-b69f-6765dc3c1035
  202  {"requestId": "a957d82c-987c-4fa9-b69f-6765dc3c1035", "status": "processing"}

GET  /api/voter-id/verify/a957d82c-987c-4fa9-b69f-6765dc3c1035
  200  { "status": "completed", "request_id": "...", "result": { ... } }
```

> [!NOTE]
> Field names differ between these responses. The submit response uses
> snake_case `request_id`, because it comes from IDfy. The `202` poll response
> uses camelCase `requestId`, because Idfy.Api generates it.

**Polling guidance**

- Start polling after about 2 s, then back off (for example 2 s, 4 s, 8 s,
  capped at 15 s).
- Stop after a deadline you choose, such as 5 minutes, and treat the check as
  failed.
- Every poll counts toward the per-IP [rate limit](#before-you-start).
- Poll the same type you submitted. A driving-licence `requestId` must be
  polled on `/api/driving-license/verify/{requestId}`.

---

## Errors

All errors are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem
details (`Content-Type: application/problem+json`). Every error includes a
`traceId`, which is the same value as the `TraceId` column in the server's log
tables. Quote it when you report a problem, because it links your request to
the server's logs and to the IDfy call it triggered.

### Status codes

| Status | Cause | What to do |
|--------|-------|------------|
| `400` | A field failed validation. The body has an `errors` map. | Fix the field named in `errors`. |
| `400` | A duplicate field. `detail` names the field. | Send each field once. |
| `400` | Malformed JSON, an unknown field, a wrong type, or an unparseable date. In production the body has no `detail`. | Check the body against the endpoint's field table. |
| `400` | Empty upload, or an Aadhaar upload without `consent=true`. | Fix the request. |
| `400` / `413` / `422` / `429` from IDfy | IDfy rejected the input, for example as an unreadable image, an invalid URL, or an oversized payload, or IDfy rate-limited the call. `title` is IDfy's error code. | Fix the input, or back off on `429`. |
| `413` | The Base64 image or the request body is over the [limit](#limits). | Compress or resize the image, or send a URL. |
| `415` | An uploaded file isn't an `image/*`. | Send the correct part `Content-Type`. |
| `422` | The image resolution is outside the [limits](#limits). | Resize the image. |
| `429` | The Idfy.Api per-IP rate limit was exceeded. | Back off and retry later. |
| `500` | Unexpected server error. Details are hidden. | Report the `traceId`. |
| `501` | `detectDocSide`, `detectFace` or `detectScanned` was requested but isn't configured on this server. | Leave the flag out, or ask the API owners to configure it. |
| `502` | IDfy is unreachable or failing, or there's a server-side account problem (credentials or insufficient credits). | Retry later. Report it if it persists. |
| `504` | IDfy didn't answer within the timeout (about 60 s). | See [Retries](#retries-timeouts-and-credits). |

### Examples

Validation error (`errors` keys use PascalCase field names):

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

Error returned from IDfy (`title` is IDfy's error code, `detail` is IDfy's
message):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "INVALID_URL",
  "status": 400,
  "detail": "<IDfy's message>",
  "idfyStatus": 400,
  "idfyRequestId": "b1f0c3d2-...",
  "traceId": "d8cf25ab..."
}
```

Upstream failure (a server-side problem, not caused by your request):

```json
{
  "title": "UPSTREAM_ERROR",
  "status": 502,
  "detail": "The document verification service is unavailable.",
  "idfyStatus": 403,
  "idfyRequestId": null,
  "traceId": "d8cf25ab..."
}
```

Image checks:

```json
{ "title": "Image resolution out of range.", "status": 422,
  "detail": "Image is 100x100px; width and height must each be between 150 and 10000px.", "traceId": "d8cf25ab..." }

{ "title": "Unsupported Media Type", "status": 415,
  "detail": "Uploaded file must be an image, got 'text/plain'.", "traceId": "d8cf25ab..." }
```

---

## Retries, timeouts and credits

- **Set your client timeout to at least 70 s.** Idfy.Api waits up to about 60 s
  for IDfy, and sync verify and OCR calls can take that long.
- **Idfy.Api never retries calls to IDfy.** Each call that reaches IDfy may use
  IDfy credits. Resending an extraction or verification can be charged twice.
- **Safe to retry:** `429` (after a backoff), and `502` when the detail
  indicates IDfy was unreachable.
- **Retry with care:** `504`. IDfy may still have finished the task and charged
  for it. For verify-with-source, the async flow avoids this.
- **Don't retry without changes:** `400`, `413`, `415`, `422` and `501`. The
  same request will fail again.

---

## Endpoint reference

Every JSON endpoint also accepts the optional `taskId` and `groupId` fields. The
field tables below leave them out.

### Document validation

Checks that an image is a readable identity document. It can optionally check
the document type and detect the document side, a face, or a scanned copy.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/documents/validate` | JSON |
| POST | `/api/documents/validate/upload` | multipart |

**Request**

| Field (JSON / form) | Type | Required | Notes |
|---------------------|------|----------|-------|
| `document` / `file` | string / file | yes | URL or Base64 / image file. |
| `docType` | string | no | One of `ind_pan`, `ind_aadhaar`, `ind_voter_id`, `ind_driving_license`, `ind_passport`. |
| `detectDocSide` | bool | no | Detect front, back or both sides. |
| `detectFace` | bool | no | Detect a face on the document. |
| `detectScanned` | bool | no | Detect a scanned copy. |

The `detect*` flags only work when the server has their IDfy key names
configured. If a flag isn't configured, the call returns `501`.

```http
POST /api/documents/validate
Content-Type: application/json

{ "document": "https://example.com/pan.jpg", "docType": "ind_pan", "detectFace": true }
```

**`result`**

| Field | Type |
|-------|------|
| `detected_doc_type` | string |
| `detected_doc_side` | string |
| `is_readable` | bool |
| `readability.confidence` | int |
| `face_details.detected` | bool |
| `is_scanned` | bool |

---

### PAN

OCR extraction from a PAN card image.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/pan/extract` | JSON: `document` |
| POST | `/api/pan/extract/upload` | multipart: `file` |

```http
POST /api/pan/extract
Content-Type: application/json

{ "document": "https://example.com/pan.jpg" }
```

**`result.extraction_output`**: `id_number`, `name_on_card`, `fathers_name`,
`date_of_birth`, `date_of_issue`, `age` (int), `minor` (bool), `pan_type`,
`is_scanned` (bool).

---

### Aadhaar

OCR extraction and number masking. **Every Aadhaar endpoint requires the
holder's consent.** Send `consent: true` in JSON, or `consent=true` as a form
field. Any other value, or no value, returns `400`.

#### Extract

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/aadhaar/extract` | JSON: `document`, `consent` |
| POST | `/api/aadhaar/extract/upload` | multipart: `file`, `consent` |

```http
POST /api/aadhaar/extract
Content-Type: application/json

{ "document": "https://example.com/aadhaar.jpg", "consent": true }
```

**`result`** has two blocks with the same fields: `extraction_output` (read
from the printed text) and `qr_output` (decoded from the QR code, when
present). The fields are `id_number`, `name_on_card`, `fathers_name`, `gender`,
`date_of_birth`, `year_of_birth`, `address`, `house_number`, `street_address`,
`district`, `state`, `pincode` and `is_scanned` (bool).

#### Mask

Masks the Aadhaar number on the image and returns links to the masked and
original images.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/aadhaar/mask` | JSON: `document`, `consent`, `advancedFeatures`? |
| POST | `/api/aadhaar/mask/upload` | multipart: `file`, `consent` |

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `advancedFeatures` | object of `string → bool` | no | Masking options, forwarded to IDfy unchanged. The key names come from IDfy. Not available on `/upload`. |

```http
POST /api/aadhaar/mask
Content-Type: application/json

{ "document": "https://example.com/aadhaar.jpg", "consent": true }
```

**`result`**

| Field | Type | Notes |
|-------|------|-------|
| `document_url` | string | Signed URL of the **masked** image. |
| `original_document_url` | string | Signed URL of the **original** image. Treat it as sensitive. |
| `id_number_found` | bool | `false` means no Aadhaar number was found to mask. |
| `self_link` | string | |

---

### Driving licence

OCR extraction from a licence image, plus verification against the government
source.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/driving-license/extract` | JSON: `document` |
| POST | `/api/driving-license/extract/upload` | multipart: `file` |
| POST | `/api/driving-license/verify/sync` | JSON (below) |
| POST | `/api/driving-license/verify` | JSON (below). Async submit |
| GET | `/api/driving-license/verify/{requestId}` | Poll |

**Extract `result.extraction_output`**: `id_number`, `name_on_card`,
`fathers_name`, `date_of_birth`, `date_of_validity`, `address`,
`street_address`, `district`, `state`, `pincode`, `type` (string[]),
`issue_dates` (object), `validity` (object), `is_scanned` (bool).

**Verify request**

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `idNumber` | string | yes | Licence number, 5–32 characters. |
| `dateOfBirth` | date | yes | `YYYY-MM-DD`. |
| `stateInfo` | bool | no | Also return the issuing `state`. |
| `ageInfo` | bool | no | Also return `is_minor`. |

```http
POST /api/driving-license/verify/sync
Content-Type: application/json

{ "idNumber": "BR0120150052869", "dateOfBirth": "1985-02-15", "stateInfo": true }
```

**Verify `result.source_output`**: `id_number`, `name`, `relatives_name`,
`dob`, `gender`, `address`, `city`, `dl_status`, `status`, `source`,
`issuing_rto_name`, `date_of_issue`, `date_of_last_transaction`,
`last_transacted_at`, `nt_validity_from`, `nt_validity_to`, `t_validity_from`,
`t_validity_to`, `hazardous_valid_till`, `hill_valid_till`, `badge_details`,
`card_serial_no`, `face_image`, `cov_details[]` (`category`, `cov`,
`issue_date`). `state` and `is_minor` are included only when you request them
with `stateInfo` and `ageInfo`.

---

### Passport

OCR extraction from the front page, with the back page optional, plus
verification against the source.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/passport/extract` | JSON: `document`, `document2`? |
| POST | `/api/passport/extract/upload` | multipart: `file`, `file2`? |
| POST | `/api/passport/verify/sync` | JSON (below) |
| POST | `/api/passport/verify` | JSON (below). Async submit |
| GET | `/api/passport/verify/{requestId}` | Poll |

`document2` / `file2` is the back page. It's optional, and an empty value is
treated as absent.

```http
POST /api/passport/extract
Content-Type: application/json

{ "document": "https://example.com/front.jpg", "document2": "https://example.com/back.jpg" }
```

**Extract `result.extraction_output`**: `id_number`, `file_number`,
`first_name`, `last_name`, `name_on_card`, `gender`, `date_of_birth`,
`place_of_birth`, `date_of_issue`, `date_of_expiry`, `place_of_issue`,
`nationality`, `fathers_name`, `mothers_name`, `name_of_spouse`, `address`,
`district`, `state`, `pincode`, `is_scanned` (bool).

**Verify request**

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `passportFileNumber` | string | yes | Passport **file** number (not the passport number), 5–32 characters. |
| `dateOfBirth` | date | yes | `YYYY-MM-DD`. |

```http
POST /api/passport/verify/sync
Content-Type: application/json

{ "passportFileNumber": "AB1234567890", "dateOfBirth": "1985-02-15" }
```

**Verify `result.source_output`**: `file_number`, `name`, `surname`,
`date_of_birth`, `application_date`, `passport_status`, `status`.

---

### Voter ID

OCR extraction from a voter ID card, with the back side optional, plus
verification of an EPIC number against the source.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/voter-id/extract` | JSON: `document`, `document2`? |
| POST | `/api/voter-id/extract/upload` | multipart: `file`, `file2`? |
| POST | `/api/voter-id/verify/sync` | JSON: `idNumber` |
| POST | `/api/voter-id/verify` | JSON: `idNumber`. Async submit |
| GET | `/api/voter-id/verify/{requestId}` | Poll |

#### Extract

`document2` / `file2` is the back side of the card. It's optional, and an empty
value is treated as absent.

```http
POST /api/voter-id/extract
Content-Type: application/json

{ "document": "https://example.com/voter-front.jpg", "document2": "https://example.com/voter-back.jpg" }
```

**`result.extraction_output`**: `id_number`, `name_on_card`, `fathers_name`,
`gender`, `date_of_birth`, `year_of_birth`, `age`, `address`, `house_number`,
`street_address`, `district`, `state`, `pincode`, `is_scanned` (bool).

- `age` is a **string** (e.g. `"28"`). On PAN it's a number.
- `id_number` may come back partly masked by IDfy (e.g. `"T*****0275"`).
- `year_of_birth` may be `""` when only the full date of birth is printed.

#### Verify

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `idNumber` | string | yes | EPIC number, 5–20 characters. |

```http
POST /api/voter-id/verify/sync
Content-Type: application/json

{ "idNumber": "ABC1234567" }
```

**`result.source_output`**: `id_number`, `name_on_card`, `rln_name`,
`gender`, `date_of_birth`, `house_no`, `district`, `state`, `st_code`, `ac_no`,
`part_no`, `section_no`, `ps_name`, `ps_lat_long`, `last_update`, `source`,
`status`.
**`result.match_output`**: `name_on_card` (int match score).

---

### PAN-Aadhaar link

Checks whether a PAN is linked to an Aadhaar number.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/pan-aadhaar-link/verify/sync` | JSON |
| POST | `/api/pan-aadhaar-link/verify` | JSON. Async submit |
| GET | `/api/pan-aadhaar-link/verify/{requestId}` | Poll |

| Field | Type | Required | Format |
|-------|------|----------|--------|
| `panNumber` | string | yes | `AAAAA9999A`: 5 letters, 4 digits, 1 letter. |
| `aadhaarNumber` | string | yes | 12 digits, with no spaces. |

```http
POST /api/pan-aadhaar-link/verify/sync
Content-Type: application/json

{ "panNumber": "ABCDE1234F", "aadhaarNumber": "123412341234" }
```

**`result.source_output`**: `is_linked` (bool), `status`, `message`.

---

### Face compare

Compares the faces in two images, for example a selfie and a document photo.

| Method | Route | Body |
|--------|-------|------|
| POST | `/api/face/compare` | JSON: `document`, `document2` (both required) |
| POST | `/api/face/compare/upload` | multipart: `file`, `file2` (both required) |

Both images must be **150–4,096 px** in each dimension. This limit is lower
than on the other endpoints.

```http
POST /api/face/compare
Content-Type: application/json

{ "document": "https://example.com/selfie.jpg", "document2": "https://example.com/id-photo.jpg" }
```

**`result`**

| Field | Type | Notes |
|-------|------|-------|
| `is_a_match` | bool | IDfy's match decision. |
| `match_score` | int | Similarity score. |
| `review_recommended` | bool | `true` when IDfy recommends manual review. |
| `image_1.face_detected`, `image_2.face_detected` | bool | Whether a face was found in each image. |
| `image_1.face_quality`, `image_2.face_quality` | string | Face quality in each image. |

---

### Health

| Method | Route | Response |
|--------|-------|----------|
| GET | `/health` | `200 Healthy` or `503 Unhealthy` (`text/plain`) |

The check reports whether Idfy.Api can reach its log database. It doesn't call
IDfy, so a `200` doesn't mean IDfy is available.

---

## Integration checklist

- [ ] Client timeout is at least 70 s for sync endpoints.
- [ ] JSON requests use camelCase fields and contain no extra fields.
- [ ] Response parsing reads snake_case fields and checks `status` before
      reading `result`.
- [ ] Base64 images have no `data:` prefix and are under about 2.25 MB, or the
      images are sent as URLs.
- [ ] Aadhaar calls send `consent: true` only after the holder has actually
      consented.
- [ ] Async verification reads `request_id` from the submit response and
      polls with backoff and a deadline.
- [ ] Errors are handled by status code, and `traceId` is logged for support.
- [ ] Extraction and verification calls are not retried automatically on
      `504` (to avoid being charged twice).
