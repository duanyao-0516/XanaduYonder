#!/usr/bin/env python3
import csv,collections,json,pathlib,argparse
p=argparse.ArgumentParser();p.add_argument('reference');p.add_argument('native');p.add_argument('--output');a=p.parse_args()
def read(path):
 rows=list(csv.reader(open(path)));return {tuple(x[1:3]):x[3:]for x in rows if x[0]=='B'},collections.Counter(tuple(x[1:3])for x in rows if x[0]=='C')
r,rc=read(a.reference);n,nc=read(a.native);names=['x','y','rotation','xs','ys','gravity'];count=collections.Counter();first={};diffrows=0
for k in sorted(r.keys()&n.keys(),key=lambda x:tuple(map(int,x))):
 different=False
 for i,(x,y)in enumerate(zip(r[k],n[k])):
  if k[1]=="2" and i==2:continue # random(6) opcode is intentionally uncontrolled, cosmetic spin only
  try:eq=abs(float(x)-float(y))<1e-9
  except ValueError:eq=x==y
  if not eq:count[names[i]]+=1;first.setdefault(names[i],{'key':k,'original':x,'native':y});different=True
 diffrows+=different
cdiff=[{'frame/id':k,'original':rc[k],'native':nc[k]}for k in sorted(rc.keys()|nc.keys(),key=lambda x:tuple(map(int,x)))if rc[k]!=nc[k]]
report={'commonRows':len(r.keys()&n.keys()),'differentRows':diffrows,'onlyOriginal':sorted(r.keys()-n.keys()),'onlyNative':sorted(n.keys()-r.keys()),'fields':dict(count),'firstByField':first,'particleCreationCountDifferences':cdiff}
s=json.dumps(report,indent=2);print(s)
if a.output:pathlib.Path(a.output).write_text(s+'\n')
raise SystemExit(1 if diffrows or r.keys()!=n.keys() or cdiff else 0)
