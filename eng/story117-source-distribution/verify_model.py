#!/usr/bin/env python3
"""Replay portable production-model evidence as data only. No command is executed."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import posixpath
import shlex
import struct
import uuid
from collections import Counter
import tarfile
import xml.etree.ElementTree as ET

from verify import bound, managed_pe, require, safe_path, verify_case_identities

AUTHORITY = dict(parentStory="in-progress", tracker="backlog", routingPolicy="V1Only", T0=None, activationAuthority=None, stagingExecuted=0, stagingRequired=14)
CONTROLS = {"DOTNET_GENERATE_ASPNET_CERTIFICATE":"false", "DOTNET_CLI_TELEMETRY_OPTOUT":"1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE":"1", "DOTNET_NOLOGO":"1", "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH":"false", "DOTNET_CLI_USE_MSBUILD_SERVER":"0", "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE":"true", "MSBUILDDISABLENODEREUSE":"1"}
PRIVATE = ("DOTNET_CLI_HOME", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_PLUGINS_CACHE_PATH", "NUGET_SCRATCH", "TMPDIR", "TEMP", "TMP")
FULL = "Hexalith.Builds.ModuleTool.Tests."
MCP_METHODS = ("BuildArgumentsUseDebugRunWorkspaceArtifactsAndThePinnedCatalog", "McpCliBuildRefusesMissingOrAlteredCatalogAuthorityAsync")
REQUIRED_CLASSES = {FULL + name for name in ("Workspace.PlatformModelCatalogTests", "CompositionSdkProcessTests", "CompositionProcessTests", "McpCliRunBuildTests")}


def dotnet_json(value):
    text = json.dumps(value, ensure_ascii=True, separators=(",", ":")).replace('\\"', '\\u0022')
    text = re.sub(r"[<>&'+`]", lambda match: "\\u" + format(ord(match[0]), "04X"), text)
    return re.sub(r"\\u([a-fA-F0-9]{4})", lambda match: "\\u" + match[1].upper(), text).encode()


def git_tree(raw):
    offset, entries = 0, {}
    while offset < len(raw):
        space, zero = raw.index(b" ", offset), raw.index(b"\0", offset)
        mode, name = raw[offset:space], raw[space+1:zero].decode()
        require(name and name not in entries and zero + 21 <= len(raw), "Malformed Git authority tree")
        entries[name] = (raw[zero+1:zero+21].hex(), "tree" if mode == b"40000" else "commit" if mode == b"160000" else "blob")
        offset = zero + 21
    return entries


def verify_git_path(authority, path, documents, inventory):
    objects = {}
    for item in authority["objects"]:
        require(inventory.get(item["file"]["path"]) == item["file"], "Git object inventory mismatch")
        raw = documents[item["file"]["path"]]
        require(hashlib.sha1(item["kind"].encode() + b" " + str(len(raw)).encode() + b"\0" + raw).hexdigest() == item["oid"], "Git object identity mismatch")
        require(item["oid"] not in objects, "Duplicate authority object")
        objects[item["oid"]] = (item["kind"], raw)
    kind, commit = objects[authority["revision"]]
    require(kind == "commit" and commit.startswith(b"tree "), "Invalid revision authority")
    oid, kind = commit.split(b"\n", 1)[0].decode()[5:], "tree"
    for part in safe_path(path).parts:
        require(kind == "tree" and objects[oid][0] == "tree", "Git authority traverses non-tree")
        oid, kind = git_tree(objects[oid][1])[part]
    require(authority["paths"][path] == dict(kind=kind, oid=oid), "False Git path authority")
    return oid, kind, objects.get(oid, (None, None))[1]


# ECMA-335 table layouts. Heap and coded indexes are read directly; no assembly is loaded.
CODED = {
    'TypeDefOrRef': (2, [2, 1, 27]), 'HasConstant': (2, [4, 8, 23]),
    'HasCustomAttribute': (5, [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43]),
    'HasFieldMarshal': (1, [4, 8]), 'HasDeclSecurity': (2, [2, 6, 32]),
    'MemberRefParent': (3, [2, 1, 26, 6, 27]), 'HasSemantics': (1, [20, 23]),
    'MethodDefOrRef': (1, [6, 10]), 'MemberForwarded': (1, [4, 6]), 'Implementation': (2, [38, 35, 39]),
    'CustomAttributeType': (3, [None, None, 6, 10, None]), 'ResolutionScope': (2, [0, 26, 35, 1]),
    'TypeOrMethodDef': (1, [2, 6]),
}
TABLES = {
 0: [2,'s','g','g','g'], 1: ['ResolutionScope','s','s'], 2: [4,'s','s','TypeDefOrRef',('t',4),('t',6)],
 3: [('t',4)], 4: [2,'s','b'], 5: [('t',6)], 6: [4,2,2,'s','b',('t',8)], 7: [('t',8)], 8: [2,2,'s'],
 9: [('t',2),'TypeDefOrRef'], 10: ['MemberRefParent','s','b'], 11: [2,'HasConstant','b'],
 12: ['HasCustomAttribute','CustomAttributeType','b'], 13: ['HasFieldMarshal','b'], 14: [2,'HasDeclSecurity','b'],
 15: [2,4,('t',2)], 16: [4,('t',4)], 17: ['b'], 18: [('t',2),('t',20)], 19: [('t',20)],
 20: [2,'s','TypeDefOrRef'], 21: [('t',2),('t',23)], 22: [('t',23)], 23: [2,'s','b'],
 24: [2,('t',6),'HasSemantics'], 25: [('t',2),'MethodDefOrRef','MethodDefOrRef'], 26: ['s'], 27: ['b'],
 28: [2,'MemberForwarded','s',('t',26)], 29: [4,('t',4)], 30: [4,4], 31: [4],
 32: [4,2,2,2,2,4,'b','s','s'], 33: [4], 34: [4,4,4], 35: [2,2,2,2,4,'b','s','s','b'],
 36: [4,('t',35)], 37: [4,4,4,('t',35)], 38: [4,'s','b'], 39: [4,4,'s','s','Implementation'],
 40: [4,4,'s','Implementation'], 41: [('t',2),('t',2)], 42: [2,2,'TypeOrMethodDef','s'],
 43: ['MethodDefOrRef','b'], 44: [('t',42),'TypeDefOrRef'],
}


def compressed(raw, offset):
    first = raw[offset]
    size = 1 if first < 128 else 2 if first < 192 else 4 if first < 224 else 0
    require(size and offset + size <= len(raw), 'Invalid compressed metadata integer')
    value = first & (127 if size == 1 else 63 if size == 2 else 31)
    for byte in raw[offset+1:offset+size]:
        value = (value << 8) | byte
    return value, offset + size


def pe_data(payload):
    """Read assembly identity, references, resources and test attributes from retained PE bytes."""
    managed_pe(payload)
    pe = struct.unpack_from('<I', payload, 60)[0]
    optional = pe + 24
    directories = optional + (96 if struct.unpack_from('<H', payload, optional)[0] == 267 else 112)
    sections = optional + struct.unpack_from('<H', payload, pe + 20)[0]
    def locate(rva, size):
        for index in range(struct.unpack_from('<H', payload, pe + 6)[0]):
            virtual, address, raw_size, pointer = struct.unpack_from('<IIII', payload, sections + index*40 + 8)
            if address <= rva and rva + size <= address + min(virtual, raw_size):
                require(pointer + rva - address + size <= len(payload), 'PE range exceeds file')
                return pointer + rva - address
        raise ValueError('PE range outside sections')
    cli = locate(*struct.unpack_from('<II', payload, directories + 14*8))
    metadata = locate(*struct.unpack_from('<II', payload, cli + 8))
    cursor = (metadata + 16 + struct.unpack_from('<I', payload, metadata + 12)[0] + 3) & ~3
    count = struct.unpack_from('<H', payload, cursor + 2)[0]
    cursor += 4
    streams = {}
    for _ in range(count):
        offset, size = struct.unpack_from('<II', payload, cursor)
        end = payload.index(b'\0', cursor + 8)
        streams[payload[cursor+8:end].decode()] = payload[metadata+offset:metadata+offset+size]
        cursor = (end + 4) & ~3
    raw = streams.get('#~', streams.get('#-'))
    require(raw is not None, 'Metadata table stream absent')
    heaps = raw[6]
    valid = struct.unpack_from('<Q', raw, 8)[0]
    cursor, counts = 24, {}
    for table in range(64):
        if valid & (1 << table):
            counts[table] = struct.unpack_from('<I', raw, cursor)[0]
            cursor += 4
    def width(kind):
        if isinstance(kind, int): return kind
        if isinstance(kind, tuple): return 4 if counts.get(kind[1], 0) >= 65536 else 2
        if kind in ('s','g','b'): return 4 if heaps & {'s':1,'g':2,'b':4}[kind] else 2
        bits, targets = CODED[kind]
        return 4 if max((counts.get(target, 0) for target in targets if target is not None), default=0) >= (1 << (16-bits)) else 2
    tables = {}
    for table, rows in sorted(counts.items()):
        require(table in TABLES or rows == 0, 'Unsupported assembly metadata table')
        schema = TABLES.get(table, [])
        tables[table] = []
        for _ in range(rows):
            row = []
            for kind in schema:
                size = width(kind)
                require(cursor + size <= len(raw), 'Truncated metadata table')
                row.append(int.from_bytes(raw[cursor:cursor+size], 'little'))
                cursor += size
            tables[table].append(row)
    def string(index):
        heap = streams['#Strings']
        require(index < len(heap), 'Invalid metadata string')
        return heap[index:heap.index(b'\0', index)].decode()
    def blob(index):
        heap = streams['#Blob']
        length, start = compressed(heap, index)
        require(start + length <= len(heap), 'Truncated metadata blob')
        return heap[start:start+length]
    def identity(name, versions, culture, key, full):
        key = blob(key)
        token = hashlib.sha1(key).digest()[-8:][::-1].hex() if key and full else key.hex() if key else 'null'
        return dict(name=string(name), version='.'.join(map(str,versions)), culture=string(culture) or 'neutral', publicKeyToken=token)
    assembly = tables[32][0]
    result = dict(identity=identity(assembly[7], assembly[1:5], assembly[8], assembly[6], True),
                  references=[identity(row[6], row[:4], row[7], row[5], bool(row[4]&1)) for row in tables.get(35,[])],
                  mvid=str(uuid.UUID(bytes_le=streams['#GUID'][(tables[0][0][2]-1)*16:tables[0][0][2]*16])), resources={}, methods={}, enums={}, codeview=[])
    resources_rva, resources_size = struct.unpack_from('<II', payload, cli+24)
    for row in tables.get(40, []):
        if row[3] == 0:
            location = locate(resources_rva + row[0], 4)
            size = struct.unpack_from('<I', payload, location)[0]
            require(row[0]+4+size <= resources_size, 'Resource exceeds CLI resource directory')
            result['resources'][string(row[2])] = payload[location+4:location+4+size]
    debug_rva, debug_size = struct.unpack_from('<II', payload, directories+6*8)
    if debug_rva:
        debug = locate(debug_rva, debug_size)
        for offset in range(debug,debug+debug_size,28):
            if struct.unpack_from('<I',payload,offset+12)[0] == 2:
                size, pointer = struct.unpack_from('<I',payload,offset+16)[0],struct.unpack_from('<I',payload,offset+24)[0]
                data = payload[pointer:pointer+size]
                require(data[:4] == b'RSDS' and b'\0' in data[24:], 'Invalid CodeView output identity')
                result['codeview'].append(data[24:data.index(b'\0',24)].decode())
    def decoded(kind, value):
        bits, targets = CODED[kind]
        tag = value & ((1 << bits)-1)
        require(tag < len(targets), 'Invalid coded metadata index')
        return targets[tag], (value >> bits)
    attributes = {}
    for row in tables.get(12, []):
        parent, rid = decoded('HasCustomAttribute', row[0])
        if parent != 6: continue
        target, target_id = decoded('CustomAttributeType', row[1])
        if target != 10: continue
        member = tables[10][target_id-1]
        owner, owner_id = decoded('MemberRefParent',member[0])
        if owner != 1: continue
        ref = tables[1][owner_id-1]
        name = string(ref[2])+'.'+string(ref[1])
        attributes.setdefault(rid, []).append((name, blob(row[2])))
    methods = tables.get(6, [])
    fields = tables.get(4, [])
    constants = {}
    for row in tables.get(11, []):
        target, rid = decoded('HasConstant', row[1])
        if target == 4 and row[0] == 8: constants[rid] = int.from_bytes(blob(row[2]),'little',signed=True)
    for index, definition in enumerate(tables.get(2, [])):
        name = string(definition[2])+'.'+string(definition[1])
        next_def = tables[2][index+1] if index+1 < len(tables[2]) else None
        enum = {constants[rid]:string(fields[rid-1][1]) for rid in range(definition[4], next_def[4] if next_def else len(fields)+1) if rid in constants}
        if enum: result['enums'][name] = enum
        selected = []
        for rid in range(definition[5],next_def[5] if next_def else len(methods)+1):
            method = methods[rid-1]
            attrs = attributes.get(rid,[])
            if not any(attr in ('Xunit.FactAttribute','Xunit.TheoryAttribute') for attr,_ in attrs): continue
            require(method[2]&7 == 6, 'Nonpublic original test method')
            next_method = methods[rid] if rid < len(methods) else None
            params = [string(row[2]) for row in tables.get(8,[])[method[5]-1:(next_method[5]-1) if next_method else None] if row[1]]
            selected.append(dict(name=string(method[3]), parameters=params, attributes=attrs))
        if selected: result['methods'][name] = selected
    return result


def test_selection(runner, tooling):
    """Reconstruct the full selected facts/theories from the original assembly attributes."""
    def inline(raw):
        require(raw[:2] == b'\x01\x00', 'Invalid InlineData prolog')
        count = int.from_bytes(raw[2:6],'little',signed=True)
        require(0 <= count < 128, 'Invalid InlineData count')
        offset, values = 6, []
        def text():
            nonlocal offset
            if raw[offset] == 255: offset += 1; return None
            length, offset = compressed(raw,offset)
            value = raw[offset:offset+length].decode();offset += length
            return value
        for _ in range(count):
            kind=raw[offset];offset+=1
            if kind==2: values.append('True' if raw[offset] else 'False');offset+=1
            elif kind==8: values.append(str(int.from_bytes(raw[offset:offset+4],'little',signed=True)));offset+=4
            elif kind==14: values.append(json.dumps(text(),ensure_ascii=False))
            elif kind==85:
                enum=text().split(',',1)[0]
                number=int.from_bytes(raw[offset:offset+4],'little',signed=True);offset+=4
                values.append(tooling['enums'][enum][number])
            else: raise ValueError('Unsupported original InlineData value')
        require(raw[offset:] == b'\x00\x00', 'Unexpected InlineData named arguments')
        return values
    selection={}
    for full in REQUIRED_CLASSES:
        methods=runner['methods'].get(full,[])
        require(methods,'Original selected class absent from assembly')
        names=[]
        for method in methods:
            name=method['name']
            if full.endswith('CompositionSdkProcessTests') and name=='QualificationCatalogProbeInterceptionIsEstablishedAsync': continue
            if full.endswith('McpCliRunBuildTests') and name not in MCP_METHODS: continue
            data=[raw for attr,raw in method['attributes'] if attr=='Xunit.InlineDataAttribute']
            theory=any(attr=='Xunit.TheoryAttribute' for attr,_ in method['attributes'])
            require(bool(data)==theory,'Original theory data absent')
            if theory:
                for raw in data:
                    values=inline(raw)
                    require(len(values)==len(method['parameters']),'Theory parameter count differs')
                    names.append((name,full+'.'+name+'('+', '.join(param+': '+value for param,value in zip(method['parameters'],values))+')'))
            else:names.append((name,full+'.'+name))
        require(names and len(names)==len(set(names)), 'Empty/duplicate original method selection')
        selection[full]=set(names)
    return selection


def private_path(value, workspace):
    """Validate recorded private paths lexically, without consulting a replay machine."""
    return (isinstance(value, str) and Path(value).is_absolute() and not any(part in {".", ".."} for part in value.split("/"))
            and "\\" not in value and "\0" not in value and str(Path(value)) == value and Path(value).is_relative_to(workspace))


def verify_child(child, mapping, workspace, sdk, helper_tooling, home=None):
    source_root = mapping["Root"]
    project = mapping["PlatformIdentity"]["PlatformPath"] + "/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj"
    catalog = workspace + "/workspace/source-mapping/pinned-tree/Props/Directory.Packages.props"
    expected = ["dotnet", "build", project, "--configuration", "Debug", "--output", workspace + "/workspace/hosts/platform-model",
                "--artifacts-path", workspace + "/workspace/build/platform-model", "-p:BuildsToolingAssemblyPath=" + helper_tooling,
                "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0", "-nodeReuse:false", "-p:UseSharedCompilation=false",
                "-m:1", "-p:BuildInParallel=false", "-p:RestoreBuildInParallel=false",
                "-p:AlternateCommonProps=" + workspace + "/workspace/source-mapping/SourceMapping.model-common.props",
                "-p:ImportDirectoryPackagesProps=false", "-p:CentralPackageVersionsFileImported=true", "-p:DirectoryPackagesPropsPath=" + catalog]
    require(child["argv"] == expected, "Production child arguments were changed or qualification controls injected")
    require(child["executable"] == sdk and child["cwd"] == source_root + "/references/Hexalith.Platform", "Substituted executable or wrong working directory")
    folder=workspace+'/workspace/source-mapping/'
    imports=dict(CustomBeforeDirectoryBuildProps=folder+'SourceMapping.targets', CustomAfterMicrosoftCommonProps=folder+'SourceMapping.props', CustomBeforeDirectoryBuildTargets=folder+'SourceMapping.before-targets.props', CustomAfterDirectoryBuildTargets=folder+'SourceMapping.late.props', AfterMicrosoftNETSdkTargets=folder+'SourceMapping.final.targets', HEXALITH_SOURCE_MAPPING_HASH=mapping['ContentHash'], Configuration='Debug')
    verify_environment(child['environment'],workspace,sdk,home if home is not None else child['environment'].get('HOME'),imports,child=True)


def verify_environment(environment, workspace, sdk, home, imports=None, child=False):
    allowed = set(CONTROLS) | set(PRIVATE) | {'HOME','LANG','DOTNET_ROOT','DOTNET_HOST_PATH','PATH'} | set(imports or {})
    require(set(environment) <= allowed, 'Unapproved executable/import/environment control')
    require(all(environment.get(key) == value for key,value in CONTROLS.items()), 'SDK prevention control missing or unsafe')
    require(all(private_path(environment.get(key),workspace) for key in PRIVATE), 'Private SDK cache boundary escaped')
    require(environment.get('HOME') == home and environment.get('DOTNET_ROOT') == str(Path(sdk).parent), 'HOME or SDK root changed')
    require(environment.get('DOTNET_HOST_PATH',sdk) == sdk and (child or environment.get('DOTNET_HOST_PATH') == sdk), 'Selected host changed')
    require(environment.get('PATH','').split(':')[0] == str(Path(sdk).parent), 'Selected SDK is not first on PATH')
    require(all(environment.get(key) == value for key,value in (imports or {}).items()), 'Genuine mapping imports changed')


def selected_paths(result, workspace):
    environment=result['environment']
    sdk=environment['DOTNET_HOST_PATH']
    require(private_path(sdk,str(Path(sdk).parent)) and Path(sdk).name=='dotnet', 'Invalid selected SDK executable')
    mapping=result.get('productionMapping')
    manifest=result.get('proposedOrdinaryInvocation',{}).get('manifest',{}).get('path','')
    root=mapping['Root'] if mapping else manifest.removesuffix('/references/Hexalith.Parties/module/hexalith.module-manifest.v2.json')
    require(root and Path(root).is_absolute(), 'Source root authority absent')
    builds=root+'/references/Hexalith.Builds'
    original=workspace+'/artifacts/original'
    helper=original+'/bin/DistributionProbe/debug/DistributionProbe.dll'
    cli=original+'/bin/Hexalith.Builds.Module.Cli/debug/Hexalith.Builds.Module.Cli.dll'
    return sdk,root,builds,original,helper,cli


def expected_commands(result,workspace):
    sdk,root,builds,original,helper,cli=selected_paths(result,workspace)
    suffixes=[arg.removeprefix('-p:VersionSuffix=') for row in result['commands'] if row['label']=='helper-build' for arg in row['argv'] if arg.startswith('-p:VersionSuffix=')]
    require(len(suffixes)<=1 and all(re.fullmatch(r'\d{14}',value) for value in suffixes), 'Ambiguous graph version suffix')
    suffix=suffixes[0] if suffixes else None
    expected={'sdk-version':([sdk,'--version'],30), 'production-controls':([sdk,helper,'production-controls',builds],900),
              'prepare-build':([sdk,cli,'prepare-build','--manifest',root+'/references/Hexalith.Parties/module/hexalith.module-manifest.v2.json','--workspace',workspace+'/workspace','--mode','source','--output','json'],900),
              'explicit-host-selection':([helper[:-4],'select',workspace+'/distribution/CLI/Hexalith.Builds.Module.Cli.dll',workspace+'/distribution/AppHost/Hexalith.Builds.Module.AppHost.dll'],30),
              'metadata-only-inspection':([helper[:-4],'inspect',workspace+'/pe-inputs.json'],60)}
    if 'productionMapping' in result:
        expected['production-model']=([sdk,helper,'production-model',workspace+'/workspace/source-mapping/mapping.json',result['productionMapping']['ContentHash'],workspace+'/workspace'],900)
    for label,project in [('helper-build',workspace+'/helper/DistributionProbe.csproj'),('cli-build',builds+'/src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj'),('original-test-build',builds+'/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj')]:
        if suffix is not None:
            expected[label]=([sdk,'build',project,'--configuration','Debug','--artifacts-path',original,'-p:RestoreConfigFile='+workspace+'/NuGet.Config','-p:NuGetAudit=false','-p:MinVerVersionOverride=1.0.0','-p:VersionSuffix='+suffix,
                             '-p:HexalithG4FixtureArtifacts='+workspace+'/fixtures/','-m:1','-nr:false','-p:BuildInParallel=false','-p:RestoreBuildInParallel=false','-p:UseSharedCompilation=false','-v:diag','-bl:'+workspace+'/logs/original-'+Path(project).stem+'.binlog'],900)
    runner=result.get('originalRunner',{}).get('path')
    if runner:
        require(private_path(runner,builds+'/.artifacts') and '/bin/' in runner and Path(runner).name=='Hexalith.Builds.Module.Tests.dll', 'Original runner outside selected private Builds bin')
        for full in REQUIRED_CLASSES:
            name=full.removeprefix(FULL)
            if name=='McpCliRunBuildTests':
                filters=[item for method in MCP_METHODS for item in ('-method',full+'.'+method)]
                discover,execute='discover-mcpcli-guards','mcpcli-guards'
            else:
                filters=['-class',full]
                if name=='CompositionSdkProcessTests': filters+=['-method-',full+'.QualificationCatalogProbeInterceptionIsEstablishedAsync']
                discover,execute='discover-'+name,'class-'+name
            expected[discover]=([sdk,runner,'-list','full/json','-preEnumerateTheories',*filters,'-parallel','none','-noColor'],900)
            expected[execute]=([sdk,runner,*filters,'-parallel','none','-xml',workspace+'/'+name+'.xml'],900)
    return expected


def verify_commands(result, documents, inventory, workspace):
    commands=result['commands']
    require(commands==json.loads(documents['commands.json']), 'Command ledger disagrees')
    expected=expected_commands(result,workspace)
    sdk,root,builds,original,helper,cli=selected_paths(result,workspace)
    verify_environment(result['environment'],workspace,sdk,result['environment']['HOME'])
    credited=result['qualification']=='passed' or result.get('independentDiagnosticGates')=='passed'
    labels=[]
    for row in commands:
        label=row['label'];labels.append(label)
        require(label in expected and (row['argv'],row['timeoutSeconds'])==expected[label], 'Command violates closed executable/project/filter/argument contract: '+label)
        require(row['cwd']==builds and Path(row['log']['path']).name==label+'.log' and inventory.get(row['log']['path'])==row['log'], 'Command owner/log binding changed')
        require(row['processes']['path']=='processes/'+label+'.json' and inventory.get(row['processes']['path'])==row['processes'], 'Process observation binding missing')
        mapping=result.get('productionEnvironment',{}) if label=='original-test-build' else {}
        require(row['environment']==dict(result['environment'],**mapping), 'Outer command environment changed')
        verify_environment(row['environment'],workspace,sdk,result['environment']['HOME'],mapping)
        observations=json.loads(documents[row['processes']['path']])
        if not row['launched']:
            require(not credited and result['qualification']=='incomplete' and row['exitCode'] is None and row['timedOut'] is False and not row.get('pid') and observations==[], 'Failed launch given execution credit')
            continue
        require(type(row.get('pid')) is int and row['pid']>0 and type(row['exitCode']) is int and type(row['timedOut']) is bool, 'Incomplete execution result')
        physical=helper[:-4] if label in {'explicit-host-selection','metadata-only-inspection'} else sdk
        roots=[item for item in observations if item.get('pid')==row['pid']]
        require(roots and all(item['argv']==row['argv'] and item['cwd']==row['cwd'] and item['executable']==physical and item['environment']==row['environment'] for item in roots), 'Actual root process differs from command record')
        if credited: require(row['exitCode']==0 and not row['timedOut'], 'Credited command failed or timed out')
    require(len(labels)==len(set(labels)), 'Duplicate command label')
    if credited:
        require(set(labels)==set(expected), 'Credited production/class/discovery command missing')
        selected=[row for row in json.loads(documents['sdk-bindings.json']) if row['path']==sdk]
        require(len(selected)==1 and selected[0]['bytes']>0 and re.fullmatch('[a-f0-9]{64}',selected[0]['sha256']), 'Selected executable SDK byte binding absent')


def verify_authority(result, documents, inventory):
    mapping = json.loads(documents["workspace/source-mapping/mapping.json"])
    require(mapping == result["productionMapping"] and mapping["Mode"] == "source" and mapping["HasGit"], "False source mapping")
    canonical = {key: mapping[key] for key in ("Mode", "Root", "ActiveModule", "Entries", "HasGit", "PlatformIdentity", "IdentityFindings")}
    require(hashlib.sha256(dotnet_json(canonical)).hexdigest() == mapping["ContentHash"], "Source mapping content hash mismatch")
    repos = json.loads(documents["repository-baseline.json"])
    require(repos == result["initialRepositories"] and len(repos) == 13, "Repository tuple mismatch")
    require(all(re.fullmatch("[a-f0-9]{40}", row["revision"]) for row in repos), "Abbreviated revision")
    require(all(row["revision"] == row["indexedGitlink"] == row["rootGitlink"] for row in repos[1:]), "Root HEAD/index/checkout drift")
    origins = {row["path"]: row["revision"] for row in repos[1:]}
    require(len(mapping["Entries"]) == 12 and {entry["Path"] for entry in mapping["Entries"]} == {mapping["Root"] + "/" + path for path in origins}
            and all(entry["Origin"] == "source" for entry in mapping["Entries"]), "Source fallback or foreign reference")
    identity = mapping["PlatformIdentity"]
    require(identity["CheckoutCommit"] == origins["references/Hexalith.Platform"], "False Platform authority")
    snapshot = json.loads(documents["workspace/source-mapping/catalog-versions.json"])
    require(snapshot["MappingHash"] == mapping["ContentHash"] and snapshot["CatalogCommit"] == identity["CatalogCommit"], "False catalog authority")
    authority = json.loads(documents["git-authority.json"])
    require(authority["platform"]["revision"] == identity["CheckoutCommit"] and authority["catalog"]["revision"] == identity["CatalogCommit"], "Git authority revision drift")
    oid, kind, _ = verify_git_path(authority["platform"], "references/Hexalith.Builds", documents, inventory)
    require(oid == identity["CatalogCommit"] and kind == "commit", "Catalog differs from genuine Platform gitlink")
    for path, digest in snapshot["PinnedFiles"].items():
        safe_path(path)
        name = "workspace/source-mapping/pinned-tree/" + path
        oid, kind, raw = verify_git_path(authority["catalog"], path, documents, inventory)
        require(kind == "blob" and raw.removeprefix(b"\xef\xbb\xbf") == documents[name] and inventory[name]["sha256"] == digest, "Pinned catalog/import differs from committed authority")
    require(snapshot["CatalogFileHash"] == snapshot["PinnedFiles"]["Props/Directory.Packages.props"], "Catalog file hash mismatch")
    return mapping


def verify_preservation(result,documents):
    """A retained failed interval cannot be turned into full qualification."""
    home=result.get('environment',{}).get('HOME')
    def observation(rows):
        require(home and isinstance(rows,list) and rows, 'Store observation absent')
        names={row.get('path'):row for row in rows}
        roots=[home+'/.dotnet/corefx/cryptography',home+'/.aspnet/https',home+'/.pki']
        require(len(names)==len(rows) and all(path in names and type(names[path].get('exists')) is bool for path in roots), 'Store root observation inventory incomplete')
        for path,row in names.items():
            require(isinstance(path,str) and Path(path).is_absolute() and str(Path(path))==path and '..' not in Path(path).parts, 'Unsafe store metadata path')
            if path in roots:require(set(row)=={'path','exists'}, 'Malformed store root probe')
            else:
                require(any(names[root]['exists'] and Path(path).is_relative_to(root) for root in roots) and set(row)=={'path','mode','bytes','mtimeNs'} and all(type(row[key]) is int and row[key]>=0 for key in ('mode','bytes','mtimeNs')), 'Malformed store metadata observation')
        return rows
    for name in ('preservation','historicalPreservation'):
        if result.get(name,{}).get('status')=='verified':require(result[name].get('errors',[])==[], 'Verified preservation retains errors')
    intervals=[]
    before=documents.get('certificate-stores-before.json')
    after=documents.get('certificate-stores-after.json')
    if before is not None or after is not None:
        require(before is not None and after is not None, 'Certificate observation interval incomplete')
        intervals.append((observation(json.loads(before)),observation(json.loads(after))))
    correction=result.get('currentCorrection')
    if correction:
        prior=json.loads(documents['prior-phase/result.json'])
        prior_observation=result['priorGlobalStoreObservation']
        require(prior_observation['preservation']==prior['preservation'] and prior_observation['before']==intervals[0][0], 'Prior failed preservation observation erased')
        folder=str(Path(correction['sourceBaseline']['path']).parent)
        current_before=observation(json.loads(documents[folder+'/certificate-stores-before.json']))
        current_after=observation(json.loads(documents[folder+'/certificate-stores-after.json']))
        require(prior_observation['current']==current_before, 'Correction store baseline differs from observed interval')
        require(result['preservation']['status']=='verified' and result['preservation']['errors']==[] and current_before==current_after, 'Independent preservation claim contains errors/drift')
        require(prior['preservation']['status']!='verified' and prior['preservation']['errors'] and intervals[0][0]!=intervals[0][1], 'Prior failed interval relabelled or inconsistent')
        intervals.append((current_before,current_after))
        require(result['qualification']=='incomplete' and result.get('firstFailure') is not None, 'Retained failed interval cannot grant qualification')
    if result.get('preservation',{}).get('status')=='verified' and not correction and intervals:
        require(result['preservation'].get('errors')==[] and all(left==right for left,right in intervals), 'Preservation claims verified over actual errors/drift')
    if result['qualification']=='passed':
        require(intervals and all(left==right for left,right in intervals) and result['preservation'].get('errors')==[] and result['historicalPreservation'].get('errors',[])==[], 'Passing preservation omits observations or retains errors/drift')


def verify_compiler(result,documents,inventory,workspace,model_pe):
    from capture_compiler import FILE_OPTIONS
    item=result['modelCompiler']
    require(inventory.get(item['path'])==item, 'Compiler report binding changed')
    reports=json.loads(documents[item['path']])
    require(len(reports)==1, 'Exactly one actual model compilation is required')
    report=reports[0];process=report['process']
    observations=json.loads(documents['processes/production-model.json'])
    require(process in observations, 'Compiler process is not an actual owned observation')
    sdk=result['environment']['DOTNET_HOST_PATH']
    executable=str(Path(sdk).parent)+'/sdk/10.0.403/Roslyn/bincore/csc'
    require(process['executable']==executable and process['argv'][0]==executable, 'Compiler executable substituted')
    cwd=result['productionMapping']['PlatformIdentity']['PlatformPath']+'/src/Hexalith.Platform.Composition'
    require(process['cwd']==cwd, 'Compiler project working directory changed')
    arguments=[];used=[]
    for argument in process['argv'][1:]:
        if argument.startswith('@'):
            name=argument[1:]
            matches=[row for row in process.get('responses',[]) if row['file']['path']==name]
            require(len(matches)==1 and private_path(name,workspace), 'Missing/foreign compiler response')
            response=matches[0];raw=response['text'].encode()
            require(len(raw)==response['file']['bytes'] and hashlib.sha256(raw).hexdigest()==response['file']['sha256'], 'Compiler response bytes changed')
            arguments.extend(shlex.split(response['text']));used.append(name)
        else:arguments.append(argument)
    require(len(used)==len(process.get('responses',[])) and len(used)==len(set(used)) and not any(arg.startswith('@') for arg in arguments), 'Incomplete/nested compiler response provenance')
    require(arguments==report['arguments'] and '/noconfig' in arguments, 'Expanded compiler arguments changed')
    outputs=[arg.split(':',1)[1] for arg in arguments if arg.startswith('/out:')]
    output=workspace+'/workspace/build/platform-model/obj/Hexalith.Platform.Composition/debug/Hexalith.Platform.Composition.dll'
    require(outputs==[output] and report['output']==output and model_pe['codeview']==[str(Path(output).with_suffix('.pdb'))], 'Compiler output is not the sealed model CodeView output')
    expected=[('compiler',executable)]
    for argument in arguments:
        if ':' not in argument:continue
        option,value=argument.split(':',1);option=option.lower()
        if option not in FILE_OPTIONS:continue
        if option in {'/resource','/linkresource'}:value=value.split(',',1)[0]
        values=value.split(',') if option in {'/analyzer','/addmodule'} else [value]
        for value in values:
            expected.append((FILE_OPTIONS[option],posixpath.normpath(value if value.startswith('/') else cwd+'/'+value)))
    for argument in arguments:
        if argument.startswith(('/reference:','/r:')):
            value=argument.split(':',1)[1]
            require(value.startswith('/') and ',' not in value, 'Unsupported compiler reference vector')
            expected.append(('reference',value))
        elif argument.endswith('.cs') and not re.match(r'^/[a-zA-Z]+:',argument):
            expected.append(('source',str(Path(argument) if Path(argument).is_absolute() else Path(cwd)/argument)))
    require([(row['kind'],row['file']['path']) for row in report['inputs']]==expected, 'Complete compiler source/reference/auxiliary inventory differs from actual arguments')
    sdk_bindings={row['path']:row for row in json.loads(documents['sdk-bindings.json'])}
    for row in report['inputs']:
        retained=row['retained'];source=row['file']
        require(inventory.get(retained['path'])==retained and source['sha256']==retained['sha256'] and source['bytes']==retained['bytes'], 'Compiler input byte binding missing')
        require(retained['path']=='compiler-bytes/'+source['sha256']+'/'+Path(source['path']).name, 'Noncanonical compiler retained path')
        if row['kind']=='compiler':require(sdk_bindings.get(source['path'])==source, 'Compiler differs from selected SDK')
    require({'source','reference','compiler'} <= {kind for kind,_ in expected}, 'Actual compiler inventory lacks required inputs')
    model=result['outputs']['Model']
    require(result['modelAssembly']['path']==workspace+'/workspace/hosts/platform-model/Hexalith.Platform.Composition.dll' and result['modelAssembly']['sha256']==model['sha256'] and result['modelAssembly']['bytes']==model['bytes'], 'Production model output byte identity differs from seal')


def verify_mapping_guards(result,documents,tooling,workspace):
    helper=workspace+'/artifacts/original/bin/Hexalith.Builds.Module.Cli/debug/Hexalith.Builds.Tooling.dll'
    for name in ('SourceMapping.targets','SourceMapping.props'):
        resource=tooling['resources'].get('Hexalith.Builds.Tooling.Workspace.'+name)
        require(resource is not None and documents['workspace/source-mapping/'+name]==resource.replace(b'__TOOLING_ASSEMBLY__',helper.encode()), 'Mapping resource bytes differ from executed Tooling assembly')
    mapping=result['productionMapping'];env=result['productionEnvironment'];folder=workspace+'/workspace/source-mapping/'
    condition="!$([System.String]::Copy('$(MSBuildProjectFullPath)').StartsWith('"+workspace+"/workspace/hosts/', System.StringComparison.Ordinal))"
    selection={'HexalithSourceMappingRoot':mapping['Root'],'Configuration':'Debug','UseHexalithProjectReferences':'true','UseNuGetDeps':'false'}
    guarded={'HexalithSourceMappingApplies':'true','HexalithSourceMappingHasGit':'true','HexalithSourceMappingMode':'source','HexalithSourceMappingHash':mapping['ContentHash'],'HexalithSourceMappingRoot':mapping['Root'],'_HXWObservedHexalithSourceMappingRoot':mapping['Root'],'HexalithSourceMappingActiveModule':mapping['ActiveModule'],'Configuration':'Debug','UseHexalithProjectReferences':'true','UseNuGetDeps':'false'}
    for entry in mapping['Entries']:
        suffix=re.sub('[^a-zA-Z0-9_]','',entry['Identity'].removeprefix('Hexalith.'))
        root='Hexalith'+suffix+'Root';origin='Hexalith'+suffix+'FromSource'
        selection[root]=entry['Path'];selection[origin]='true'
        guarded[root]=entry['Path'];guarded['_HXWObserved'+root]=entry['Path'];guarded[origin]='true'
        if suffix=='Tenants':
            selection['HexalithTenantsBasePath']=entry['Path']+'/src';guarded['HexalithTenantsBasePath']=entry['Path']+'/src';guarded['_HXWObservedHexalithTenantsBasePath']=entry['Path']+'/src'
        if suffix=='Commons':
            for name in ('HexalithCommonsHttpFromSource','HexalithCommonsServiceDefaultsFromSource'):selection[name]='true';guarded[name]='true'
    guarded.update({key:value for key,value in env.items() if key in {'CustomBeforeDirectoryBuildProps','CustomBeforeDirectoryBuildTargets','CustomAfterDirectoryBuildTargets'}})
    targets=ET.fromstring(documents['workspace/source-mapping/SourceMapping.values.targets'])
    require(targets.tag=='Project' and targets.get('InitialTargets')=='HexalithVerifySourceMappingSelection;HexalithValidateSourceProjectsBeforeRestore', 'Mapping initial guard schedule changed')
    by_name={node.get('Name'):node for node in targets.findall('Target')}
    required={'HexalithBindMappedRootSubmodules','HexalithVerifySourceMappingSelection','HexalithVerifyPreLateSourceMappingSelection','HexalithReconcileSourceMapping','HexalithValidateSourceProjectsBeforeRestore','HexalithValidateSourceMapping'}
    require(set(by_name)==required and len(targets)==len(required) and len(targets.findall('Target'))==len(required) and all(node.get('Condition')==condition for node in by_name.values()), 'Mapping executable target selection changed')
    expected_conditions={"'$("+key+")' != '"+value+"'" for key,value in guarded.items()}
    observed={'_HXWObservedConfiguration':'Debug','_HXWObservedUseHexalithProjectReferences':'true','_HXWObservedUseNuGetDeps':'false'}
    observed.update({'_HXWObserved'+key:value for key,value in selection.items() if key.endswith('FromSource')})
    expected_conditions.update("'$("+key+")' != '' and '$("+key+")' != '"+value+"'" for key,value in observed.items())
    expected_conditions.add("!$([System.String]::Copy(';$(AfterMicrosoftNETSdkTargets);').Contains(';"+folder+"SourceMapping.final.targets;', System.StringComparison.Ordinal))")
    expected_conditions.add("'$(IsCrossTargetingBuild)' != 'true' and '$(_HXWFinalTargetsImported)' != 'true'")
    for name in ('HexalithVerifySourceMappingSelection','HexalithReconcileSourceMapping','HexalithValidateSourceProjectsBeforeRestore','HexalithValidateSourceMapping'):
        errors=by_name[name].findall('Error')
        conditions={row.get('Condition') for row in errors if row.get('Text','').startswith('HXW006:')}
        require(conditions==expected_conditions and len(errors)==len(conditions), 'Executable mapping property/import guards removed or changed')
    pre=by_name['HexalithVerifyPreLateSourceMappingSelection']
    preconditions={node.get('Condition') for node in pre.findall('Error') if node.get('Text','').startswith('HXW006:')}
    require(all("'$(_HXWPreLateSelectionCaptured)' == 'true' and '$(_HXWPreLate"+key+")' != '"+value+"'" in preconditions for key,value in selection.items()), 'Pre-late executable mapping guards removed')
    for name in ('HexalithReconcileSourceMapping','HexalithValidateSourceProjectsBeforeRestore','HexalithValidateSourceMapping'):
        require({'Restore','CoreCompile'} & set(by_name[name].get('BeforeTargets','').split(';')), 'Mapping validation lost scheduling')
    reconcile=by_name['HexalithReconcileSourceMapping'].find('SourceMappingReconciliationTask')
    require(reconcile is not None and reconcile.get('MappingFile')=='$(MSBuildThisFileDirectory)mapping.json' and reconcile.get('MappingHash')=='$(HexalithSourceMappingHash)' and reconcile.get('Projects')=='@(ProjectReference)' and reconcile.get('Packages')=='@(PackageReference)', 'Mapping reconciliation task changed')
    for name,projects_only in [('HexalithValidateSourceProjectsBeforeRestore','true'),('HexalithValidateSourceMapping',None)]:
        task=by_name[name].find('SourceMappingValidationTask')
        require(task is not None and task.get('MappingFile')=='$(MSBuildThisFileDirectory)mapping.json' and task.get('MappingHash')=='$(HexalithSourceMappingHash)' and task.get('ProjectsOnly')==projects_only and task.get('Projects')=='@(ProjectReference)' and task.get('Packages')=='@(PackageReference)', 'Mapping validation task removed/changed')
    final=ET.fromstring(documents['workspace/source-mapping/SourceMapping.final.targets'])
    require(len(final)==2 and final.findtext('PropertyGroup/_HXWFinalTargetsImported')=='true', 'Final import executable marker absent')
    final_target=final.find('Target')
    final_values={key:value for key,value in guarded.items() if key in {'HexalithSourceMappingApplies','HexalithSourceMappingMode','HexalithSourceMappingHash','HexalithSourceMappingRoot','Configuration','UseHexalithProjectReferences','UseNuGetDeps'} or (key.startswith('Hexalith') and (key.endswith('FromSource') or key.endswith('Root') or key=='HexalithTenantsBasePath'))}
    require(final_target is not None and {node.get('Condition') for node in final_target.findall('Error') if node.get('Text','').startswith('HXW006:')}=={"'$("+key+")' != '"+value+"'" for key,value in final_values.items()}, 'Final executable property guards removed')
    require(final_target is not None and final_target.get('Name')=='HexalithValidateFinalSourceProjects' and final_target.get('Condition')==condition and final_target.find('SourceMappingValidationTask') is not None and 'ResolveProjectReferences' in final_target.get('BeforeTargets','').split(';'), 'Final executable source boundary removed')


def verify_complete(result, documents, inventory, workspace):
    mapping = verify_authority(result, documents, inventory)
    require(result["sdkVersion"] == "10.0.403" and documents["logs/sdk-version.log"].strip() == b"10.0.403", "Wrong installed SDK")
    source_rows = json.loads(documents["source-baseline.json"])
    require(source_rows and len({row["path"] for row in source_rows}) == len(source_rows) == result["preservation"]["boundSourceCount"], "Source inventory mismatch")
    require(result["preservation"]["repositoryCount"] == 13, "Repository preservation count mismatch")
    require(result["historicalPreservation"]["files"] == len(json.loads(documents["historical-baseline.json"])), "Historical preservation count mismatch")
    require(len(result["protectedCopies"]) == 4, "Protected authority bytes absent")
    protected = []
    for item in result["protectedCopies"]:
        retained = item["retained"]
        require(inventory.get(retained["path"]) == retained and retained["sha256"] == item["source"]["sha256"] and retained["bytes"] == item["source"]["bytes"], "Protected authority byte drift")
        protected.append(documents[retained["path"]])
    require(re.search(rb"^status: ['\"]?in-progress['\"]?\s*$", protected[0], re.MULTILINE), "Parent status changed")
    require(re.search(rb"^\s+1-17-publish-the-pd10-v2-authorization-contract-spine: backlog\s*$", protected[1], re.MULTILINE), "Tracker status changed")
    for raw in protected[2:]:
        controls = json.loads(raw)["productionControl"]
        require(controls["routeMode"] == "V1Only" and controls["T0"] is None and controls["activationAuthority"] is None, "Activation authority changed")
    checks = json.loads(protected[3])["checks"]
    require(len(checks) == 14 and all(row["status"] == "unexecuted" and row["result"] is None for row in checks), "Staging activity changed")
    require(result["apiInputBindings"], "Unbound API producer")
    for item in result["apiInputBindings"]:
        retained = inventory[item["retainedPath"]]
        require(retained["sha256"] == item["source"]["sha256"] and retained["bytes"] == item["source"]["bytes"], "Executed API/CLI input drift")
    role_names=dict(CLI='Hexalith.Builds.Module.Cli', Tooling='Hexalith.Builds.Tooling', AppHost='Hexalith.Builds.Module.AppHost', EventStoreHost='Hexalith.Builds.Module.EventStoreHost', UiHost='Hexalith.Builds.Module.UiHost', Model='Hexalith.Platform.Composition')
    require(set(result['outputs'])==set(role_names), 'Missing or unexpected production output role')
    actual_pe={name:pe_data(raw) for name,raw in documents.items() if name.startswith('distribution/') and name.endswith('.dll') and Path(name).name.startswith('Hexalith.')}
    for role,name in role_names.items():
        canonical='distribution/'+role+'/'+name+'.dll'
        require(result['outputs'][role]['path']==canonical and inventory.get(canonical)==result['outputs'][role] and actual_pe[canonical]['identity']['name']==name, 'Canonical output role/actual assembly identity changed')
    reported=json.loads(documents['pe-metadata.json'])
    by_path={row['path']:row for row in reported}
    require(len(by_path)==len(reported) and set(by_path)=={workspace+'/'+name for name in actual_pe}, 'PE metadata inventory differs from actual retained assemblies')
    for name,metadata in actual_pe.items():
        require(by_path[workspace+'/'+name]==dict(path=workspace+'/'+name,**{key:metadata[key] for key in ('identity','references','mvid')}), 'Reported PE identity/reference/MVID differs from retained file')
    tooling=actual_pe['distribution/Tooling/Hexalith.Builds.Tooling.dll']
    for role in ('Helper','CLI','AppHost','Model'):
        copy=inventory['distribution/'+role+'/Hexalith.Builds.Tooling.dll']
        require(copy['sha256']==result['outputs']['Tooling']['sha256'] and copy['bytes']==result['outputs']['Tooling']['bytes'], 'Executed/model/selected Tooling copies differ')
    verify_mapping_guards(result,documents,tooling,workspace)
    original_tests=actual_pe['distribution/Tests/Hexalith.Builds.Module.Tests.dll']
    selections=test_selection(original_tests,tooling)
    source_authority={row['path']:row for row in source_rows}
    test_processes=json.loads(documents['processes/original-test-build.json'])
    test_compilers=[]
    for process in test_processes:
        if Path(process['argv'][0]).name!='csc':continue
        arguments=[]
        for argument in process['argv'][1:]:
            if argument.startswith('@'):
                responses=[row for row in process.get('responses',[]) if row['file']['path']==argument[1:]]
                require(len(responses)==1, 'Test compiler response absent')
                response=responses[0];raw=response['text'].encode()
                require(len(raw)==response['file']['bytes'] and hashlib.sha256(raw).hexdigest()==response['file']['sha256'], 'Test compiler response bytes changed')
                arguments.extend(shlex.split(response['text']))
            else:arguments.append(argument)
        if '/out:'+workspace+'/artifacts/original/obj/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll' in arguments:test_compilers.append((process,arguments))
    require(len(test_compilers)==1 and original_tests['codeview']==[workspace+'/artifacts/original/obj/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.pdb'], 'Original test assembly not bound to actual compiler output')
    test_process,test_arguments=test_compilers[0]
    for full in REQUIRED_CLASSES:
        relative='references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/'+full.removeprefix(FULL).replace('.','/')+'.cs'
        require(relative in source_authority and re.fullmatch('[a-f0-9]{64}',source_authority[relative]['sha256']) and any(posixpath.normpath(arg if arg.startswith('/') else test_process['cwd']+'/'+arg)==mapping['Root']+'/'+relative for arg in test_arguments if arg.endswith('.cs')), 'Required original test source/assembly authority absent')
    verify_compiler(result,documents,inventory,workspace,actual_pe[result['outputs']['Model']['path']])
    child = result["modelChild"]
    model_observations = json.loads(documents["processes/production-model.json"])
    require(sum(row == child for row in model_observations) == 1, "Production child is not in actual observation")
    helper_tooling = workspace + "/artifacts/original/bin/DistributionProbe/debug/Hexalith.Builds.Tooling.dll"
    verify_child(child, mapping, workspace, result["environment"]["DOTNET_HOST_PATH"], helper_tooling, result["environment"]["HOME"])
    require(all(child["environment"].get(key) == value for key, value in result["productionEnvironment"].items()), "Production mapping environment lost")
    require(child["environment"]["HOME"] == result["environment"]["HOME"], "Child HOME changed")
    early = ET.fromstring(documents["workspace/source-mapping/SourceMapping.model-common.props"])
    require(early.tag == "Project" and not early.attrib and len(early) == 2 and all(node.tag == "Import" and set(node.attrib) == {"Project"} for node in early), "Modified production early import")
    require([node.get("Project") for node in early] == [workspace + "/workspace/source-mapping/pinned-tree/Props/Directory.Packages.props", "$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props"], "Wrong genuine import order")
    preparation = json.loads(documents["logs/prepare-build.log"])
    require(preparation == result["preparation"] and preparation["status"] == "prepared" and preparation["buildPreparation"]["environment"] == result["productionEnvironment"], "False preparation authority")
    runner = inventory.get("distribution/Tests/Hexalith.Builds.Module.Tests.dll")
    require(runner is not None and runner["sha256"] == result["originalRunner"]["sha256"] and runner["bytes"] == result["originalRunner"]["bytes"], "Original runner byte identity missing")
    relocation = result["runnerRelocation"]["files"]
    require(relocation and any(row['destination']==result['originalRunner'] and row['source']['path']==workspace+'/artifacts/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll' for row in relocation), 'Real original runner source/location binding absent')
    require(all(row["source"]["sha256"] == row["destination"]["sha256"] and row["source"]["bytes"] == row["destination"]["bytes"] for row in relocation), "Runner relocation changed bytes")
    reports = result["testClasses"]
    require({row["name"] for row in reports} == REQUIRED_CLASSES and len(reports) == len(REQUIRED_CLASSES), "Required original class missing")
    total = 0
    for row in reports:
        discovery = result["classDiscoveries"][row["name"]]
        require(inventory.get(discovery["file"]["path"]) == discovery["file"] and json.loads(documents[discovery["file"]["path"]]) == discovery["cases"], "Discovery binding mismatch")
        require(row["total"] == row["passed"] == row["discovered"] == len(discovery["cases"]) > 0 and all(row[key] == 0 for key in ("failed", "errors", "skipped", "notRun")), "Incomplete original class")
        require(all(case["Assembly"] == result["originalRunner"]["path"] and case['Class']==row['name'] and not case.get("Explicit") and not case.get("SkipReason") and re.fullmatch('[a-f0-9]{64}',case['ID']) for case in discovery["cases"]), "Wrong discovery identity")
        require(len({case['ID'] for case in discovery['cases']})==len(discovery['cases']) and {(case['Method'],case['DisplayName']) for case in discovery['cases']}==selections[row['name']], 'Original fact/theory selection reduced or changed')
        require(inventory.get(row["report"]["path"]) == row["report"], "Test report binding mismatch")
        report_root=ET.fromstring(documents[row["report"]["path"]])
        require(len(report_root.findall('assembly'))==1 and not report_root.findall('.//error') and not report_root.findall('.//failure'), 'XML contains extra assembly/error/failure')
        assembly = report_root.find("assembly")
        require(assembly is not None and int(assembly.get("total")) == row["total"] and int(assembly.get("passed")) == row["total"], "Original XML denominator mismatch")
        require(assembly.get('name')==result['originalRunner']['path'], 'XML names another test assembly')
        for collection in assembly.findall('collection'):
            collection_tests=collection.findall('test')
            require(int(collection.get('total','-1'))==len(collection_tests) and int(collection.get('passed','-1'))==len(collection_tests) and all(int(collection.get(key,'0' if key=='errors' else '-1'))==0 for key in ('errors','failed','skipped','not-run')), 'XML collection counters changed')
        tests = assembly.findall(".//test")
        require(len(tests)==row['total'] and all(int(assembly.get(key,'-1'))==0 for key in ('errors','failed','skipped','not-run')), 'XML failed/errors/skipped/not-run accounting changed')
        require(all(test.get("result") == "Pass" and test.get('type')==row['name'] for test in tests), "Nonpassing original case")
        verify_case_identities(tests, discovery["cases"])
        total += row["total"]
    sdk_cases = result["classDiscoveries"][FULL + "CompositionSdkProcessTests"]["cases"]
    require(any(case["Method"] == "ProductionBareDotnetChildUsesExplicitControlsAsync" for case in sdk_cases), "Fresh production SDK assertion absent")
    require(not any(case["Method"] == "QualificationCatalogProbeInterceptionIsEstablishedAsync" for case in sdk_cases)
            and result["excludedHistoricalInterception"]["credit"] is False, "Historical interception grants current credit")
    for label in ("modelAssets", "originalAssets"):
        require(result[label], "Restored assets absent")
        for row in result[label]:
            require(inventory.get(row["retained"]["path"]) == row["retained"] and not row["hexalithPackages"], "Unbound/fallback source assets")
            assets = json.loads(documents[row["retained"]["path"]])
            require(not any(name.lower().startswith("hexalith.") and item["type"] == "package" for name, item in assets["libraries"].items()), "Hexalith package fallback")
    seal = json.loads(documents["output-seal.json"])
    sealed = {row["path"]: row for row in seal}
    require(len(sealed) == len(seal) and all(inventory.get(path) == row for path, row in sealed.items()), "Output seal drift")
    require(set(result["outputs"]) == {"CLI", "Tooling", "AppHost", "EventStoreHost", "UiHost", "Model"}, "Missing or unexpected production output role")
    for role, item in result["outputs"].items():
        require(sealed.get(item["path"]) == item, "Selected output drift")
        managed_pe(documents[item["path"]])
    metadata = {row["path"]: row for row in json.loads(documents["pe-metadata.json"])}
    references = result["modelReferenceIdentities"]
    require(references["model"] == workspace + "/" + result["outputs"]["Model"]["path"], "Wrong PE model")
    require(metadata[references["model"]]["references"] == references["references"], "PE reference evidence mismatch")
    expected = [dict(reference=item, output=str(Path(references["model"]).parent / (item["name"] + ".dll"))) for item in references["references"] if item["name"].startswith("Hexalith.")]
    require(expected and references["hexalithMatches"] == expected, "Partial Hexalith PE reference credit")
    for item in expected:
        relative = str(Path(item["output"]).relative_to(workspace))
        require(relative in sealed and metadata[item["output"]]["identity"] == item["reference"], "Incompatible source dependency identity")
    return total


def verify_correction(result, documents, inventory):
    """Only a same-tuple runner-location correction may reuse production observations."""
    correction = result.get("currentCorrection")
    require(bool(correction)==any(name.startswith('prior-phase/') for name in documents), 'Retained prior failure cannot be detached from correction scope')
    if not correction:
        require('priorGlobalStoreObservation' not in result, 'Prior failed interval detached from retained scope')
        return
    require(correction["kind"] == "runner-location-only" and correction["productionCommandsReexecuted"] == 0, "Unknown continuation scope")
    prior = json.loads(documents["prior-phase/receipt.json"])
    require(prior["result"] == json.loads(documents["prior-phase/result.json"]), "Prior result changed")
    prior_members_raw = documents["prior-phase/members.json"]
    require(hashlib.sha256(prior_members_raw).hexdigest() == prior["memberManifest"]["sha256"] and len(prior_members_raw) == prior["memberManifest"]["bytes"], "Prior member manifest changed")
    for item in json.loads(prior_members_raw):
        alias = "prior-phase/" + item["path"] if item["path"] in {"result.json", "commands.json"} else item["path"]
        require(alias in inventory and inventory[alias]["sha256"] == item["sha256"] and inventory[alias]["bytes"] == item["bytes"], "Preserved prior-phase bytes changed: " + alias)
    old = {row["path"]: row for row in json.loads(documents["source-baseline.json"])}
    current = {row["path"]: row for row in json.loads(documents[correction["sourceBaseline"]["path"]])}
    require(set(old) == set(current), "Source inventory changed during location correction")
    changes = [dict(before=old[name], after=current[name]) for name in sorted(old) if old[name] != current[name]]
    require(changes == correction["sourceChanges"] and all(item["before"]["path"] in {"eng/story117-source-distribution/qualify_model.py", "eng/story117-source-distribution/verify_model.py"} for item in changes), "Production/test source changed during location correction")
    for key in ("productionMapping", "productionEnvironment", "modelChild", "modelAssembly", "modelCompiler", "modelAssets", "apiInputBindings", "initialRepositories"):
        require(result[key] == prior["result"][key], "Reused production observation changed: " + key)
    prior_commands = prior["result"]["commands"]
    require(result["commands"][:len(prior_commands)] == prior_commands and all(row["exitCode"] == 0 and not row["timedOut"] for row in prior_commands), "Reused command ledger changed or contains failure")
    require({"production-model", "original-test-build"} <= {row["label"] for row in prior_commands}, "Required completed production gates absent")


def replay(receipt_path):
    receipt_path = Path(receipt_path)
    receipt = json.loads(receipt_path.read_text())
    base = receipt_path.parent
    archive = bound(base, receipt["archive"], "Archive")
    members = json.loads(bound(base, receipt["memberManifest"], "Manifest").read_text())
    inventory = {row["path"]: row for row in members}
    require(len(inventory) == len(members) == receipt["memberCount"], "Manifest count mismatch")
    documents, seen = {}, set()
    with tarfile.open(archive, "r:gz") as capsule:
        for member in capsule:
            safe_path(member.name)
            require(member.isfile() and member.name not in seen and member.name in inventory, "Unexpected/duplicate/linked member")
            seen.add(member.name)
            item = inventory[member.name]
            require(member.size == item["bytes"], "Member size mismatch")
            value = hashlib.sha256()
            keep = (member.name.endswith((".json", ".xml", ".props", ".targets")) or member.name.startswith("protected/") or member.name.startswith("git-objects/")
                    or (Path(member.name).name.startswith("discover-") and member.name.endswith(".log")) or member.name in {"logs/prepare-build.log", "logs/sdk-version.log", "logs/production-model.log"}
                    or (member.name.startswith("distribution/") and member.name.endswith(".dll") and Path(member.name).name.startswith("Hexalith.")))
            chunks = []
            with capsule.extractfile(member) as stream:
                while chunk := stream.read(1024 * 1024):
                    value.update(chunk)
                    if keep:
                        chunks.append(chunk)
            require(value.hexdigest() == item["sha256"], "Member SHA-256 mismatch: " + member.name)
            if keep:
                documents[member.name] = b"".join(chunks)
    require(seen == set(inventory), "Missing archive member")
    result = json.loads(documents["result.json"])
    require(result == receipt["result"], "Receipt/result mismatch")
    require(result["protectedAuthority"] == AUTHORITY, "Protected authority changed")
    require(all(result[key] == "pending" for key in ("ordinaryStartup", "signedDistribution", "operationalAcceptance")), "False startup/release/operational credit")
    require(result["qualification"] in {"passed", "incomplete"}, "Unknown outcome")
    require(result["historicalEvidence"]["status"] == "not reused", "Historical evidence used as current authority")
    if result.get("commands"):
        verify_commands(result, documents, inventory, receipt["privateWorkspace"])
    verify_correction(result, documents, inventory)
    verify_preservation(result, documents)
    total = 0
    if result["qualification"] == "passed":
        require(result["firstFailure"] is None and result["preservation"]["status"] == result["historicalPreservation"]["status"] == "verified", "Passing preservation claim is false")
        total = verify_complete(result, documents, inventory, receipt["privateWorkspace"])
    else:
        require(result.get("firstFailure") is not None or result["preservation"]["status"] != "verified", "Incomplete evidence has no failed gate")
        if result.get("independentDiagnosticGates") == "passed":
            require(result.get("currentCorrection", {}).get("kind") == "runner-location-only", "Unbound independent diagnostic claim")
            total = verify_complete(result, documents, inventory, receipt["privateWorkspace"])
    return dict(qualification=result["qualification"], verifiedMembers=len(seen), originalAssemblyTests=total, commandExecutionCount=0,
                startupCredit=False, runtimeCredit=False, acceptedToolCredit=False, signedReleaseCredit=False, operationalCredit=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", required=True, type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(replay(args.receipt)))
        return 0
    except (ValueError, KeyError, OSError, TypeError, IndexError, struct.error, UnicodeError, tarfile.TarError, ET.ParseError) as error:
        print("Production model replay refused: " + str(error))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
