#!/usr/bin/env python3
"""Compare observed root frame-end state; no time alignment or tolerance fitting."""
import argparse,csv,collections,json,pathlib
p=argparse.ArgumentParser();p.add_argument('reference',type=pathlib.Path);p.add_argument('native',type=pathlib.Path);p.add_argument('--output',type=pathlib.Path);a=p.parse_args()
fields={'F':['kind','frame','gold','xp','training','trainingId','techTimer','aiTimer','enemyTraining','enemyPopulation','playerHP','enemyHP'],'U':['kind','frame','uid','id','side','x','y','health','dead','scanCounter','target','enemyDistance','closestDistance','outerAnimationFrame','actionFrame']}
def read(path):
 return {(r[0],int(r[1]),int(r[2])if r[0]=='U' else 0):r for r in csv.reader(path.open())if r[0]in fields}
r,n=read(a.reference),read(a.native);counts=collections.Counter();first={};different=0
for key in sorted(r.keys()&n.keys()):
 ds=[]
 for i,(x,y)in enumerate(zip(r[key],n[key])):
  try: equal=abs(float(x)-float(y))<1e-9
  except ValueError:equal=x==y
  if not equal:
   field=key[0]+'.'+fields[key[0]][i];counts[field]+=1;first.setdefault(field,{'key':key,'original':x,'native':y});ds.append(field)
 if ds:different+=1
report={'reference':str(a.reference),'native':str(a.native),'commonRows':len(r.keys()&n.keys()),'differentRows':different,'onlyOriginalRows':len(r.keys()-n.keys()),'onlyNativeRows':len(n.keys()-r.keys()),'firstOnlyOriginal':sorted(r.keys()-n.keys())[:10],'firstOnlyNative':sorted(n.keys()-r.keys())[:10],'differencesByField':dict(counts),'firstByField':first}
s=json.dumps(report,indent=2);print(s)
if a.output:a.output.write_text(s+'\n')
raise SystemExit(0 if not different and r.keys()==n.keys() else 1)
