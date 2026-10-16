"""Prepare engine-independent input from the committed Unity 4.6 HERO recording."""
import gzip
import json
import pathlib
import sys

source, target = map(pathlib.Path, sys.argv[1:3])
floor = False
count = 0
with gzip.open(source, "rt") as rows, target.open("w") as out:
    def write(*values):
        out.write(" ".join(str(int(v)) if isinstance(v, bool) else str(v) for v in values)+"\n")
    for line in rows:
        r = json.loads(line)
        phase = r.get("phase")
        if phase == "reset":
            floor = r.get("floor", False)
            write("reset", *r["position"], *r["velocity"], r.get("groundedBefore",False), r.get("gas",75),floor)
        elif phase == "command" and r.get("name") in ("dash","jump"):
            write(r["name"])
        elif phase == "set-velocity": write("velocity",*r["velocity"])
        elif phase == "hook-command":write("hook",*r["target"],*r["muzzle"])
        elif phase in ("frame.before","fixed.before"):
            write("frame" if phase == "frame.before" else "physics", r["dt"], r.get("driverMoveY",0),r.get("driverJump",False),
                  r.get("leftHeld",r.get("driverLeft",False)) if phase == "fixed.before" else r.get("driverLeft",False),
                  r.get("groundProbe",False),r.get("frontProbe",False),r.get("upperClear",False),*r["look"])
        elif phase == "observed":
            write("observed",*r["position"],*r["velocity"],r.get("gas",-1),r["scenario"],r["tick"])
            count += 1
        elif phase == "complete":
            expected=r.get("observations",r.get("scenarios",0)*30)
            if count != expected or count == 0:raise SystemExit("Incomplete Unity fixture")
            write("complete",count)
        elif phase == "error":raise SystemExit("Fixture contains an error")
print(f"Prepared {count} original-player observations")
