# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Scaffold, please report it responsibly:

1. **Do not** open a public GitHub issue
2. Email security concerns to the repository maintainers
3. Include a description of the vulnerability and steps to reproduce

We will acknowledge receipt within 48 hours and provide an estimated timeline for a fix.

## Security Considerations

### Authentication
Scaffold supports Azure AD (Entra ID) authentication. The `DisableAuth` configuration flag is provided **for local development only** and must never be enabled in production deployments.

### Database Credentials
- Source database credentials entered in the UI are used at runtime and not persisted to disk
- For production use, configure credentials via Azure Key Vault using `KeyVaultSecretUri` on connection configurations
- The `Password` field on `ConnectionInfo` is marked `[NotMapped]` and is never written to the database

### Docker
The default `docker-compose.yml` uses a development SQL Server password. For production:
- Use strong, unique passwords
- Do not expose SQL Server ports publicly
- Remove `DisableAuth: "true"` from environment variables
