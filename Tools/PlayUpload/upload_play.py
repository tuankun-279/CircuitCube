"""Upload an Android App Bundle to a Google Play testing track (Android Publisher API v3).

Usage (PowerShell, from the repo root):
    py -3.12 Tools/PlayUpload/upload_play.py Builds/CircuitCube-1.4.0-7.aab
    py -3.12 Tools/PlayUpload/upload_play.py Builds/CircuitCube-1.4.0-7.aab --track internal --draft

It opens an edit, uploads the bundle (resumable, fine for 100+ MB), puts that version code on the track with the
release notes from StoreListing/release_notes.txt (<en-US> and <vi-VN> blocks), and commits the edit.
Production is refused unless --allow-production is given.

The service account key is read from PLAY_SERVICE_ACCOUNT_JSON, default D:\\Projects\\KeyPublish\\play-upload.json.
It never goes into the repo. One-time setup: Tools/PlayUpload/README.md.
Needs: pip install --user google-api-python-client google-auth
"""
import argparse
import os
import re
import sys

PACKAGE = "com.zyerkun.circuitcube"
DEFAULT_KEY = r"D:\Projects\KeyPublish\play-upload.json"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
NOTES = os.path.join(ROOT, "StoreListing", "release_notes.txt")
SCOPES = ["https://www.googleapis.com/auth/androidpublisher"]


def release_notes(path):
    """The <lang>...</lang> blocks of the notes file as Play release notes (500 characters at most each)."""
    if not os.path.exists(path):
        return []
    text = open(path, encoding="utf-8").read()
    notes = []
    for lang, body in re.findall(r"<([a-z]{2}-[A-Z]{2})>\s*\n(.*?)\n\s*</\1>", text, re.S):
        body = body.strip()
        if len(body) > 500:
            sys.exit("release notes for %s are %d characters; Play allows 500" % (lang, len(body)))
        notes.append({"language": lang, "text": body})
    return notes


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("aab", help="path to the .aab")
    p.add_argument("--track", default="alpha", help="testing track: internal, alpha (closed testing), beta (open testing)")
    p.add_argument("--name", help='release name, default "<version code> (<version name>)" as Play shows it')
    p.add_argument("--draft", action="store_true", help="leave the release as a draft instead of rolling it out")
    p.add_argument("--notes", default=NOTES, help="release notes file with <en-US> and <vi-VN> blocks")
    p.add_argument("--package", default=PACKAGE)
    p.add_argument("--allow-production", action="store_true", help="needed to touch the production track")
    a = p.parse_args()

    if a.track == "production" and not a.allow_production:
        sys.exit("refusing the production track without --allow-production")
    if not os.path.exists(a.aab):
        sys.exit("no such file: " + a.aab)
    key = os.environ.get("PLAY_SERVICE_ACCOUNT_JSON", DEFAULT_KEY)
    if not os.path.exists(key):
        sys.exit("service account key not found: %s (see Tools/PlayUpload/README.md)" % key)

    from google.oauth2 import service_account
    from googleapiclient.discovery import build
    from googleapiclient.http import MediaFileUpload

    creds = service_account.Credentials.from_service_account_file(key, scopes=SCOPES)
    api = build("androidpublisher", "v3", credentials=creds, cache_discovery=False)
    edits = api.edits()

    edit = edits.insert(packageName=a.package, body={}).execute()
    eid = edit["id"]
    print("edit", eid)
    media = MediaFileUpload(a.aab, mimetype="application/octet-stream", resumable=True, chunksize=8 * 1024 * 1024)
    req = edits.bundles().upload(packageName=a.package, editId=eid, media_body=media)
    bundle = None
    while bundle is None:
        status, bundle = req.next_chunk()
        if status:
            print("uploaded %d%%" % int(status.progress() * 100))
    code = bundle["versionCode"]
    print("bundle version code", code)

    release = {"versionCodes": [str(code)], "status": "draft" if a.draft else "completed"}
    if a.name:
        release["name"] = a.name
    notes = release_notes(a.notes)
    if notes:
        release["releaseNotes"] = notes
    edits.tracks().update(packageName=a.package, editId=eid, track=a.track,
                          body={"track": a.track, "releases": [release]}).execute()
    res = edits.commit(packageName=a.package, editId=eid).execute()
    print("committed edit %s: version %s on %s (%s)" % (res.get("id", eid), code, a.track, release["status"]))


if __name__ == "__main__":
    main()
