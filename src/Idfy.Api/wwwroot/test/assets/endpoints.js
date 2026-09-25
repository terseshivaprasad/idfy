// Endpoint definitions for the Development-only test pages. Each page mounts one entry by id.
//
// kind:  "json"   — JSON body built from `fields`
//        "upload" — multipart/form-data built from `fields`
//        "get"    — GET with a {requestId} route value (async poll) or no input
// field types: image (URL or Base64, with a file picker), file, text, date, bool (omit/true/false),
//              select, guid, json (object)

const ids = [
  { name: "taskId", type: "guid", help: "Optional. Generated when omitted; echoed as task_id." },
  { name: "groupId", type: "guid", help: "Optional. Generated when omitted; echoed as group_id." },
];

const docTypes = ["ind_pan", "ind_aadhaar", "ind_voter_id", "ind_driving_license", "ind_passport"];

const detectFlags = [
  { name: "detectDocSide", type: "bool", help: "Detect front/back/both. Returns 501 unless configured on the server." },
  { name: "detectFace", type: "bool", help: "Detect a face. Returns 501 unless configured on the server." },
  { name: "detectScanned", type: "bool", help: "Detect a scanned copy. Returns 501 unless configured on the server." },
];

const consent = { name: "consent", type: "bool", default: "true", help: "Must be true: the holder has consented." };

function verifyTrio(group, title, path, fields, sample) {
  return [
    { id: `${group}-verify-sync`, group, title: `${title} — verify (sync)`, method: "POST", path: `${path}/verify/sync`, kind: "json",
      description: "Verifies against the source and returns the result directly (can take up to ~60 s).", fields, sample },
    { id: `${group}-verify`, group, title: `${title} — verify (async submit)`, method: "POST", path: `${path}/verify`, kind: "json",
      description: "Submits an async verification and returns a request_id to poll.", fields, sample, pollPage: `${group}-verify-poll` },
    { id: `${group}-verify-poll`, group, title: `${title} — verify (async poll)`, method: "GET", path: `${path}/verify/{requestId}`, kind: "get",
      description: "Polls an async verification: 202 while processing, 200 with the result when done.",
      fields: [{ name: "requestId", type: "text", required: true, route: true, help: "request_id from the submit response." }] },
  ];
}

export const groups = [
  ["documents", "Document validation"],
  ["pan", "PAN"],
  ["aadhaar", "Aadhaar"],
  ["driving-license", "Driving licence"],
  ["passport", "Passport"],
  ["voter-id", "Voter ID"],
  ["pan-aadhaar-link", "PAN-Aadhaar link"],
  ["face", "Face compare"],
  ["ops", "Operational"],
];

export const endpoints = [
  { id: "documents-validate", group: "documents", title: "Validate document", method: "POST", path: "/api/documents/validate", kind: "json",
    description: "Checks that an image is a readable identity document; optionally detects type, side, face or a scanned copy.",
    fields: [
      { name: "document", type: "image", required: true },
      { name: "docType", type: "select", options: docTypes, help: "Optional expected document type." },
      ...detectFlags, ...ids,
    ],
    sample: { document: "https://example.com/pan.jpg", docType: "ind_pan" } },
  { id: "documents-validate-upload", group: "documents", title: "Validate document (upload)", method: "POST", path: "/api/documents/validate/upload", kind: "upload",
    description: "Same as Validate document, with the image uploaded as a file.",
    fields: [
      { name: "file", type: "file", required: true },
      { name: "docType", type: "select", options: docTypes, help: "Optional expected document type." },
      ...detectFlags,
    ] },

  { id: "pan-extract", group: "pan", title: "PAN extraction", method: "POST", path: "/api/pan/extract", kind: "json",
    description: "Reads the details printed on a PAN card.",
    fields: [{ name: "document", type: "image", required: true }, ...ids],
    sample: { document: "https://example.com/pan.jpg" } },
  { id: "pan-extract-upload", group: "pan", title: "PAN extraction (upload)", method: "POST", path: "/api/pan/extract/upload", kind: "upload",
    description: "Same as PAN extraction, with the image uploaded as a file.",
    fields: [{ name: "file", type: "file", required: true }] },

  { id: "aadhaar-extract", group: "aadhaar", title: "Aadhaar extraction", method: "POST", path: "/api/aadhaar/extract", kind: "json",
    description: "Reads the details on an Aadhaar card. Requires consent.",
    fields: [{ name: "document", type: "image", required: true }, consent, ...ids],
    sample: { document: "https://example.com/aadhaar.jpg", consent: true } },
  { id: "aadhaar-extract-upload", group: "aadhaar", title: "Aadhaar extraction (upload)", method: "POST", path: "/api/aadhaar/extract/upload", kind: "upload",
    description: "Same as Aadhaar extraction, with the image uploaded as a file. Requires consent.",
    fields: [{ name: "file", type: "file", required: true }, consent] },
  { id: "aadhaar-mask", group: "aadhaar", title: "Aadhaar masking", method: "POST", path: "/api/aadhaar/mask", kind: "json",
    description: "Masks the Aadhaar number in the image; returns signed URLs to the masked and original images. Requires consent.",
    fields: [
      { name: "document", type: "image", required: true }, consent,
      { name: "advancedFeatures", type: "json", help: 'Optional masking options, e.g. {"key": true}. Key names come from IDfy.' },
      ...ids,
    ],
    sample: { document: "https://example.com/aadhaar.jpg", consent: true } },
  { id: "aadhaar-mask-upload", group: "aadhaar", title: "Aadhaar masking (upload)", method: "POST", path: "/api/aadhaar/mask/upload", kind: "upload",
    description: "Same as Aadhaar masking, with the image uploaded as a file. Requires consent.",
    fields: [{ name: "file", type: "file", required: true }, consent] },

  { id: "driving-license-extract", group: "driving-license", title: "Driving licence extraction", method: "POST", path: "/api/driving-license/extract", kind: "json",
    description: "Reads the details printed on a driving licence.",
    fields: [{ name: "document", type: "image", required: true }, ...ids],
    sample: { document: "https://example.com/dl.jpg" } },
  { id: "driving-license-extract-upload", group: "driving-license", title: "Driving licence extraction (upload)", method: "POST", path: "/api/driving-license/extract/upload", kind: "upload",
    description: "Same as Driving licence extraction, with the image uploaded as a file.",
    fields: [{ name: "file", type: "file", required: true }] },
  ...verifyTrio("driving-license", "Driving licence", "/api/driving-license", [
    { name: "idNumber", type: "text", required: true, help: "Licence number, 5–32 characters." },
    { name: "dateOfBirth", type: "date", required: true, help: "YYYY-MM-DD." },
    { name: "stateInfo", type: "bool", help: "Also return the issuing state." },
    { name: "ageInfo", type: "bool", help: "Also return is_minor." },
    ...ids,
  ], { idNumber: "BR0120150052869", dateOfBirth: "1985-02-15" }),

  { id: "passport-extract", group: "passport", title: "Passport extraction", method: "POST", path: "/api/passport/extract", kind: "json",
    description: "Reads the passport's front page, and optionally the back page.",
    fields: [
      { name: "document", type: "image", required: true, label: "document (front page)" },
      { name: "document2", type: "image", label: "document2 (back page)", help: "Optional." },
      ...ids,
    ],
    sample: { document: "https://example.com/passport-front.jpg" } },
  { id: "passport-extract-upload", group: "passport", title: "Passport extraction (upload)", method: "POST", path: "/api/passport/extract/upload", kind: "upload",
    description: "Same as Passport extraction, with the pages uploaded as files.",
    fields: [
      { name: "file", type: "file", required: true, label: "file (front page)" },
      { name: "file2", type: "file", label: "file2 (back page)", help: "Optional." },
    ] },
  ...verifyTrio("passport", "Passport", "/api/passport", [
    { name: "passportFileNumber", type: "text", required: true, help: "Passport file number (not the passport number), 5–32 characters." },
    { name: "dateOfBirth", type: "date", required: true, help: "YYYY-MM-DD." },
    ...ids,
  ], { passportFileNumber: "AB1234567890", dateOfBirth: "1985-02-15" }),

  { id: "voter-id-extract", group: "voter-id", title: "Voter ID extraction", method: "POST", path: "/api/voter-id/extract", kind: "json",
    description: "Reads the voter ID card's front side, and optionally the back side.",
    fields: [
      { name: "document", type: "image", required: true, label: "document (front side)" },
      { name: "document2", type: "image", label: "document2 (back side)", help: "Optional." },
      ...ids,
    ],
    sample: { document: "https://example.com/voter-front.jpg" } },
  { id: "voter-id-extract-upload", group: "voter-id", title: "Voter ID extraction (upload)", method: "POST", path: "/api/voter-id/extract/upload", kind: "upload",
    description: "Same as Voter ID extraction, with the sides uploaded as files.",
    fields: [
      { name: "file", type: "file", required: true, label: "file (front side)" },
      { name: "file2", type: "file", label: "file2 (back side)", help: "Optional." },
    ] },
  ...verifyTrio("voter-id", "Voter ID", "/api/voter-id", [
    { name: "idNumber", type: "text", required: true, help: "EPIC number, 5–20 characters." },
    ...ids,
  ], { idNumber: "ABC1234567" }),

  ...verifyTrio("pan-aadhaar-link", "PAN-Aadhaar link", "/api/pan-aadhaar-link", [
    { name: "panNumber", type: "text", required: true, help: "AAAAA9999A: 5 letters, 4 digits, 1 letter." },
    { name: "aadhaarNumber", type: "text", required: true, help: "12 digits, no spaces." },
    ...ids,
  ], { panNumber: "ABCDE1234F", aadhaarNumber: "234567890123" }),

  { id: "face-compare", group: "face", title: "Face compare", method: "POST", path: "/api/face/compare", kind: "json",
    description: "Compares the faces in two images. Each image must be 150–4,096 px.",
    fields: [
      { name: "document", type: "image", required: true, label: "document (first image)" },
      { name: "document2", type: "image", required: true, label: "document2 (second image)" },
      ...ids,
    ],
    sample: { document: "https://example.com/selfie.jpg", document2: "https://example.com/id-photo.jpg" } },
  { id: "face-compare-upload", group: "face", title: "Face compare (upload)", method: "POST", path: "/api/face/compare/upload", kind: "upload",
    description: "Same as Face compare, with both images uploaded as files.",
    fields: [
      { name: "file", type: "file", required: true, label: "file (first image)" },
      { name: "file2", type: "file", required: true, label: "file2 (second image)" },
    ] },

  { id: "health", group: "ops", title: "Health", method: "GET", path: "/health", kind: "get", noIdfy: true,
    description: "Liveness/readiness: 200 Healthy, or 503 when the log database is unreachable. Does not call IDfy.",
    fields: [] },
];

export const byId = Object.fromEntries(endpoints.map(e => [e.id, e]));
