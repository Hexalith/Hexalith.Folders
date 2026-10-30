"""Bind direct compiler auxiliary inputs from already-retained Csc diagnostics; never execute commands."""
import hashlib
import json
from pathlib import Path
import re
import shlex
import shutil

FILE_OPTIONS = {
    "/analyzer": "analyzer", "/additionalfile": "additional-file", "/analyzerconfig": "analyzer-config",
    "/resource": "embedded-resource", "/linkresource": "linked-resource", "/embed": "embedded-source",
    "/sourcelink": "source-link", "/appconfig": "application-config", "/ruleset": "ruleset", "/keyfile": "keyfile",
    "/win32res": "win32-resource", "/win32icon": "win32-icon", "/win32manifest": "win32-manifest", "/addmodule": "module",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def binding(path, base=None):
    path = Path(path)
    require(path.is_file() and not path.is_symlink(), "Unavailable compiler input: " + str(path))
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return dict(path=str(path.relative_to(base) if base else path), bytes=path.stat().st_size, sha256=digest.hexdigest())


def compiler_command(line):
    if "/noconfig" not in line and "@" not in line:
        return None
    text = re.sub(r"\s+\(TaskId:\d+\)\s*$", "", line.strip())
    if not text.startswith(("/", '"')):
        return None
    words = shlex.split(text)
    if not words:
        return None
    parsed = None
    if Path(words[0]).name in {"csc", "csc.exe"}:
        parsed = words[0], words[1:]
    if len(words) > 3 and Path(words[0]).name in {"dotnet", "dotnet.exe"} and words[1] == "exec" and Path(words[2]).name == "csc.dll":
        parsed = words[2], words[3:]
    if parsed:
        require(not any(argument.startswith("@") for argument in parsed[1]), "Unsupported compiler response-file vector; complete inputs cannot be inventoried")
        return parsed if "/noconfig" in parsed[1] else None
    return parsed


def project_for_output(arguments, asset_projects):
    output = next((item.split(":", 1)[1] for item in arguments if item.lower().startswith("/out:")), None)
    require(output and Path(output).is_absolute(), "Compiler output must have an absolute artifacts path")
    output = Path(output)
    matches = [(directory, project) for directory, project in asset_projects if output.is_relative_to(directory)]
    require(bool(matches), "No restored project owns compiler output: " + str(output))
    directory, project = max(matches, key=lambda item: len(item[0].parts))
    return project, output


def auxiliary_inputs(compiler, arguments, project):
    require(not any(argument.startswith("@") for argument in arguments), "Unsupported compiler response-file vector; complete inputs cannot be inventoried")
    rows = [dict(kind="compiler", path=str(Path(compiler).resolve()))]
    for argument in arguments:
        if ":" not in argument:
            continue
        option, value = argument.split(":", 1)
        option = option.lower()
        if option not in FILE_OPTIONS:
            continue
        if option in {"/resource", "/linkresource"}:
            value = value.split(",", 1)[0]
        values = value.split(",") if option in {"/analyzer", "/addmodule"} else [value]
        for value in values:
            path = Path(value)
            if not path.is_absolute():
                path = project.parent / path
            rows.append(dict(kind=FILE_OPTIONS[option], path=str(path.resolve())))
    return rows


def capture(work):
    work = Path(work)
    result = json.loads((work / "result.json").read_text())
    artifacts = Path(result["privateArtifacts"])
    asset_projects = []
    for root in (artifacts, work / "workspace/build/platform-model"):
        for path in root.rglob("project.assets.json"):
            document = json.loads(path.read_text())
            project = Path(document["project"]["restore"]["projectPath"])
            asset_projects.append((path.parent, project))
    logs = [work / "logs" / name for name in ("bootstrap-build.log", "original-debug-build.log", "current-api-helper-build.log")]
    model_child = result.get("modelChildControls")
    if model_child:
        logs.append(Path(model_child["log"]))
    commands = []
    inputs = {}
    for log in logs:
        if not log.is_file():
            continue
        log_binding = binding(log, work)
        with log.open(errors="strict") as stream:
            for number, line in enumerate(stream, 1):
                parsed = compiler_command(line)
                if parsed is None:
                    continue
                compiler, arguments = parsed
                project, output = project_for_output(arguments, asset_projects)
                selected = []
                for row in auxiliary_inputs(compiler, arguments, project):
                    item = binding(row["path"])
                    destination = work / "compiler-auxiliary-bytes" / item["sha256"] / Path(item["path"]).name
                    if not destination.exists():
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(item["path"], destination)
                    retained = binding(destination, work)
                    require(retained["bytes"] == item["bytes"] and retained["sha256"] == item["sha256"], "Compiler input copy mismatch")
                    value = dict(kind=row["kind"], file=item, retained=retained)
                    inputs[(row["kind"], item["path"])] = value
                    selected.append(value)
                commands.append(dict(log=log_binding, line=number, compiler=compiler, arguments=arguments,
                                     project=str(project), output=str(output), inputs=selected))
    require(commands and inputs, "No actual compiler invocations were captured")
    if result.get("qualification") == "passed":
        require(any(Path(row["project"]).stem == "Hexalith.Platform.Composition" for row in commands), "Actual model compiler invocation absent")
    source_result = work / "compiler-auxiliary-source-result.json"
    if not source_result.exists():
        shutil.copy2(work / "result.json", source_result)
    report = dict(schemaVersion=1, scope="Read-only post-execution audit of direct Csc auxiliary file inputs; source/reference captures and complete original diagnostics remain separate; commands are never executed",
                  commands=commands, inputs=list(inputs.values()), sourceResult=binding(source_result, work))
    path = work / "compiler-auxiliary-inputs.json"
    path.write_text(json.dumps(report, indent=2) + "\n")
    return report
