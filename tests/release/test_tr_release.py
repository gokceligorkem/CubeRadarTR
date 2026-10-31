import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

import pyzipper

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('tr_release',ROOT/'tools'/'package_tr_release.py')
release=importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)

class ProtectedReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.folder=self.root/'app';self.folder.mkdir()
        for name in release.REQUIRED_FILES:
            path=self.folder/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b'test release data')
        (self.folder/'ThirdPartyNotices').mkdir();(self.folder/'ThirdPartyNotices'/'NOTICE.txt').write_text('license')
        self.source=self.root/'source.zip'
        with zipfile.ZipFile(self.source,'w') as archive:
            archive.writestr('CubeRadarTR-src/LICENSE','GPL-3.0')
            archive.writestr('CubeRadarTR-src/src/CubeRadar.Tr/CubeRadar.Tr.csproj','<Project><PropertyGroup><Version>0.8.7</Version></PropertyGroup></Project>')
        self.password=b'Test-only-password-456'
        self.output=self.root/'out'

    def package(self,password=None,version='0.8.7'):
        return release.package(self.folder,self.source,self.output,version,self.password if password is None else password)

    def test_all_members_are_aes256_and_wrong_or_missing_password_cannot_read_exe_or_source(self):
        result=self.package()
        with pyzipper.AESZipFile(result['archive']) as archive:
            self.assertTrue(all(i.flag_bits&1 and i.wz_aes_strength==3 for i in archive.infolist()))
            for name in ['CubeRadarTR/CubeRadarTR.exe','CubeRadarTR/Sources/CubeRadarTR-source.zip']:
                for password in [None,b'incorrect-password']:
                    with self.subTest(name=name,passwordSet=password is not None):
                        with self.assertRaises(RuntimeError):archive.read(name,pwd=password)
            self.assertEqual(archive.read('CubeRadarTR/CubeRadarTR.exe',pwd=self.password),b'test release data')
            self.assertEqual(archive.read('CubeRadarTR/Sources/CubeRadarTR-source.zip',pwd=self.password),self.source.read_bytes())

    def test_personal_settings_debug_symbols_and_unexpected_files_are_not_in_the_release(self):
        for name in ['pets.json','companion.json','CubeRadarTR.pdb','password.txt','config/private.json','Motion/debug.log']:
            path=self.folder/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('private')
        result=self.package()
        with pyzipper.AESZipFile(result['archive']) as archive:
            names=archive.namelist()
            self.assertFalse(any(any(s in n for s in ['pets.json','companion.json','.pdb','password','private.json','debug.log']) for n in names))
            self.assertIn('CubeRadarTR/Motion/CubeRadarMotion.exe',names)
            self.assertIn('CubeRadarTR/ThirdPartyNotices/NOTICE.txt',names)

    def test_no_password_or_short_password_fails_before_creating_output(self):
        for password in [b'',b'short']:
            with self.assertRaises(ValueError):self.package(password)
        self.assertFalse(self.output.exists())

    def test_missing_motion_reader_fails_closed(self):
        (self.folder/'Motion/CubeRadarMotion.exe').unlink()
        with self.assertRaises(ValueError):self.package()
        self.assertFalse(self.output.exists())

    def test_source_version_must_match_the_executable_release_version(self):
        with self.assertRaises(ValueError):self.package(version='0.8.8')
        self.assertFalse(self.output.exists())

    def test_manifest_covers_only_encrypted_asset_and_cannot_contain_password(self):
        result=self.package();archive=Path(result['archive'])
        checksums=Path(result['checksums']).read_text()
        self.assertEqual(checksums,f'{hashlib.sha256(archive.read_bytes()).hexdigest()}  CubeRadarTR.zip\n')
        self.assertNotIn(self.password.decode(),checksums)
        self.assertEqual({p.name for p in self.output.iterdir()},{'CubeRadarTR.zip','SHA256SUMS.txt'})

if __name__=='__main__':unittest.main()
