#!/usr/bin/env python3
"""Isolated native runner for new scenarios, without editing the Unity project."""
import pathlib,subprocess,os,sys
root=pathlib.Path(__file__).resolve().parents[3]
scenario=sys.argv[1] if len(sys.argv)>1 else 'effects-special'
if scenario not in ('effects-special','properties'):raise ValueError(scenario)
name={'effects-special':'EffectsSpecial','properties':'Properties'}[scenario]
tmp=pathlib.Path('/tmp/aow-parity-one-'+scenario+'/native');tmp.mkdir(parents=True,exist_ok=True)
core=root/'Assets/Scripts/Core/AgeOfWar/One'
for source in core.glob('*.cs'):(tmp/source.name).write_bytes(source.read_bytes())
model=tmp/'OneModel.cs';old='uint x=_state;x^=x<<13;x^=x>>17;x^=x<<5;_state=x;return x/4294967296.0;';text=model.read_text();assert old in text;model.write_text(text.replace(old,'_state=1;return 0.1;'))
if scenario=='effects-special':
 effects=tmp/'OneEffects.cs';text=effects.read_text();needle='        private Projectile CreateProjectile(double x,double y,double rotation,double damage,string id,int side)\n        {';assert needle in text
 effects.write_text(text.replace(needle,needle+'\n            Console.WriteLine("D,"+(_tick-1)+","+id+","+x+","+y);'))
(tmp/'Parity.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>7.3</LangVersion><ImplicitUsings>disable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="'+str(root/('Tests/AgeOfWarParity/One/'+name+'.cs'))+'"/><Compile Include="'+str(tmp/'*.cs')+'"/><Compile Include="'+str(root/'Assets/Scripts/Core/AgeOfWar/Shared/*.cs')+'"/></ItemGroup></Project>')
exe=pathlib.Path(os.environ.get('DOTNET_ROOT',str(pathlib.Path.home()/'.dotnet')))/'dotnet'
r=subprocess.run([str(exe),'run','--project',str(tmp/'Parity.csproj'),'-c','Release'],text=True,capture_output=True)
if r.returncode and 'FAIL ' not in r.stdout:raise RuntimeError(r.stdout+r.stderr)
path=tmp.parent/'native.csv';lines=[x for x in r.stdout.splitlines()if x.startswith(('F,','Q,','B,','S,','D,','PASS ','FAIL '))];path.write_text('\n'.join(lines)+'\n');print(path,len(lines),'rows');print('\n'.join(lines) if scenario=='properties' else '');sys.exit(r.returncode)
