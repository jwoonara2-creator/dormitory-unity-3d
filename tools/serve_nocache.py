"""Static file server for the local 3D test: python serve_nocache.py <folder> <port>

Differences from `python -m http.server`:
  * Cache-Control: no-cache  -> the browser re-checks index.html / loader / data on every load, so a NEW Unity build
    (new file names) is never mixed with an OLD cached index.html ("Unknown data format" error).
  * Windows: a busy port is an error instead of silently sharing the port with another server.
"""
import functools
import http.server
import sys


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()


class Server(http.server.ThreadingHTTPServer):
    allow_reuse_address = sys.platform != 'win32'


def main():
    folder, port = sys.argv[1], int(sys.argv[2])
    handler = functools.partial(Handler, directory=folder)
    server = Server(('127.0.0.1', port), handler)
    print(f'serving {folder} on http://127.0.0.1:{port}/', flush=True)
    server.serve_forever()


if __name__ == '__main__':
    main()
