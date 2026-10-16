"""Compare independent C# and C++ traces, with exact discrete state/event checks."""
import csv
import itertools
import json
import math
import pathlib
import sys
import struct

path = pathlib.Path(sys.argv[1])
names = "row skill scale equipment route frame px py pz vx vy vz gas blade spare state grounded yaw targetYaw hitbox clip clipTime clipSpeed revision skillCooldown flareCooldown leftPhase rightPhase lx ly lz rx ry rz leftLoaded rightLoaded leftReserve rightReserve pitch roll leaning leanLeft cameraX cameraY cameraZ cameraYaw cameraPitch cameraRoll leftRopeCount rightRopeCount smokeCount smokeAge smokeSize smokeX events hits sounds physicsTicks".split()
exact = {0, 1, 3, 4, 5, 14, 15, 16, 19, 20, 23, 26, 27, 34, 35, 36, 37, 40, 41, 48, 49, 50, 54, 55, 56, 57}
maximum = {}
failures = []
failed_count = 0
rows = 0
float32_differences = 0
with (path / "csharp.tsv").open(newline="") as a, (path / "cpp.tsv").open(newline="") as b:
    for row, (expected, actual) in enumerate(itertools.zip_longest(csv.reader(a, delimiter="\t"), csv.reader(b, delimiter="\t"))):
        if expected is None or actual is None:
            raise SystemExit("Trace lengths differ")
        if len(expected) != len(names) or len(actual) != len(names):
            raise SystemExit(f"Invalid column count at {row}: {len(expected)}, {len(actual)} vs {len(names)}")
        rows += 1
        for column, (x, y) in enumerate(zip(expected, actual)):
            name = names[column]
            if column in exact:
                good = x == y
                error = None
            else:
                x, y = (struct.unpack('f',struct.pack('f',float(v)))[0] for v in (x,y))
                error = abs(x-y)
                float32_differences += x != y
                maximum[name] = max(maximum.get(name, 0.0), error)
                # Input scale is binary32, printed differently by the two standard libraries.
                tolerance = 0.0002 + 0.000002 * max(abs(x), abs(y))
                good = math.isfinite(x) and math.isfinite(y) and error <= tolerance
            if not good:
                failed_count += 1
                if len(failures) < 30:
                    failures.append({"row": row, "scenario": expected[:6], "field": name, "csharp": x, "cpp": y, "error": error})
report = {"passed": failed_count == 0, "frames": rows, "columns": len(names), "comparisons": rows*len(names), "failedComparisons": failed_count, "differentFloat32Values":float32_differences, "maxAbsoluteError": maximum, "firstFailures": failures, "tolerance": "exact discrete fields; floats rounded back to binary32, abs <= 0.0002 + 0.000002 * max(abs(values))"}
(path/"report.json").write_text(json.dumps(report, indent=2)+"\n")
print(json.dumps({k: report[k] for k in ("passed", "frames", "comparisons", "failedComparisons", "firstFailures")}, indent=2))
sys.exit(0 if report["passed"] else 1)
