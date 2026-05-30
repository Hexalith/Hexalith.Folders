# Tenant / Auth / ACL Layered-Authorization Decision Flow

Status: Story 7.13 consumer reference (metadata-only).

This diagram shows the contractual **layered-authorization order** every request passes through. Authority is
derived from the authenticated context and EventStore claim-transform evidence — never from request payloads,
headers, or query values. Each layer is **deny-by-default**: a failure at any layer denies the request with a
safe, metadata-only Problem Details response. See [authentication guidance](../sdk/authentication.md) for the
claim-provenance contract and the frozen S-2 OIDC parameters.

```mermaid
flowchart TD
    Request["Inbound request + bearer JWT"] --> Jwt{"JWT validation<br/>(S-2 OIDC parameters)"}
    Jwt -->|invalid| Deny["Deny (safe, metadata-only Problem Details)"]
    Jwt -->|valid| Claims{"EventStore claim transform<br/>sub · eventstore:tenant · eventstore:permission"}
    Claims -->|no authoritative tenant| Deny
    Claims -->|authoritative tenant + permission| Freshness{"Tenant-access projection freshness"}
    Freshness -->|stale / unavailable| Deny
    Freshness -->|fresh| Acl{"Folder ACL evidence"}
    Acl -->|no ACL grant| Deny
    Acl -->|granted| Validator{"EventStore validator"}
    Validator -->|reject| Deny
    Validator -->|accept| Dapr{"Dapr deny-by-default policy"}
    Dapr -->|not allowed| Deny
    Dapr -->|allowed| Allow["Allow operation"]
```

The order is fixed: **JWT validation → EventStore claim transform → tenant-access projection freshness →
folder ACL → EventStore validator → Dapr deny-by-default policy**. Client-supplied tenant or principal values
(in the payload, headers, or query string) are comparison inputs validated against `eventstore:tenant` /
`sub`; they are never authority and never short-circuit a layer.
