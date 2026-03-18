#!/usr/bin/env python3
"""
Simple web server for sample-project-2
Serves static files and handles basic routing.
"""

from http.server import HTTPServer, SimpleHTTPRequestHandler
import sys
import os


class CustomHTTPHandler(SimpleHTTPRequestHandler):
    """Custom request handler with improved logging."""

    def do_GET(self):
        """Handle GET requests."""
        print(f"[{self.client_address[0]}] GET {self.path}")
        super().do_GET()

    def log_message(self, format, *args):
        """Override default logging."""
        print(f"[HTTP] {format % args}")


def run_server(host="localhost", port=8000):
    """Start the web server."""
    server_address = (host, port)
    httpd = HTTPServer(server_address, CustomHTTPHandler)
    print(f"Server running at http://{host}:{port}/")
    print("Press Ctrl+C to stop")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nShutdown requested")
        httpd.shutdown()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8000
    run_server(port=port)
