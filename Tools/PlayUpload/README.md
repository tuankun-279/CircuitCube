# Play upload (Android Publisher API)

`upload_play.py` uploads a signed `.aab` to a Google Play testing track and rolls it out with the release notes in
`StoreListing/release_notes.txt`. Browser tools cannot attach a 100 MB bundle to Play Console, so releases go through
this script.

```
py -3.12 Tools/PlayUpload/upload_play.py Builds/CircuitCube-1.4.0-7.aab            # closed testing (alpha), rolled out
py -3.12 Tools/PlayUpload/upload_play.py Builds/CircuitCube-1.4.0-7.aab --draft    # leave it as a draft to check in Play Console
py -3.12 Tools/PlayUpload/upload_play.py Builds/CircuitCube-1.4.0-7.aab --track internal
```

- Tracks: `internal`, `alpha` (Closed testing - Alpha, the default), `beta` (open testing). `production` is refused
  unless `--allow-production` is given.
- The version code must be higher than every code uploaded before (Player Settings > Android > Bundle Version Code).
- Release notes: the `<en-US>` and `<vi-VN>` blocks, 500 characters at most each.

## One-time setup (the owner does this; no key ever goes into the repo)

1. Google Cloud Console (the project linked to Play Console, or a new one): APIs & Services > Library >
   **Google Play Android Developer API** > Enable.
2. IAM & Admin > Service Accounts > Create, e.g. `play-upload`. No Cloud roles needed. Keys > Add key > JSON.
   Save it as `D:\Projects\KeyPublish\play-upload.json` (outside the repo), or set `PLAY_SERVICE_ACCOUNT_JSON` to its path.
3. Play Console > Users and permissions > Invite new users: the service account email
   (`play-upload@<project>.iam.gserviceaccount.com`). App permissions: Circuit Cube with **Release apps to testing
   tracks** (add "Release to production" only if production uploads are wanted). Send the invite; it is active at once.
4. On the PC: `py -3.12 -m pip install --user google-api-python-client google-auth`.

Check: `py -3.12 Tools/PlayUpload/upload_play.py <aab> --draft` should print the edit, the upload progress and
"committed"; the draft then shows in Play Console under the track.
