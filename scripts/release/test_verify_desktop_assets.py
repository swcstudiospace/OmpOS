"""Unit tests for verify_desktop_assets.py. Stdlib only; the packages are minimal byte strings in temp directories.

Run from the repo root: python3 scripts/release/test_verify_desktop_assets.py
"""
import contextlib
import hashlib
import importlib.util
import io
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

SCRIPT = pathlib.Path(__file__).with_name("verify_desktop_assets.py")
spec = importlib.util.spec_from_file_location("vda", SCRIPT)
vda = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vda)

MIB = 1024 * 1024
VERSION = "0.20.0"

GOOD_ZIP = b"PK\x03\x04" + b"\x00" * 16
GOOD_GZ = b"\x1f\x8b\x08\x00" + b"\x00" * 16
GOOD_DEB = b"!<arch>\n" + b"debian-binary/  " + b"0" * 44 + b"2.0\n"
GOOD_EXE = b"MZ" + b"\x00" * MIB
GOOD_DMG = b"\x00" * MIB + b"koly" + b"\x00" * 508

GOOD_CONTENT = {
    "linux-x64.tar.gz": GOOD_GZ,
    "linux-x64.deb": GOOD_DEB,
    "win-x64.zip": GOOD_ZIP,
    "win-x64-Setup.exe": GOOD_EXE,
    "osx-arm64.zip": GOOD_ZIP,
    "osx-arm64.dmg": GOOD_DMG,
}


def name_for(suffix, version=VERSION):
    return f"Autonogrammer-{version}-{suffix}"


class Case(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.dir = tmp.name

    def put(self, name, data):
        with open(os.path.join(self.dir, name), "wb") as handle:
            handle.write(data)

    def populate(self, version=VERSION, only=None, overrides=None):
        overrides = overrides or {}
        for suffix, data in GOOD_CONTENT.items():
            if only is not None and not any(suffix.startswith(rid) for rid in only):
                continue
            self.put(name_for(suffix, version), overrides.get(suffix, data))

    def run_main(self, *extra, version=VERSION, platform=None):
        argv = ["--dir", self.dir, "--version", version]
        if platform is not None:
            argv += ["--platform", platform]
        argv += list(extra)
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = vda.main(argv)
        return code, out.getvalue()

    def errors(self, output):
        return [line for line in output.splitlines() if line.startswith("::error::verify_desktop_assets: ")]


class PresenceTests(Case):
    def test_all_present_passes(self):
        self.populate()
        code, out = self.run_main()
        self.assertEqual(code, 0, out)
        self.assertEqual(self.errors(out), [])
        self.assertIn("6 files verified for version 0.20.0", out)
        for suffix in GOOD_CONTENT:
            self.assertIn(name_for(suffix), out)

    def test_each_file_missing_is_reported(self):
        for suffix in GOOD_CONTENT:
            with self.subTest(suffix=suffix):
                for entry in os.listdir(self.dir):
                    os.remove(os.path.join(self.dir, entry))
                self.populate()
                os.remove(os.path.join(self.dir, name_for(suffix)))
                code, out = self.run_main()
                self.assertEqual(code, 1)
                self.assertEqual(self.errors(out), [f"::error::verify_desktop_assets: missing {name_for(suffix)} in {self.dir}"])

    def test_empty_file_is_reported(self):
        self.populate()
        self.put(name_for("win-x64.zip"), b"")
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertEqual(self.errors(out), [f"::error::verify_desktop_assets: {name_for('win-x64.zip')} is empty"])

    def test_wrong_version_name_is_reported(self):
        self.populate()
        os.rename(os.path.join(self.dir, name_for("linux-x64.deb")), os.path.join(self.dir, name_for("linux-x64.deb", "0.19.0")))
        code, out = self.run_main()
        self.assertEqual(code, 1)
        errors = self.errors(out)
        self.assertEqual(len(errors), 2, out)
        self.assertTrue(any("carries version 0.19.0" in line and "release version is 0.20.0" in line for line in errors))
        self.assertTrue(any(f"missing {name_for('linux-x64.deb')}" in line for line in errors))

    def test_prerelease_version_is_not_confused_with_its_base(self):
        # 0.20.0-rc.1 files in a 0.20.0 run carry a different version; the reverse also fails.
        self.populate("0.20.0-rc.1")
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertTrue(any("carries version 0.20.0-rc.1" in line for line in self.errors(out)))

    def test_unexpected_extra_file_is_reported(self):
        self.populate()
        self.put("notes.txt", b"hello")
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertEqual(self.errors(out), [f"::error::verify_desktop_assets: unexpected file notes.txt in {self.dir}"])

    def test_sums_file_and_dotfiles_are_ignored(self):
        self.populate()
        self.put("SHA256SUMS-desktop", b"stale\n")
        self.put(".DS_Store", b"x")
        code, out = self.run_main()
        self.assertEqual(code, 0, out)

    def test_missing_directory_fails(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = vda.main(["--dir", os.path.join(self.dir, "nope"), "--version", VERSION])
        self.assertEqual(code, 1)
        self.assertIn("is not a directory", out.getvalue())

    def test_prerelease_version_passes(self):
        self.populate("0.20.0-rc.1")
        code, out = self.run_main(version="0.20.0-rc.1")
        self.assertEqual(code, 0, out)
        self.assertIn("Autonogrammer-0.20.0-rc.1-win-x64-Setup.exe", out)


class MagicTests(Case):
    def assert_bad(self, suffix, data, fragment):
        self.populate(overrides={suffix: data})
        code, out = self.run_main()
        self.assertEqual(code, 1, out)
        errors = self.errors(out)
        self.assertEqual(len(errors), 1, out)
        self.assertIn(name_for(suffix), errors[0])
        self.assertIn(fragment, errors[0])

    def test_bad_zip_magic(self):
        self.assert_bad("win-x64.zip", b"XX" + b"\x00" * 16, "PK")

    def test_bad_osx_zip_magic(self):
        self.assert_bad("osx-arm64.zip", b"\x00" * 16, "PK")

    def test_bad_exe_magic(self):
        self.assert_bad("win-x64-Setup.exe", b"PK" + b"\x00" * MIB, "MZ")

    def test_bad_gzip_magic(self):
        self.assert_bad("linux-x64.tar.gz", b"PK\x03\x04" + b"\x00" * 16, "1f 8b")

    def test_bad_deb_magic(self):
        self.assert_bad("linux-x64.deb", b"PK\x03\x04" + b"\x00" * 40, "!<arch>")

    def test_deb_without_debian_binary_member(self):
        self.assert_bad("linux-x64.deb", b"!<arch>\n" + b"control.tar.gz  " + b"0" * 44, "debian-binary")

    def test_bad_dmg_trailer(self):
        self.assert_bad("osx-arm64.dmg", b"\x00" * MIB + b"nope" + b"\x00" * 508, "koly")

    def test_dmg_with_koly_only_at_the_start_is_bad(self):
        self.assert_bad("osx-arm64.dmg", b"koly" + b"\x00" * (MIB + 508), "koly")

    def test_good_magic_for_each_type(self):
        self.populate()
        problems, rows = vda.verify(self.dir, VERSION, "all")
        self.assertEqual(problems, [])
        self.assertEqual(len(rows), 6)

    def test_dmg_smaller_than_trailer_is_bad(self):
        self.assert_bad("osx-arm64.dmg", b"koly", "smaller than the 512-byte UDIF trailer")


class SizeTests(Case):
    def test_stub_exe_is_a_failure(self):
        self.populate(overrides={"win-x64-Setup.exe": b"MZ" + b"\x00" * 1000})
        code, out = self.run_main()
        self.assertEqual(code, 1)
        errors = self.errors(out)
        self.assertEqual(len(errors), 1, out)
        self.assertIn("win-x64-Setup.exe is only 1002 bytes", errors[0])

    def test_stub_dmg_is_a_failure(self):
        self.populate(overrides={"osx-arm64.dmg": b"\x00" * 4096 + b"koly" + b"\x00" * 508})
        code, out = self.run_main()
        self.assertEqual(code, 1)
        errors = self.errors(out)
        self.assertEqual(len(errors), 1, out)
        self.assertIn("osx-arm64.dmg is only", errors[0])

    def test_exactly_one_mib_is_not_enough(self):
        self.populate(overrides={"win-x64-Setup.exe": b"MZ" + b"\x00" * (MIB - 2)})
        code, _out = self.run_main()
        self.assertEqual(code, 1)

    def test_success_table_reports_human_readable_sizes(self):
        self.populate()
        code, out = self.run_main()
        self.assertEqual(code, 0)
        self.assertIn("1.00 MiB", out)
        self.assertIn(f"{len(GOOD_EXE)} bytes", out)
        self.assertIn("20 B", out)

    def test_output_is_deterministic(self):
        self.populate()
        self.assertEqual(self.run_main(), self.run_main())


class PlatformTests(Case):
    def test_platform_filter_expects_only_that_rids_files(self):
        for rid in vda.PLATFORMS:
            with self.subTest(rid=rid):
                for entry in os.listdir(self.dir):
                    os.remove(os.path.join(self.dir, entry))
                self.populate(only=[rid])
                code, out = self.run_main(platform=rid)
                self.assertEqual(code, 0, out)
                self.assertIn("2 files verified", out)
                code, out = self.run_main()  # the same directory is incomplete for "all"
                self.assertEqual(code, 1)
                self.assertEqual(len(self.errors(out)), 4)

    def test_platform_filter_flags_other_rids_files_as_unexpected(self):
        self.populate()
        code, out = self.run_main(platform="linux-x64")
        self.assertEqual(code, 1)
        errors = self.errors(out)
        self.assertEqual(len(errors), 4, out)
        self.assertTrue(all("unexpected file" in line for line in errors))

    def test_same_version_file_of_another_platform_is_unexpected_not_a_version_clash(self):
        self.populate(only=["win-x64"])
        self.put(name_for("linux-x64.tar.gz"), GOOD_GZ)
        code, out = self.run_main(platform="win-x64")
        self.assertEqual(code, 1)
        errors = self.errors(out)
        self.assertEqual(len(errors), 1, out)
        self.assertIn("unexpected file", errors[0])
        self.assertIn(name_for("linux-x64.tar.gz"), errors[0])
        self.assertNotIn("carries version", errors[0])

    def test_platform_filter_still_checks_its_own_files(self):
        self.populate(only=["win-x64"], overrides={"win-x64.zip": b""})
        code, out = self.run_main(platform="win-x64")
        self.assertEqual(code, 1)
        self.assertEqual(self.errors(out), [f"::error::verify_desktop_assets: {name_for('win-x64.zip')} is empty"])

    def test_platform_filter_reports_wrong_version(self):
        self.populate(only=["osx-arm64"])
        os.rename(os.path.join(self.dir, name_for("osx-arm64.zip")), os.path.join(self.dir, name_for("osx-arm64.zip", "0.19.0")))
        code, out = self.run_main(platform="osx-arm64")
        self.assertEqual(code, 1)
        self.assertEqual(len(self.errors(out)), 2, out)


class SumsTests(Case):
    def sums_path(self):
        return os.path.join(self.dir, "SHA256SUMS-desktop")

    def test_write_sums_round_trip(self):
        self.populate()
        code, out = self.run_main("--write-sums", self.sums_path())
        self.assertEqual(code, 0, out)
        with open(self.sums_path(), "rb") as handle:
            lines = handle.read().decode().splitlines()
        names = sorted(name_for(suffix) for suffix in GOOD_CONTENT)
        self.assertEqual([line.split("  ", 1)[1] for line in lines], names)
        for line in lines:
            digest, name = line.split("  ", 1)
            self.assertRegex(digest, r"^[0-9a-f]{64}$")
            with open(os.path.join(self.dir, name), "rb") as handle:
                self.assertEqual(digest, hashlib.sha256(handle.read()).hexdigest())
        self.assertEqual(vda.check_sums(self.dir, names, self.sums_path()), [])

    def test_write_sums_outside_the_directory_and_rerun(self):
        self.populate()
        with tempfile.TemporaryDirectory() as other:
            target = os.path.join(other, "sums.txt")
            code, out = self.run_main("--write-sums", target)
            self.assertEqual(code, 0, out)
            self.assertTrue(os.path.isfile(target))
        self.assertEqual(sorted(os.listdir(self.dir)), sorted(name_for(suffix) for suffix in GOOD_CONTENT))

    def test_rerun_over_an_existing_sums_file_passes(self):
        self.populate()
        self.assertEqual(self.run_main("--write-sums", self.sums_path())[0], 0)
        self.assertEqual(self.run_main("--write-sums", self.sums_path())[0], 0)

    def test_without_write_sums_nothing_is_written(self):
        self.populate()
        before = sorted(os.listdir(self.dir))
        code, _out = self.run_main()
        self.assertEqual(code, 0)
        self.assertEqual(sorted(os.listdir(self.dir)), before)

    def test_failed_verification_writes_no_sums(self):
        self.populate()
        os.remove(os.path.join(self.dir, name_for("linux-x64.deb")))
        code, _out = self.run_main("--write-sums", self.sums_path())
        self.assertEqual(code, 1)
        self.assertFalse(os.path.exists(self.sums_path()))

    def test_tampered_file_fails_the_reread_check(self):
        self.populate()
        names = sorted(name_for(suffix) for suffix in GOOD_CONTENT)
        vda.write_sums(self.dir, names, self.sums_path())
        self.assertEqual(vda.check_sums(self.dir, names, self.sums_path()), [])
        with open(os.path.join(self.dir, name_for("win-x64.zip")), "ab") as handle:
            handle.write(b"tamper")
        problems = vda.check_sums(self.dir, names, self.sums_path())
        self.assertEqual(len(problems), 1)
        self.assertIn(f"{name_for('win-x64.zip')} does not match", problems[0])

    def test_main_exits_1_when_the_file_changes_between_write_and_reread(self):
        self.populate()
        real = vda.write_sums
        victim = os.path.join(self.dir, name_for("linux-x64.tar.gz"))

        def write_then_tamper(directory, names, sums_path):
            real(directory, names, sums_path)
            with open(victim, "ab") as handle:
                handle.write(b"tamper")

        with mock.patch.object(vda, "write_sums", side_effect=write_then_tamper):
            code, out = self.run_main("--write-sums", self.sums_path())
        self.assertEqual(code, 1)
        self.assertEqual(len(self.errors(out)), 1, out)
        self.assertIn("does not match its SHA-256", out)

    def test_check_sums_rejects_malformed_and_incomplete_files(self):
        self.populate()
        names = sorted(name_for(suffix) for suffix in GOOD_CONTENT)
        self.put("SHA256SUMS-desktop", b"not a sums line\n")
        problems = vda.check_sums(self.dir, names, self.sums_path())
        self.assertTrue(any("not in sha256sum format" in p for p in problems))
        self.assertTrue(any("instead of" in p for p in problems))


class CliTests(Case):
    def run_cli(self, *args):
        return subprocess.run([sys.executable, str(SCRIPT), *args], capture_output=True, text=True, check=False)

    def test_exit_0_on_success(self):
        self.populate()
        result = self.run_cli("--dir", self.dir, "--version", VERSION)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("6 files verified", result.stdout)

    def test_exit_1_on_a_problem_with_error_lines(self):
        self.populate()
        os.remove(os.path.join(self.dir, name_for("osx-arm64.dmg")))
        result = self.run_cli("--dir", self.dir, "--version", VERSION)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout.splitlines(), [f"::error::verify_desktop_assets: missing {name_for('osx-arm64.dmg')} in {self.dir}"])

    def test_exit_2_on_missing_arguments_with_usage(self):
        result = self.run_cli("--version", VERSION)
        self.assertEqual(result.returncode, 2)
        self.assertIn("usage:", result.stderr)

    def test_invalid_version_is_bad_usage(self):
        self.populate()
        for bad in ("v0.20.0", "0.20", "0.20.0+build", "latest", "", "1.2.3-"):
            with self.subTest(version=bad):
                result = self.run_cli("--dir", self.dir, "--version", bad)
                self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
                self.assertIn("usage:", result.stderr)

    def test_unknown_platform_is_bad_usage(self):
        result = self.run_cli("--dir", self.dir, "--version", VERSION, "--platform", "linux-arm64")
        self.assertEqual(result.returncode, 2)


if __name__ == "__main__":
    unittest.main()
