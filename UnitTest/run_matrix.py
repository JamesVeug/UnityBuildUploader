"""Run EditMode smoke tests in separate Unity processes. Python 3.8+, standard library only."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


def run(config_path, output_root):
    config = json.loads(config_path.read_text(encoding="utf-8-sig"))
    output_root.mkdir(parents=True, exist_ok=True)
    results = []
    for job in config["jobs"]:
        unity = Path(job["unity"]).expanduser().resolve()
        project = Path(job["project"]).expanduser().resolve()
        if not unity.is_file() or not (project / "Assets").is_dir():
            raise ValueError("Each job needs an existing Unity executable and Unity project: " + job["label"])
        # Smoke runs once per editor, then each requested target gets a fresh editor process.
        runs = [("services", None, "BuildUploader.UnitTest.Core;BuildUploader.UnitTest.Steam;BuildUploader.UnitTest.Discord;BuildUploader.UnitTest.Itchio", None)]
        runs += [(target["name"], target["name"], "BuildUploader.UnitTest.Builds", target.get("profile"))
                 for target in job["targets"]]
        for name, target, assemblies, profile in runs:
            safe_label = "".join(c if c.isalnum() or c in "._-" else "_" for c in job["label"])
            directory = output_root / safe_label / name
            directory.mkdir(parents=True, exist_ok=True)
            xml_path = directory / "results.xml"
            # Use a fresh directory per invocation so stale XML can never report a pass.
            if xml_path.exists():
                raise ValueError("Output already exists; choose a fresh --output: " + str(xml_path))
            command = [str(unity), "-batchmode", "-projectPath", str(project), "-runTests",
                       "-testPlatform", "EditMode", "-assemblyNames", assemblies,
                       "-testResults", str(xml_path), "-logFile", str(directory / "editor.log")]
            if target:
                command += ["-buildTarget", target]
            environment = os.environ.copy()
            if profile:
                environment["BUILDUPLOADER_TEST_BUILD_PROFILE"] = profile
            else:
                environment.pop("BUILDUPLOADER_TEST_BUILD_PROFILE", None)
            print("Running", job["label"], name, flush=True)
            try:
                process = subprocess.run(command, env=environment, timeout=job.get("timeoutSeconds", 3600))
                exit_code = process.returncode
            except subprocess.TimeoutExpired:
                exit_code = -1
            status = "failed"
            cases = []
            if xml_path.exists():
                tree = ET.parse(xml_path)
                cases = [{"name": case.get("fullname"), "result": case.get("result")}
                         for case in tree.iter("test-case")]
                failures = any(case["result"] == "Failed" for case in cases)
                if exit_code == 0 and cases and not failures:
                    passed = sum(case["result"] == "Passed" for case in cases)
                    # Service suite requires both tests, target suite requires one actual player build.
                    expected = 3 if target is None else 1
                    status = "passed" if passed >= expected else "incomplete"
            results.append({"editor": job["label"], "run": name, "status": status,
                            "exitCode": exit_code, "tests": cases, "artifacts": str(directory)})
            (output_root / "matrix.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
            print(status.upper(), job["label"], name, flush=True)
    return 0 if results and all(result["status"] == "passed" for result in results) else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("config", type=Path)
    parser.add_argument("--output", type=Path, required=True, help="Fresh directory for test XML and editor logs")
    args = parser.parse_args()
    try:
        sys.exit(run(args.config.resolve(), args.output.resolve()))
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        parser.exit(1, str(error) + "\n")
