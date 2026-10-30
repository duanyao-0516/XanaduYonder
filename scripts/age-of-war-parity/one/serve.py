#!/usr/bin/env python3
"""Local-only Ruffle harness. Trace submission remains on this machine."""
import argparse,http.server,pathlib
p=argparse.ArgumentParser();p.add_argument('--directory',required=True);p.add_argument('--trace',required=True);p.add_argument('--port',type=int,default=8769);a=p.parse_args()
class Handler(http.server.SimpleHTTPRequestHandler):
 def __init__(self,*args,**kwargs):super().__init__(*args,directory=a.directory,**kwargs)
 def do_POST(self):
  if self.path!='/trace':self.send_error(404);return
  size=int(self.headers.get('Content-Length','0'))
  if size>16*1024*1024:self.send_error(413);return
  data=self.rfile.read(size);path=pathlib.Path(a.trace);path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
  self.send_response(200);self.end_headers();self.wfile.write(('Saved '+str(path)+' ('+str(len(data))+' bytes)').encode())
http.server.ThreadingHTTPServer(('127.0.0.1',a.port),Handler).serve_forever()
