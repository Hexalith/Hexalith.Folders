"""Small synthetic retained-format capsule for replay tests; never builds or runs code."""
import copy
import hashlib
import io
import json
from pathlib import Path
import struct
import tarfile
import xml.etree.ElementTree as ET

import verify_model as v


def packed(value):
    if value<128:return bytes([value])
    if value<16384:return bytes([128|(value>>8),value&255])
    return bytes([192|(value>>24),(value>>16)&255,(value>>8)&255,value&255])


def assembly(name, references=(), classes=None, resources=None, codeview=None, enum=False):
    strings=bytearray(b'\0');blobs=bytearray(b'\0');rows={};string_ids={}
    def string(value):
        if value not in string_ids:string_ids[value]=len(strings);strings.extend(value.encode()+b'\0')
        return string_ids[value]
    def blob(value):
        index=len(blobs);blobs.extend(packed(len(value))+value);return index
    def add(table,*values):rows.setdefault(table,[]).append(values);return len(rows[table])
    add(0,0,string(name+'.dll'),1,0,0)
    add(2,0,string('<Module>'),string(''),0,1,1)
    if enum:
        add(2,1,string('WorkspaceMode'),string('Hexalith.Builds.Tooling.Workspace'),0,1,1)
        for number,label in enumerate(('Source','Package')):
            field=add(4,0x8056,string(label),blob(b'\x06\x08'));add(11,8,field<<2,blob(struct.pack('<i',number)))
    if classes:
        for attr in ('Fact','Theory','InlineData'):
            ref=add(1,0,string(attr+'Attribute'),string('Xunit'));add(10,(ref<<3)|1,string('.ctor'),blob(b'\x20\0\x01'))
        for full,methods in classes.items():
            namespace,short=full.rsplit('.',1)
            add(2,1,string(short),string(namespace),0,1,len(rows.get(6,[]))+1)
            for method,parameters,data in methods:
                start=len(rows.get(8,[]))+1
                for i,param in enumerate(parameters,1):add(8,0,i,string(param))
                rid=add(6,0,0,6,string(method),blob(b'\x20'+bytes([len(parameters)])+b'\x01'+b'\x1c'*len(parameters)),start)
                add(12,rid<<5,((2 if data else 1)<<3)|3,blob(b'\x01\0\0\0'))
                for values in data:
                    raw=bytearray(b'\x01\0'+struct.pack('<I',len(values)))
                    for value in values:
                        if isinstance(value,bool):raw.extend(bytes([2,int(value)]))
                        elif isinstance(value,tuple):
                            text=b'Hexalith.Builds.Tooling.Workspace.WorkspaceMode';raw.extend(b'\x55'+packed(len(text))+text+struct.pack('<i',value[1]))
                        else:text=value.encode();raw.extend(b'\x0e'+packed(len(text))+text)
                    raw.extend(b'\0\0');add(12,rid<<5,(3<<3)|3,blob(bytes(raw)))
    add(32,0x8004,1,0,0,0,0,0,string(name),string(''))
    for reference in references:add(35,1,0,0,0,0,0,string(reference),string(''),0)
    resource_bytes=bytearray()
    for resource,value in (resources or {}).items():
        add(40,len(resource_bytes),1,string(resource),0);resource_bytes.extend(struct.pack('<I',len(value))+value)
    valid=sum(1<<table for table in rows)
    tables=bytearray(struct.pack('<IBBBBQQ',0,2,0,0,1,valid,0))
    for table in sorted(rows):tables.extend(struct.pack('<I',len(rows[table])))
    for table in sorted(rows):
        for row in rows[table]:
            for kind,value in zip(v.TABLES[table],row):tables.extend(value.to_bytes(kind if isinstance(kind,int) else 2,'little'))
    streams=[('#~',bytes(tables)),('#Strings',bytes(strings)),('#Blob',bytes(blobs)),('#GUID',b'\x11'*16)]
    version=b'v4.0.30319\0\0'
    prefix=bytearray(b'BSJB'+struct.pack('<HHII',1,1,0,len(version))+version+struct.pack('<HH',0,len(streams)))
    header_size=len(prefix)+sum(8+((len(key)+1+3)&~3) for key,_ in streams)
    offset=header_size;content=bytearray()
    for key,data in streams:
        prefix.extend(struct.pack('<II',offset,len(data))+key.encode()+b'\0');prefix.extend(b'\0'*((-len(prefix))%4))
        content.extend(data);offset+=len(data)
    metadata=bytes(prefix+content)
    section=bytearray(72)+metadata
    resource_offset=len(section);section.extend(resource_bytes)
    debug_offset=len(section)
    if codeview:
        data=b'RSDS'+b'\x22'*16+struct.pack('<I',1)+codeview.encode()+b'\0'
        section.extend(struct.pack('<IIHHIIII',0,0,0,0,2,len(data),0x2000+debug_offset+28,512+debug_offset+28));section.extend(data)
    struct.pack_into('<IHHIIIIIIII',section,0,72,2,5,0x2000+72,len(metadata),1,0,0x2000+resource_offset,len(resource_bytes),0,0)
    raw_size=(len(section)+511)&~511
    payload=bytearray(512+raw_size)
    payload[:2]=b'MZ';struct.pack_into('<I',payload,60,128);payload[128:132]=b'PE\0\0'
    struct.pack_into('<HHIIIHH',payload,132,0x14c,1,0,0,0,224,0x2102)
    optional=152;struct.pack_into('<H',payload,optional,0x10b);struct.pack_into('<I',payload,optional+92,16)
    struct.pack_into('<II',payload,optional+96+14*8,0x2000,72)
    if codeview:struct.pack_into('<II',payload,optional+96+6*8,0x2000+debug_offset,28)
    payload[376:384]=b'.text\0\0\0';struct.pack_into('<IIII',payload,384,len(section),0x2000,raw_size,512)
    payload[512:512+len(section)]=section
    return bytes(payload)


def binding(name,raw):return dict(path=name,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())


class CompletedFixture:
    def __init__(self):
        self.workspace='/private/run';self.root='/source';self.sdk='/installed/dotnet';self.documents={}
        self.result=dict(qualification='passed',protectedAuthority=copy.deepcopy(v.AUTHORITY),ordinaryStartup='pending',signedDistribution='pending',operationalAcceptance='pending',firstFailure=None,historicalEvidence=dict(status='not reused'),historicalPreservation=dict(status='verified',files=0),commands=[])
        self.put('historical-baseline.json',[])
        environment=dict(v.CONTROLS,HOME='/home/preserved',LANG='C.UTF-8',DOTNET_ROOT='/installed',DOTNET_HOST_PATH=self.sdk,PATH='/installed:/usr/bin:/bin')
        environment.update({key:self.workspace+'/'+key.lower() for key in v.PRIVATE});self.result['environment']=environment
        self.result['sdkVersion']='10.0.403'
        self.objects={}
        catalog=b'<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>'
        blob=self.git_object('blob',catalog);props=self.git_object('tree',b'100644 Directory.Packages.props\0'+bytes.fromhex(blob));tree=self.git_object('tree',b'40000 Props\0'+bytes.fromhex(props));catalog_commit=self.git_object('commit',('tree '+tree+'\n\nfixture\n').encode())
        catalog_authority=dict(revision=catalog_commit,paths={'Props/Directory.Packages.props':dict(kind='blob',oid=blob)},objects=list(self.objects.values()))
        self.objects={};refs=self.git_object('tree',b'160000 Hexalith.Builds\0'+bytes.fromhex(catalog_commit));tree=self.git_object('tree',b'40000 references\0'+bytes.fromhex(refs));platform=self.git_object('commit',('tree '+tree+'\n\nfixture\n').encode())
        platform_authority=dict(revision=platform,paths={'references/Hexalith.Builds':dict(kind='commit',oid=catalog_commit)},objects=list(self.objects.values()))
        names=['Hexalith.Builds','Hexalith.Platform','Hexalith.Dep']+['Hexalith.Fixture'+str(i) for i in range(9)]
        repos=[dict(path='.',revision='f'*40)]+[dict(path='references/'+name,revision=platform if name=='Hexalith.Platform' else catalog_commit if name=='Hexalith.Builds' else 'b'*40) for name in names]
        for row in repos[1:]:row.update(rootGitlink=row['revision'],indexedGitlink=row['revision'])
        self.result['initialRepositories']=repos;self.put('repository-baseline.json',repos)
        mapping=dict(Mode='source',Root=self.root,ActiveModule='Hexalith.Parties',Entries=[dict(Identity=name,Origin='source',Path=self.root+'/references/'+name) for name in names],HasGit=True,PlatformIdentity=dict(CheckoutCommit=platform,PlatformPath=self.root+'/references/Hexalith.Platform',CatalogCommit=catalog_commit),IdentityFindings=[])
        mapping['ContentHash']=hashlib.sha256(v.dotnet_json(mapping)).hexdigest();self.result['productionMapping']=mapping;self.put('workspace/source-mapping/mapping.json',mapping)
        self.put('git-authority.json',dict(platform=platform_authority,catalog=catalog_authority));catrow=self.put('workspace/source-mapping/pinned-tree/Props/Directory.Packages.props',catalog)
        self.put('workspace/source-mapping/catalog-versions.json',dict(MappingHash=mapping['ContentHash'],CatalogCommit=catalog_commit,CatalogFileHash=catrow['sha256'],PinnedFiles={'Props/Directory.Packages.props':catrow['sha256']}))
        folder=self.workspace+'/workspace/source-mapping/'
        imports=dict(CustomBeforeDirectoryBuildProps=folder+'SourceMapping.targets',CustomAfterMicrosoftCommonProps=folder+'SourceMapping.props',CustomBeforeDirectoryBuildTargets=folder+'SourceMapping.before-targets.props',CustomAfterDirectoryBuildTargets=folder+'SourceMapping.late.props',AfterMicrosoftNETSdkTargets=folder+'SourceMapping.final.targets',HEXALITH_SOURCE_MAPPING_HASH=mapping['ContentHash'],Configuration='Debug')
        self.result['productionEnvironment']=imports
        self.result['preparation']=dict(status='prepared',buildPreparation=dict(environment=imports))
        self.put('logs/prepare-build.log',self.result['preparation']);self.put('logs/sdk-version.log',b'10.0.403\n')
        classes={
            v.FULL+'Workspace.PlatformModelCatalogTests':[('ModelBuildAppliesPinnedCatalogOnceBeforeOwnerPropsAsync',['mode','changeLiveCatalog'],[(('enum',0),False),(('enum',1),True)]),('ModelFixtureFact',[],[])],
            v.FULL+'CompositionSdkProcessTests':[('ProductionBareDotnetChildUsesExplicitControlsAsync',[],[]),('QualificationCatalogProbeInterceptionIsEstablishedAsync',[],[]),('StartDescriptionUsesCommandSpecificControls',['command','serial'],[('build',True),('--version',False)])],
            v.FULL+'CompositionProcessTests':[('CreateStartInfoScrubsInheritedEnvironmentAsync',[],[]),('DotnetChildrenRetainSdkControlsWithoutBroadeningOtherHelpers',[],[])],
            v.FULL+'McpCliRunBuildTests':[(v.MCP_METHODS[0],[],[]),(v.MCP_METHODS[1],['drift'],[('catalog',),('snapshot',)])],
        }
        self.resources={
            'Hexalith.Builds.Tooling.Workspace.SourceMapping.targets':b'<Project><UsingTask TaskName="Hexalith.Builds.Tooling.Workspace.SourceMappingValidationTask" AssemblyFile="__TOOLING_ASSEMBLY__"/><Import Project="$(MSBuildThisFileDirectory)SourceMapping.values.targets"/></Project>',
            'Hexalith.Builds.Tooling.Workspace.SourceMapping.props':b'<Project><Import Project="$(MSBuildThisFileDirectory)SourceMapping.values.props"/></Project>',
        }
        tooling=assembly('Hexalith.Builds.Tooling',resources=self.resources,enum=True)
        original=self.workspace+'/artifacts/original';model_output=self.workspace+'/workspace/build/platform-model/obj/Hexalith.Platform.Composition/debug/Hexalith.Platform.Composition.dll'
        tests=assembly('Hexalith.Builds.Module.Tests',classes=classes,codeview=original+'/obj/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.pdb')
        dependency=assembly('Hexalith.Dep');model=assembly('Hexalith.Platform.Composition',['Hexalith.Dep'],codeview=model_output[:-4]+'.pdb')
        role_names=dict(CLI='Hexalith.Builds.Module.Cli',Tooling='Hexalith.Builds.Tooling',AppHost='Hexalith.Builds.Module.AppHost',EventStoreHost='Hexalith.Builds.Module.EventStoreHost',UiHost='Hexalith.Builds.Module.UiHost',Model='Hexalith.Platform.Composition')
        self.result['outputs']={}
        for role,name in role_names.items():self.result['outputs'][role]=self.put('distribution/'+role+'/'+name+'.dll',tooling if role=='Tooling' else model if role=='Model' else assembly(name))
        self.put('distribution/Model/Hexalith.Dep.dll',dependency);testrow=self.put('distribution/Tests/Hexalith.Builds.Module.Tests.dll',tests)
        for role in ('Helper','CLI','AppHost','Model'):self.put('distribution/'+role+'/Hexalith.Builds.Tooling.dll',tooling)
        helperrow=self.put('distribution/Helper/DistributionProbe.dll',assembly('DistributionProbe'))
        self.result['apiInputBindings']=[dict(source=dict(self.documents_row(item['path']),path=original+'/bin/'+project+'/debug/'+Path(item['path']).name),retainedPath=item['path']) for item,project in [(helperrow,'DistributionProbe'),(self.result['outputs']['CLI'],'Hexalith.Builds.Module.Cli'),(self.documents_row('distribution/Helper/Hexalith.Builds.Tooling.dll'),'DistributionProbe'),(self.documents_row('distribution/CLI/Hexalith.Builds.Tooling.dll'),'Hexalith.Builds.Module.Cli')]]
        runner=self.root+'/references/Hexalith.Builds/.artifacts/fixture/bin/runner/Hexalith.Builds.Module.Tests.dll'
        self.result['originalRunner']=dict(testrow,path=runner);self.result['runnerRelocation']=dict(files=[dict(source=dict(testrow,path=original+'/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll'),destination=self.result['originalRunner'])])
        self.mapping_guards()
        protect=[b"status: 'in-progress'\n",b'  1-17-publish-the-pd10-v2-authorization-contract-spine: backlog\n',json.dumps(dict(productionControl=dict(routeMode='V1Only',T0=None,activationAuthority=None))).encode(),json.dumps(dict(productionControl=dict(routeMode='V1Only',T0=None,activationAuthority=None),checks=[dict(status='unexecuted',result=None)]*14)).encode()]
        self.result['protectedCopies']=[]
        for i,raw in enumerate(protect):row=self.put('protected/'+str(i)+'.json',raw);self.result['protectedCopies'].append(dict(source=dict(row,path='protected-source/'+str(i)),retained=row))
        sources=[]
        for full in classes:
            path='references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/'+full.removeprefix(v.FULL).replace('.','/')+'.cs';sources.append(binding(path,('fixture source '+full).encode()))
        self.put('source-baseline.json',sources);self.result['preservation']=dict(status='verified',errors=[],repositoryCount=13,boundSourceCount=len(sources))
        stores=[dict(path='/home/preserved/'+suffix,exists=False) for suffix in ('.dotnet/corefx/cryptography','.aspnet/https','.pki')];self.put('certificate-stores-before.json',stores);self.put('certificate-stores-after.json',stores)
        self.result['historicalPreservation']=dict(status='verified',files=0)
        assets=self.put('assets/model/project.assets.json',dict(libraries={'Hexalith.Dep/1.0.0':dict(type='project')}))
        self.result['modelAssets']=[dict(retained=assets,hexalithPackages=[])];self.result['originalAssets']=copy.deepcopy(self.result['modelAssets'])
        self.result['modelAssembly']=dict(self.result['outputs']['Model'],path=self.workspace+'/workspace/hosts/platform-model/Hexalith.Platform.Composition.dll')
        self.put('logs/production-model.log',dict(assembly=self.result['modelAssembly']['path']))
        child_env={key:value for key,value in environment.items() if key!='DOTNET_HOST_PATH'};child_env.update(imports)
        helper=original+'/bin/DistributionProbe/debug/DistributionProbe.dll';catalog=folder+'pinned-tree/Props/Directory.Packages.props';project=mapping['PlatformIdentity']['PlatformPath']+'/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj'
        self.result['modelChild']=dict(pid=101,argv=['dotnet','build',project,'--configuration','Debug','--output',self.workspace+'/workspace/hosts/platform-model','--artifacts-path',self.workspace+'/workspace/build/platform-model','-p:BuildsToolingAssemblyPath='+helper.replace('DistributionProbe.dll','Hexalith.Builds.Tooling.dll'),'-p:NuGetAudit=false','-p:MinVerVersionOverride=1.0.0','-nodeReuse:false','-p:UseSharedCompilation=false','-m:1','-p:BuildInParallel=false','-p:RestoreBuildInParallel=false','-p:AlternateCommonProps='+folder+'SourceMapping.model-common.props','-p:ImportDirectoryPackagesProps=false','-p:CentralPackageVersionsFileImported=true','-p:DirectoryPackagesPropsPath='+catalog],cwd=mapping['PlatformIdentity']['PlatformPath'],executable=self.sdk,environment=child_env)
        compiler='/installed/sdk/10.0.403/Roslyn/bincore/csc';compilerrow=binding(compiler,b'fixture compiler');self.put('sdk-bindings.json',[compilerrow,binding(self.sdk,b'fixture SDK')])
        cwd=mapping['PlatformIdentity']['PlatformPath']+'/src/Hexalith.Platform.Composition';reference='/source/references/Hexalith.Dep/bin/Hexalith.Dep.dll';source=cwd+'/Model.cs';analyzer='/installed/analyzer.dll'
        raw_args='/out:'+model_output+' /reference:'+reference+' /analyzer:'+analyzer+' Model.cs'
        process=self.compiler_process(cwd,compiler,self.workspace+'/tmpdir/model.rsp',raw_args)
        args=['/noconfig',*raw_args.split()]
        inputs=[]
        for kind,path,raw in [('compiler',compiler,b'fixture compiler'),('analyzer',analyzer,b'fixture analyzer'),('reference',reference,dependency),('source',source,b'fixture model source')]:
            item=binding(path,raw);retained=self.put('compiler-bytes/'+item['sha256']+'/'+Path(path).name,raw);inputs.append(dict(kind=kind,file=item,retained=retained))
        self.result['modelCompiler']=self.put('model-compiler.json',[dict(process=process,arguments=args,output=model_output,inputs=inputs)])
        self.model_compiler_process=process
        self.test_compiler_process=self.compiler_process(self.root+'/references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests',compiler,self.workspace+'/tmpdir/tests.rsp','/out:'+original+'/obj/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll '+' '.join(self.root+'/'+row['path'] for row in sources))
        parsed={name:v.pe_data(raw) for name,raw in self.documents.items() if name.startswith('distribution/') and name.endswith('.dll') and Path(name).name.startswith('Hexalith.')}
        self.put('pe-metadata.json',[dict(path=self.workspace+'/'+name,**{key:value[key] for key in ('identity','references','mvid')}) for name,value in parsed.items()])
        self.put('pe-inputs.json',[self.workspace+'/'+name for name in parsed])
        model_meta=parsed[self.result['outputs']['Model']['path']]
        self.result['modelReferenceIdentities']=dict(model=self.workspace+'/'+self.result['outputs']['Model']['path'],references=model_meta['references'],hexalithMatches=[dict(reference=row,output=self.workspace+'/distribution/Model/'+row['name']+'.dll') for row in model_meta['references']])
        self.put('output-seal.json',[self.documents_row(name) for name in self.documents if name.startswith('distribution/')])
        self.result['excludedHistoricalInterception']=dict(credit=False)
        selected=v.test_selection(v.pe_data(tests),v.pe_data(tooling));self.result['classDiscoveries']={};self.result['testClasses']=[]
        self.put('commands.json',[])
        self.result['commands']=[dict(label='helper-build',argv=['-p:VersionSuffix=20261030000000'])]
        expected=v.expected_commands(self.result,self.workspace)
        ordered=['sdk-version','helper-build','production-controls','cli-build','prepare-build','production-model','original-test-build']
        for full,cases in selected.items():
            name=full.removeprefix(v.FULL);discover='discover-mcpcli-guards' if name=='McpCliRunBuildTests' else 'discover-'+name;execute='mcpcli-guards' if name=='McpCliRunBuildTests' else 'class-'+name
            ordered.extend([discover,execute]);discovery=[dict(Assembly=runner,DisplayName=display,ID=hashlib.sha256(display.encode()).hexdigest(),Class=full,Method=method,Explicit=False) for method,display in sorted(cases)]
            log=self.put('logs/'+discover+'.log',discovery);self.result['classDiscoveries'][full]=dict(file=log,cases=discovery)
            xml=ET.Element('assemblies');node=ET.SubElement(xml,'assembly',name=runner,total=str(len(cases)),passed=str(len(cases)),failed='0',errors='0',skipped='0',**{'not-run':'0'});collection=ET.SubElement(node,'collection',total=str(len(cases)),passed=str(len(cases)),failed='0',skipped='0',**{'not-run':'0'})
            for method,display in sorted(cases):ET.SubElement(collection,'test',name=json.dumps(display)[1:-1],type=full,method=method,result='Pass')
            report=self.put(name+'.xml',ET.tostring(xml));self.result['testClasses'].append(dict(name=full,total=len(cases),passed=len(cases),discovered=len(cases),failed=0,errors=0,skipped=0,notRun=0,report=report))
        ordered+=['explicit-host-selection','metadata-only-inspection'];self.result['commands']=[]
        for pid,label in enumerate(ordered,1):
            argv,timeout=expected[label];env=dict(environment,**(imports if label=='original-test-build' else {}));log='logs/'+label+'.log'
            if log not in self.documents:self.put(log,b'fixture successful command\n')
            observation=dict(pid=pid,argv=argv,cwd=self.root+'/references/Hexalith.Builds',executable=helper[:-4] if label in {'explicit-host-selection','metadata-only-inspection'} else self.sdk,environment=env)
            observations=[observation]
            if label=='production-model':observations.extend([self.result['modelChild'],process])
            if label=='original-test-build':observations.append(self.test_compiler_process)
            obs=self.put('processes/'+label+'.json',observations)
            self.result['commands'].append(dict(label=label,argv=argv,cwd=observation['cwd'],environment=env,timeoutSeconds=timeout,launched=True,pid=pid,exitCode=0,timedOut=False,log=self.documents_row(log),processes=obs))
        self.sync()

    def documents_row(self,name):return binding(name,self.documents[name])
    def put(self,name,value):
        raw=value if isinstance(value,bytes) else (json.dumps(value,indent=2)+'\n').encode();self.documents[name]=raw;return binding(name,raw)
    def git_object(self,kind,raw):
        oid=hashlib.sha1(kind.encode()+b' '+str(len(raw)).encode()+b'\0'+raw).hexdigest();item=self.put('git-objects/'+oid,raw);self.objects[oid]=dict(kind=kind,oid=oid,file=item);return oid
    def compiler_process(self,cwd,compiler,path,text):return dict(pid=1000+len(text),argv=[compiler,'/noconfig','@'+path],cwd=cwd,executable=compiler,environment={},responses=[dict(file=binding(path,text.encode()),text=text)])
    def mapping_guards(self):
        w=self.workspace;mapping=self.result['productionMapping'];folder=w+'/workspace/source-mapping/';helper=w+'/artifacts/original/bin/Hexalith.Builds.Module.Cli/debug/Hexalith.Builds.Tooling.dll'
        # Resource names have a dotted basename; use its explicit retained names.
        for name in ('SourceMapping.targets','SourceMapping.props'):self.put('workspace/source-mapping/'+name,self.resources['Hexalith.Builds.Tooling.Workspace.'+name].replace(b'__TOOLING_ASSEMBLY__',helper.encode()))
        early=ET.Element('Project');ET.SubElement(early,'Import',Project=folder+'pinned-tree/Props/Directory.Packages.props');ET.SubElement(early,'Import',Project='$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props');self.put('workspace/source-mapping/SourceMapping.model-common.props',ET.tostring(early))
        condition="!$([System.String]::Copy('$(MSBuildProjectFullPath)').StartsWith('"+w+"/workspace/hosts/', System.StringComparison.Ordinal))"
        selection=dict(HexalithSourceMappingRoot=mapping['Root'],Configuration='Debug',UseHexalithProjectReferences='true',UseNuGetDeps='false')
        guarded=dict(HexalithSourceMappingApplies='true',HexalithSourceMappingHasGit='true',HexalithSourceMappingMode='source',HexalithSourceMappingHash=mapping['ContentHash'],HexalithSourceMappingRoot=mapping['Root'],_HXWObservedHexalithSourceMappingRoot=mapping['Root'],HexalithSourceMappingActiveModule=mapping['ActiveModule'],Configuration='Debug',UseHexalithProjectReferences='true',UseNuGetDeps='false')
        for entry in mapping['Entries']:
            suffix=entry['Identity'].removeprefix('Hexalith.');root='Hexalith'+suffix+'Root';origin='Hexalith'+suffix+'FromSource';selection[root]=entry['Path'];selection[origin]='true';guarded[root]=entry['Path'];guarded['_HXWObserved'+root]=entry['Path'];guarded[origin]='true'
        guarded.update({key:value for key,value in self.result['productionEnvironment'].items() if key in {'CustomBeforeDirectoryBuildProps','CustomBeforeDirectoryBuildTargets','CustomAfterDirectoryBuildTargets'}})
        xml=ET.Element('Project',InitialTargets='HexalithVerifySourceMappingSelection;HexalithValidateSourceProjectsBeforeRestore')
        for name in ('HexalithBindMappedRootSubmodules','HexalithVerifySourceMappingSelection','HexalithVerifyPreLateSourceMappingSelection','HexalithReconcileSourceMapping','HexalithValidateSourceProjectsBeforeRestore','HexalithValidateSourceMapping'):
            node=ET.SubElement(xml,'Target',Name=name,Condition=condition,BeforeTargets='Restore;CoreCompile')
            if name=='HexalithBindMappedRootSubmodules':continue
            if name=='HexalithVerifyPreLateSourceMappingSelection':
                for key,value in selection.items():ET.SubElement(node,'Error',Condition="'$(_HXWPreLateSelectionCaptured)' == 'true' and '$(_HXWPreLate"+key+")' != '"+value+"'",Text='HXW006: fixture')
            else:
                for key,value in guarded.items():ET.SubElement(node,'Error',Condition="'$("+key+")' != '"+value+"'",Text='HXW006: fixture')
                observed={'_HXWObservedConfiguration':'Debug','_HXWObservedUseHexalithProjectReferences':'true','_HXWObservedUseNuGetDeps':'false'}
                observed.update({'_HXWObserved'+key:value for key,value in selection.items() if key.endswith('FromSource')})
                for key,value in observed.items():ET.SubElement(node,'Error',Condition="'$("+key+")' != '' and '$("+key+")' != '"+value+"'",Text='HXW006: fixture')
                ET.SubElement(node,'Error',Condition="!$([System.String]::Copy(';$(AfterMicrosoftNETSdkTargets);').Contains(';"+folder+"SourceMapping.final.targets;', System.StringComparison.Ordinal))",Text='HXW006: fixture')
                ET.SubElement(node,'Error',Condition="'$(IsCrossTargetingBuild)' != 'true' and '$(_HXWFinalTargetsImported)' != 'true'",Text='HXW006: fixture')
            if name in {'HexalithReconcileSourceMapping','HexalithValidateSourceProjectsBeforeRestore','HexalithValidateSourceMapping'}:
                attrs=dict(MappingFile='$(MSBuildThisFileDirectory)mapping.json',MappingHash='$(HexalithSourceMappingHash)',Projects='@(ProjectReference)',Packages='@(PackageReference)')
                if name=='HexalithValidateSourceProjectsBeforeRestore':attrs['ProjectsOnly']='true'
                ET.SubElement(node,'SourceMappingReconciliationTask' if name=='HexalithReconcileSourceMapping' else 'SourceMappingValidationTask',**attrs)
        self.put('workspace/source-mapping/SourceMapping.values.targets',ET.tostring(xml))
        final=ET.Element('Project');ET.SubElement(ET.SubElement(final,'PropertyGroup'),'_HXWFinalTargetsImported').text='true';node=ET.SubElement(final,'Target',Name='HexalithValidateFinalSourceProjects',Condition=condition,BeforeTargets='ResolveProjectReferences')
        values={key:value for key,value in guarded.items() if key in {'HexalithSourceMappingApplies','HexalithSourceMappingMode','HexalithSourceMappingHash','HexalithSourceMappingRoot','Configuration','UseHexalithProjectReferences','UseNuGetDeps'} or (key.startswith('Hexalith') and (key.endswith('FromSource') or key.endswith('Root') or key=='HexalithTenantsBasePath'))}
        for key,value in values.items():ET.SubElement(node,'Error',Condition="'$("+key+")' != '"+value+"'",Text='HXW006: fixture')
        ET.SubElement(node,'SourceMappingValidationTask');self.put('workspace/source-mapping/SourceMapping.final.targets',ET.tostring(final))

    def incomplete(self):
        before=[dict(path='/home/preserved/'+suffix,exists=(suffix=='.pki')) for suffix in ('.dotnet/corefx/cryptography','.aspnet/https','.pki')]+[dict(path='/home/preserved/.pki/nssdb/cert9.db',mode=33152,bytes=100,mtimeNs=1)]
        after=copy.deepcopy(before);after[-1]['mtimeNs']=2
        self.put('certificate-stores-before.json',before);self.put('certificate-stores-after.json',after)
        prior=copy.deepcopy(self.result);prior.update(qualification='incomplete',firstFailure=dict(owner='unproven NSS writer'),preservation=dict(self.result['preservation'],status='failed-or-unknown',errors=['Certificate-store metadata drift']))
        prior['commands']=prior['commands'][:7]
        self.put('prior-phase/result.json',prior);self.put('prior-phase/commands.json',prior['commands'])
        old_members=[dict(self.documents_row('prior-phase/result.json'),path='result.json'),dict(self.documents_row('prior-phase/commands.json'),path='commands.json')]
        old_members += [self.documents_row(name) for name in ('source-baseline.json','certificate-stores-before.json','certificate-stores-after.json')]
        manifest=self.put('prior-phase/members.json',old_members)
        self.put('prior-phase/receipt.json',dict(result=prior,memberManifest=dict(manifest,path='members.json')))
        baseline=self.put('correction/source-baseline.json',json.loads(self.documents['source-baseline.json']))
        self.put('correction/certificate-stores-before.json',after);self.put('correction/certificate-stores-after.json',after)
        self.result.update(qualification='incomplete',firstFailure=dict(owner='unproven NSS writer'),independentDiagnosticGates='passed',currentCorrection=dict(kind='runner-location-only',productionCommandsReexecuted=0,sourceBaseline=baseline,sourceChanges=[]),priorGlobalStoreObservation=dict(preservation=prior['preservation'],before=before,current=after))
        self.sync();return self

    def sync(self):
        self.put('commands.json',self.result['commands']);self.put('result.json',self.result)
    def save(self,directory):
        self.sync();directory=Path(directory);directory.mkdir(exist_ok=True)
        archive=directory/'capsule.tar.gz';members=[]
        with tarfile.open(archive,'w:gz') as output:
            for name,raw in sorted(self.documents.items()):
                info=tarfile.TarInfo(name);info.size=len(raw);output.addfile(info,io.BytesIO(raw));members.append(binding(name,raw))
        manifest=directory/'members.json';manifest.write_text(json.dumps(members))
        receipt=dict(schemaVersion=1,privateWorkspace=self.workspace,result=self.result,archive=binding(archive.name,archive.read_bytes()),memberManifest=binding(manifest.name,manifest.read_bytes()),memberCount=len(members))
        path=directory/'receipt.json';path.write_text(json.dumps(receipt));return path
