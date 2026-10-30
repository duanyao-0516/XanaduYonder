#!/usr/bin/env python3
"""Build production source unchanged except a copied RNG adapter for this controlled sample."""
import pathlib,subprocess,os,json,hashlib,sys
root=pathlib.Path(__file__).resolve().parents[3];projectiles='--projectiles' in sys.argv;tmp=pathlib.Path('/tmp/aow-parity-one-projectiles/native' if projectiles else '/tmp/aow-parity-one/native');tmp.mkdir(parents=True,exist_ok=True)
model=root/'Assets/Scripts/Core/AgeOfWar/One/OneModel.cs';text=model.read_text();old='uint x=_state;x^=x<<13;x^=x>>17;x^=x<<5;_state=x;return x/4294967296.0;';assert old in text
(tmp/'OneModel.cs').write_text(text.replace(old,'_state=1;return 0.1;'))
effects=root/'Assets/Scripts/Core/AgeOfWar/One/OneEffects.cs'
if projectiles:
 copied=effects.read_text();needle='        private Particle CreateParticle(double x,double y,int id,int side,double parameter)\n        {';assert needle in copied
 (tmp/'OneEffects.cs').write_text(copied.replace(needle,needle+'\n            Console.WriteLine("C,"+(_tick-1)+","+id);'))
(tmp/'Parity.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>7.3</LangVersion><ImplicitUsings>disable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="'+str(root/('Tests/AgeOfWarParity/One/Projectiles.cs' if projectiles else 'Tests/AgeOfWarParity/One/Program.cs'))+'"/><Compile Include="OneModel.cs"/><Compile Include="'+str(root/'Assets/Scripts/Core/AgeOfWar/One/*.cs')+'" Exclude="'+str(model)+(';' + str(effects) if projectiles else '')+'"/><Compile Include="'+str(root/'Assets/Scripts/Core/AgeOfWar/Shared/*.cs')+'"/>'+('<Compile Include="OneEffects.cs"/>' if projectiles else '')+'</ItemGroup></Project>')
exe=pathlib.Path(os.environ.get('DOTNET_ROOT',str(pathlib.Path.home()/'.dotnet')))/'dotnet'
r=subprocess.run([str(exe),'run','--project',str(tmp/'Parity.csproj'),'-c','Release'],text=True,capture_output=True)
if r.returncode:raise RuntimeError(r.stdout+r.stderr)
lines=[x for x in r.stdout.splitlines()if x.startswith(('F,','U,','B,','C,'))];path=tmp.parent/'native.csv';path.write_text('\n'.join(lines)+'\n');print(path,len(lines),'rows')
