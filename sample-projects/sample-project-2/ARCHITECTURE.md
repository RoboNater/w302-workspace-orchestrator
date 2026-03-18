# Architecture — Sample Project 2

## System Design

### Components

1. **Web Server** (`web-server.py`)
   - HTTP server using Python stdlib
   - Custom request handler for logging
   - Single-threaded, event-driven

2. **Static Files**
   - HTML/CSS served directly
   - No database
   - No backend processing

## Design Decisions

- **No frameworks**: Using stdlib for minimal dependencies
- **Single port**: All traffic on configurable port (default 8000)
- **Synchronous**: Simple request-response model
- **Minimal logging**: Console output only

## Performance

- Target: Development use only
- Not intended for production load
- Memory footprint: ~20MB idle

## Future

- Add SSL/TLS support
- Implement request throttling
- Add static file caching headers
