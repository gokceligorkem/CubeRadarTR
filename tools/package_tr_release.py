"""Create the public AES-256 ZIP; the password is supplied only through the environment."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

import pyzipper

REQUIRED_FILES=(
    'CubeRadarTR.exe','LICENSE','KILAVUZ-TR.txt','CHANGES-TR.md',
    'TEKNIK-RAPOR-TR.md','YON-INCELEME-TR.md','config/radar.json','config/cubes.json',
    'Motion/CubeRadarMotion.exe','Motion/LICENSE-MIT.txt',
)

def package(folder,source,output,version,password):
    if not isinstance(password,bytes) or not 16<=len(password)<=256 or not password.strip():
        raise ValueError('A release password of 16–256 bytes is required.')
    if not re.fullmatch(r'\d+\.\d+\.\d+',version):raise ValueError('Invalid version.')
    folder=Path(folder).resolve();source=Path(source);output=Path(output)
    if not folder.is_dir() or not source.is_file() or source.is_symlink():raise ValueError('Release/source is missing.')
    files=[]
    for name in REQUIRED_FILES:
        path=folder/name
        if not path.is_file() or path.is_symlink() or not path.resolve().is_relative_to(folder):
            raise ValueError('Required release file is missing or unsafe: '+name)
        files.append((path,'CubeRadarTR/'+name))
    notices=folder/'ThirdPartyNotices'
    if not notices.is_dir() or notices.is_symlink():raise ValueError('Third-party notices are required.')
    for path in sorted(notices.rglob('*')):
        if path.is_symlink() or not path.resolve().is_relative_to(folder):raise ValueError('Unsafe notice path.')
        if path.is_file() and (path.suffix.lower() in ('.txt','.xml') or path.name in ('NOTICE','LICENSE')):
            files.append((path,'CubeRadarTR/'+path.relative_to(folder).as_posix()))
    if not any(name.startswith('CubeRadarTR/ThirdPartyNotices/') for _,name in files):
        raise ValueError('Third-party notices are empty.')
    with zipfile.ZipFile(source) as archive:
        project='CubeRadarTR-src/src/CubeRadar.Tr/CubeRadar.Tr.csproj'
        if 'CubeRadarTR-src/LICENSE' not in archive.namelist() or archive.getinfo(project).file_size>64000:
            raise ValueError('Source/license is missing or invalid.')
        tree=ET.fromstring(archive.read(project))
        if tree.findtext('./PropertyGroup/Version')!=version:raise ValueError('Source version does not match release.')
    files.append((source,'CubeRadarTR/Sources/CubeRadarTR-source.zip'))
    output.mkdir(parents=True,exist_ok=True)
    target=output/'CubeRadarTR.zip';temporary=output/'CubeRadarTR.zip.tmp'
    try:
        with pyzipper.AESZipFile(str(temporary),'w',compression=pyzipper.ZIP_DEFLATED,compresslevel=6) as archive:
            archive.setpassword(password);archive.setencryption(pyzipper.WZ_AES,nbits=256)
            for path,name in files:archive.write(str(path),name)
        with pyzipper.AESZipFile(str(temporary)) as archive:
            if not all(i.flag_bits&1 and i.wz_aes_strength==3 for i in archive.infolist()):
                raise ValueError('An unencrypted member was detected.')
            archive.setpassword(password)
            if archive.testzip() is not None:raise ValueError('Encrypted ZIP verification failed.')
        temporary.replace(target)
    finally:
        if temporary.exists():temporary.unlink()
    with target.open('rb') as stream:checksum=hashlib.file_digest(stream,'sha256').hexdigest()
    manifest=output/'SHA256SUMS.txt';manifest.write_text(checksum+'  CubeRadarTR.zip\n',encoding='utf-8')
    return {'archive':str(target),'checksums':str(manifest),'version':version,'aesBits':256,'files':len(files)}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder',type=Path,required=True)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--version',required=True)
    args=parser.parse_args()
    try:
        result=package(args.folder,args.source,args.output,args.version,
            os.environ.get('CUBE_RADAR_RELEASE_PASSWORD','').encode('utf-8'))
        print('AES-256 ZIP verified: '+result['archive'])
    except (ValueError,OSError,KeyError,zipfile.BadZipFile,ET.ParseError):
        print('Release packaging failed; check inputs and the private password configuration.',file=sys.stderr)
        return 1
    return 0

if __name__=='__main__':sys.exit(main())
