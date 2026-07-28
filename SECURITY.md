# Security Policy

## Supported Versions

Security fixes are made against the latest stable AvaScope release. Older releases are not supported with separate security patches.

| Version | Supported |
| --- | --- |
| Latest stable release | Yes |
| Older releases | No |

## Reporting a Vulnerability

Do not open a public issue for a suspected vulnerability.

Use [GitHub private vulnerability reporting](https://github.com/RolandUI/AvaScope/security/advisories/new). If private reporting is unavailable, email `soos.roland93@gmail.com` with the subject `[AvaScope Security]`.

Include the affected version, reproduction steps, expected impact, any known mitigation, and your preferred disclosure timeline. Reports are handled on a best-effort basis; the project does not currently offer a response-time guarantee or bug bounty.

## Security Boundaries

AvaScope deliberately loads and executes selected local Avalonia project code inside an isolated preview-host process. This is expected behavior, not a sandbox for untrusted code. Runtime bridge discovery and control are opt-in and local-only.

See the [security threat model](docs/SECURITY_THREAT_MODEL.md) for supported trust boundaries, transports, and operational guidance.
