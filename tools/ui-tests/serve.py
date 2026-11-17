#!/usr/bin/env python3
"""Static file server for the UI tests: usage: serve.py <directory> <port>

Like `python -m http.server`, but pins the MIME types browsers care about. On Windows, mimetypes.guess_type()
consults HKEY_CURRENT_USER\\SOFTWARE\\Classes, and some machines have that key reassign '.js' to text/plain (seen
from other installed software) -- browsers refuse to run a <script type="module"> served with that type, which
breaks every page silently and fails nearly the whole suite with no link back to this cause.
"""
import functools
import http.server
import mimetypes
import sys

directory, port = sys.argv[1], int(sys.argv[2])
for ext, mime in (('.js', 'text/javascript'), ('.mjs', 'text/javascript'), ('.css', 'text/css'), ('.json', 'application/json')):
    mimetypes.add_type(mime, ext)

http.server.test(HandlerClass=functools.partial(http.server.SimpleHTTPRequestHandler, directory=directory), port=port, bind='127.0.0.1')
