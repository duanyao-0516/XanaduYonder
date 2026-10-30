#!/usr/bin/env python3
"""Compare observed state. Only the uncontrolled part4 random scale is excluded."""
import csv,sys,json,math,pathlib

def read(path):
 out={}
 for row in csv.reader(open(path)):
  if not row or row[0] not in ('Q','S','B','D'):continue
  key=tuple(row[:3]) if row[0] in ('Q','B') else tuple(row[:2])
  vals=row[3:] if row[0] in ('Q','B') else row[2:]
  # Original random(20) picks part4 scale. That cosmetic integer opcode is not patched.
  if row[0]=='Q' and row[2]=='4':vals[3]='uncontrolled-scale'
  out[key]=vals
 return out

def same(a,b):
 if a==b:return True
 try:return math.isclose(float(a),float(b),rel_tol=0,abs_tol=1e-9)
 except ValueError:return False
ref=read(sys.argv[1]);native=read(sys.argv[2]);common=ref.keys()&native.keys()
diffs=[{'key':k,'reference':ref[k],'native':native[k]} for k in sorted(common) if len(ref[k])!=len(native[k]) or not all(same(a,b) for a,b in zip(ref[k],native[k]))]
report={'common':len(common),'different':len(diffs),'originalOnly':len(ref.keys()-native.keys()),'nativeOnly':len(native.keys()-ref.keys()),'firstDifferences':diffs[:20],'originalOnlyExamples':sorted(ref.keys()-native.keys())[:20],'nativeOnlyExamples':sorted(native.keys()-ref.keys())[:20]}
print(json.dumps(report,indent=2));sys.exit(bool(diffs or ref.keys()!=native.keys()))
