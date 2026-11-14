"""Turn a dump of the Level Lab database into the content/ folder the game downloads, and check it.

usage: python3 publish.py <dump dir> <repo root> [--version N]
       python3 publish.py --pin <commit sha> <repo root>

<dump dir> holds one JSON file per database document, as the ArtifactData tool saves them with out_dir:
  meta/manifest.json, groups/<id>.json, orders/<group>-<k>.json, levels/<group>-<serial>.json, shapes/<id>.json
<repo root>/content/ is replaced with the same layout as Level Lab's "Export zip":
  manifest.json, groups/<id>.json, orders/<group>/<k>.json, levels/<group>/<serial4>.json, shapes.json,
  shapes/voxel/<id>.json
The manifest gets contentVersion = the previous one + 1 (or --version). Publishing is two commits: the first holds
the new content/; then --pin writes that commit's jsDelivr address (immutable, cached for good) into
content/manifest.json as "cdn", and a second commit carries only that. The game reads the manifest from main on
raw.githubusercontent.com and everything else from the pinned commit. Order chunks get the revision of each level
(revs), so players re-download edited levels.

Every level is checked: its shape exists, its tile string fits the shape (3 characters per tile, valid characters),
and there is at least one source and one lamp. Problems stop the publish (exit code 1) and are printed.
"""
import json
import os
import shutil
import subprocess
import sys

REPO = "tuankun-279/CircuitCube"
B64 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_"
RULES = {"windowBack": 10, "windowAhead": 10, "hintsPerLevel": 3, "hintPenalty": 1, "undosPerLevel": 5, "adsOn": 1, "adsTesters": "", "updateBelow": 0, "minScore": 3, "parScore": 6, "overParScore": 5,
         "expertSlackPercent": 15, "expertSlackMin": 0}
POLY_TYPES = ("tetra", "octa", "dodeca", "geo")
HERE = os.path.dirname(os.path.abspath(__file__))


def load_dir(d):
    out = {}
    if not os.path.isdir(d):
        return out
    for f in sorted(os.listdir(d)):
        if f.endswith(".json"):
            with open(os.path.join(d, f)) as fh:
                doc = json.load(fh)
            # tolerate a wrapped document ({id, data, version})
            if isinstance(doc, dict) and "data" in doc and isinstance(doc["data"], dict) and "version" in doc:
                doc = doc["data"]
            out[f[:-5]] = doc
    return out


# ---------------------------------------------------------------- tile counts per shape

def preset_blocks(t, p):
    o = []
    seen = set()

    def a(x, y, z):
        if (x, y, z) not in seen:
            seen.add((x, y, z))
            o.append((x, y, z))
    if t == "cube":
        n = p["n"]
        for x in range(n):
            for y in range(n):
                for z in range(n):
                    a(x, y, z)
    elif t == "cross":
        for i in range(-p["arm"], p["arm"] + p["thick"]):
            for b in range(p["thick"]):
                for c in range(p["thick"]):
                    a(i, b, c); a(b, i, c); a(b, c, i)
    elif t == "tower":
        w, h = p["w"], p["h"]
        for x in range(w + 2):
            for z in range(w + 2):
                a(x, 0, z)
        for y in range(1, h):
            for x in range(1, w + 1):
                for z in range(1, w + 1):
                    a(x, y, z)
    elif t == "ell":
        for i in range(p["len"]):
            for b in range(p["thick"]):
                for c in range(p["thick"]):
                    a(i, b, c); a(b, i, c)
    elif t == "pyramid":
        h, s = 0, p["base"]
        while s >= 1:
            for x in range(s):
                for z in range(s):
                    a(x + h, h, z + h)
            h += 1
            s -= 2
    elif t == "ball":
        r = p["r"]
        R = int(r) + 1
        for x in range(-R, R + 1):
            for y in range(-R, R + 1):
                for z in range(-R, R + 1):
                    if x * x + y * y + z * z <= r * r:
                        a(x, y, z)
    elif t == "bar":
        for i in range(p["len"]):
            a(i, 0, 0)
    elif t == "slab":
        for x in range(p["w"]):
            for z in range(p["d"]):
                a(x, 0, z)
    return o


def faces(blocks):
    s = set(map(tuple, blocks))
    n = 0
    for (x, y, z) in s:
        for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
            if (x + d[0], y + d[1], z + d[2]) not in s:
                n += 1
    return n


def parse_id(sid):
    t, _, d = sid.rpartition("-")
    if not t or not d.isdigit():
        return None
    p = {}
    if t in ("cube",) + POLY_TYPES:
        p["n"] = int(d)
    elif t == "pyramid":
        p["base"] = int(d)
    elif t == "bar":
        p["len"] = int(d)
    elif t == "ball":
        p["r"] = int(d) / 10
    elif len(d) >= 2:
        a, b = int(d[:-1]), int(d[-1])
        p = {"cross": {"arm": a, "thick": b}, "tower": {"w": a, "h": b}, "ell": {"len": a, "thick": b}, "slab": {"w": a, "d": b}}.get(t)
        if p is None:
            return None
    else:
        return None
    return t, p


_poly_counts = {}


def poly_count(t, n):
    key = t + "-" + str(n)
    if key not in _poly_counts:
        js = "const P=require(%s);const T=P.build(%s,%d);console.log(T?T.count:0)" % (json.dumps(os.path.join(HERE, "poly.js")), json.dumps(t), n)
        _poly_counts[key] = int(subprocess.check_output(["node", "-e", js]).decode().strip())
    return _poly_counts[key]


def tile_count(sid, shapes):
    if sid.startswith("voxel-"):
        s = shapes.get(sid)
        if not s or not s.get("blocks"):
            return None
        b = s["blocks"]
        flat = b if b and not isinstance(b[0], list) else [v for blk in b for v in blk]
        return faces([tuple(flat[i:i + 3]) for i in range(0, len(flat) - 2, 3)])
    parsed = parse_id(sid)
    if not parsed:
        return None
    t, p = parsed
    if t in POLY_TYPES:
        return poly_count(t, p["n"]) or None
    return faces(preset_blocks(t, p))


# ---------------------------------------------------------------- publish

def pin(sha, repo):
    path = os.path.join(repo, "content", "manifest.json")
    with open(path) as fh:
        m = json.load(fh)
    m["cdn"] = "https://cdn.jsdelivr.net/gh/%s@%s/content/" % (REPO, sha)
    with open(path, "w") as fh:
        json.dump(m, fh, indent=2)
    print(json.dumps({"contentVersion": m.get("contentVersion"), "cdn": m["cdn"]}))


def main():
    if sys.argv[1] == "--pin":
        pin(sys.argv[2], sys.argv[3])
        return
    dump, repo = sys.argv[1], sys.argv[2]
    forced = int(sys.argv[sys.argv.index("--version") + 1]) if "--version" in sys.argv else None
    meta = load_dir(os.path.join(dump, "meta")).get("manifest")
    if not meta:
        print("No meta/manifest in the dump.")
        sys.exit(1)
    groups = load_dir(os.path.join(dump, "groups"))
    orders = load_dir(os.path.join(dump, "orders"))
    levels = load_dir(os.path.join(dump, "levels"))
    shapes = load_dir(os.path.join(dump, "shapes"))

    content = os.path.join(repo, "content")
    old = 0
    try:
        with open(os.path.join(content, "manifest.json")) as fh:
            old = json.load(fh).get("contentVersion", 0)
    except (OSError, ValueError):
        pass
    version = forced or old + 1

    problems, files = [], {}
    used_shapes = set()
    manifest = {
        "schema": meta.get("schema", 3), "minApp": meta.get("minApp", "0.1.0"), "shapesVersion": meta.get("shapesVersion", 1),
        "rules": dict(RULES, **(meta.get("rules") or {})),
        "groups": [{"id": g["id"], "unlock": g.get("unlock") or {"group": "", "cleared": 0}} for g in meta.get("groups", [])],
        "contentVersion": version,
        "cdn": "",
    }
    files["manifest.json"] = manifest
    total = 0
    for ref in manifest["groups"]:
        gid = ref["id"]
        g = dict(groups.get(gid) or {"id": gid, "name": gid, "enable": False, "levelCount": 0, "chunkSize": 100, "chunks": []})
        g.pop("nextSerial", None)
        g.pop("shapes", None)   # Level Lab only: the shapes Auto fill uses for this group
        chunks = sorted((o for o in orders.values() if o.get("group") == gid), key=lambda o: o["chunk"])
        count = 0
        for c in chunks:
            revs = []
            for serial in c.get("levels", []):
                lid = "%s-%04d" % (gid, serial)
                L = levels.get(lid)
                if L is None:
                    problems.append("%s chunk %d lists level %s, which does not exist" % (gid, c["chunk"], lid))
                    revs.append(0)
                    continue
                L = dict(L)
                L.pop("updatedAt", None)
                revs.append(L.get("rev", 1))
                n = tile_count(L.get("shape", ""), shapes)
                tiles = L.get("tiles", "")
                if n is None:
                    problems.append("%s: unknown shape %s" % (lid, L.get("shape")))
                elif len(tiles) != 3 * n:
                    problems.append("%s: %d tiles in the string, shape %s has %d" % (lid, len(tiles) // 3, L["shape"], n))
                elif any(B64.find(tiles[i]) < 0 for i in range(0, len(tiles), 3)):
                    problems.append("%s: bad wire character" % lid)
                elif not any(tiles[i] in "rby" for i in range(1, len(tiles), 3)) or not any(tiles[i] in "1234567" for i in range(1, len(tiles), 3)):
                    problems.append("%s: needs at least one source and one lamp" % lid)
                used_shapes.add(L.get("shape", ""))
                files["levels/%s/%04d.json" % (gid, serial)] = L
                count += 1
            files["orders/%s/%d.json" % (gid, c["chunk"])] = {"group": gid, "chunk": c["chunk"], "version": c.get("version", 1), "levels": c.get("levels", []), "revs": revs}
        if g.get("levelCount", 0) != count:
            problems.append("%s: levelCount %s but the order lists %d levels" % (gid, g.get("levelCount"), count))
        files["groups/%s.json" % gid] = g
        total += count

    catalog = {"version": manifest["shapesVersion"]}
    for sid in sorted(used_shapes):
        s = shapes.get(sid)
        if sid.startswith("voxel-"):
            if s:
                b = s["blocks"]
                flat = b if b and not isinstance(b[0], list) else [v for blk in b for v in blk]
                files["shapes/voxel/%s.json" % sid] = {"id": sid, "type": "voxel", "blocks": flat}
            continue
        parsed = parse_id(sid)
        if parsed:
            t, p = parsed
            catalog.setdefault(t, []).append(dict({"id": sid}, **p))
    files["shapes.json"] = catalog

    if problems:
        print("Publish stopped, %d problems:" % len(problems))
        for p in problems:
            print(" - " + p)
        sys.exit(1)

    if os.path.isdir(content):
        shutil.rmtree(content)
    for rel, doc in files.items():
        path = os.path.join(content, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as fh:
            json.dump(doc, fh, indent=2 if rel.count("/") <= 1 and not rel.startswith("levels") else None)
    print(json.dumps({"contentVersion": version, "groups": len(manifest["groups"]), "levels": total, "files": len(files)}))


if __name__ == "__main__":
    main()
