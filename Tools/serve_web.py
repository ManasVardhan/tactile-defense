"""Local static server for the gzip-compressed Unity WebGL build in TactileWarsWeb/.

Usage: python3 Tools/serve_web.py [port]   (default port 8765, binds 127.0.0.1)
"""
import sys
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

BUILD_DIR = Path(__file__).resolve().parent.parent / "TactileWarsWeb"
DEFAULT_PORT = 8765

TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
}

ENCODINGS = {
    ".gz": "gzip",
    ".br": "br",
}


def split_encoding(path):
    """Return (encoding or None, path without the compression suffix)."""
    for ext, enc in ENCODINGS.items():
        if path.endswith(ext):
            return enc, path[: -len(ext)]
    return None, path


class Handler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        _, base = split_encoding(str(path))
        for ext, ctype in TYPES.items():
            if base.endswith(ext):
                return ctype
        return super().guess_type(path)

    def end_headers(self):
        enc, _ = split_encoding(self.path.split("?", 1)[0])
        if enc:
            self.send_header("Content-Encoding", enc)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PORT
    if not BUILD_DIR.is_dir():
        sys.exit(f"Build folder not found: {BUILD_DIR}")
    server = ThreadingHTTPServer(("127.0.0.1", port), partial(Handler, directory=str(BUILD_DIR)))
    print(f"Serving {BUILD_DIR} at http://127.0.0.1:{port}/", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
