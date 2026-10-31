# CubeRadarMotion (0.8.5)

Read-only Windows helper. Capture is scoped to the exact outbound IPv4 world
connection owned by AION2. The only process-memory rights requested are
`PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`; no injection, input or packet send.
Only decoded numeric motion, selected-target state and status pass to the GUI's same-account named pipe.
Keys and cipher state stay in this process. Normal `--pipe` mode writes no files.
Explicit laboratory `--report`/`--status` modes write numeric diagnostics, never keys.

The locator subset derives from lunixxx37/aion2-protocol-research commit
`fe2122f0e54e1de854c11e91e3c7bf51916a4f52`, MIT; full license is included.
RSA/authentication/export CLIs were omitted. The subset's `locate_states` accepts
a callback to reject inactive pairs. Original notices remain in its source.
Other helper code is part of this modified GPL-3.0 Cube Radar project.

Initial location can read substantial writable memory to find validated network
objects. There is a 20-second/3-GiB search budget. Once located, only small cached
state blocks are sampled. Session/capture mismatch invalidates motion; the helper
does not bypass denied process access or modify protection settings.

## Build (Windows x64; Python 3.12, .NET SDK 10)

From repository root:

```powershell
python -m pip install -r src/CubeRadar.Motion/requirements-build.txt
python -m unittest discover -s src/CubeRadar.Motion -v
dotnet test tests/CubeRadar.Tests/CubeRadar.Tests.csproj -c Release
dotnet publish src/CubeRadar.Tr/CubeRadar.Tr.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/TR
python -m PyInstaller --onefile --console --name CubeRadarMotion --distpath dist/TR/Motion --workpath dist/motion-build --specpath dist src/CubeRadar.Motion/motion_reader.py
```

Distribute the entire TR directory, including `config`, `Motion`, GPL and
third-party notices. Npcap is a prerequisite and is not redistributed.

The GUI's `Paket yönü` button starts the helper with a random pipe name and the
process that owns the active world connection. Windows elevation is requested for
this helper only. `--start --motion` starts both readers; normal launch is manual.
`--topmost --proximity --movement` are optional startup preferences.
Closing the GUI/pipe terminates the helper on its next write; scans publish progress.
Unrecognized movement records are not promoted to a facing measurement.

The validated C2S stream also reports exact persistent `33 38` target select/clear
records (`selectionProtocol=1`). Noncanonical, truncated or unknown flag variants
invalidate selection; `30 38` skill context is ignored. Health renews only the
stream lease, never the original selection time. Capture gaps invalidate target
state and advance its generation. No packet bodies or cipher state cross the pipe.

TCP reordering is buffered for at most 1500 ms, 256 KiB and 256 segments.
Only persistent loss or queue pressure clears the partial frame and invalidates
the cipher. Health records include the helper version, bounded transport counts
and at most 24 opcode/length counts. They contain no payloads or cipher state.
`CubeRadarMotion.exe --version` prints the compiled helper's version without
opening the game or capture. Copy the whole release, including `Motion`.
