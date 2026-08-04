# Approval Gates

These actions require explicit human approval before an agent proceeds. An agent must **stop and ask** when encountering any of these.

## Always Ask First

### Architecture & Design
- Changing project structure (adding/removing projects in the solution)
- Modifying solution configuration files (`.sln`, `.csproj`)
- Adding or removing NuGet package dependencies
- Changing database schema or data models
- Introducing new architectural patterns or layers

### Security & Data
- Adding or modifying authentication/authorization flows
- Storing new categories of sensitive data
- Changing CORS, security headers, or network configuration
- Adding external service integrations
- Modifying encryption or hashing implementations

### Operations
- Modifying CI/CD pipeline configuration
- Changing deployment scripts or infrastructure
- Modifying environment variable schemas
- Adding or changing rate limiting / throttling rules

### Irreversible Actions
- Deleting files or directories
- Dropping database tables or columns
- Removing public API endpoints
- Force-pushing to any branch
- Modifying `.gitignore` to track previously ignored files

## Never Do (Hard Blocks)

- Commit secrets, API keys, passwords, or tokens to version control
- Execute commands that modify production data
- Disable security controls for convenience
- Remove or weaken existing tests without explicit approval
- Push directly to `main` without a PR/review workflow
- Run `rm -rf`, `git clean -fdx`, or equivalents on user's working directory
- Install packages from unverified sources
