import copy
import importlib.util
import json
import pathlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("gs", pathlib.Path(__file__).with_name("generate-sbom.py"))
gs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gs)

NUSPEC = '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>Foo.Bar</id><license type="expression">MIT</license></metadata></package>'


def tree(tmp):
    root = pathlib.Path(tmp) / "repo"
    (root / "src/P").mkdir(parents=True)
    (root / "deploy").mkdir()
    (root / "src/P/packages.lock.json").write_text(json.dumps({"version": 2, "dependencies": {"net10.0": {
        "Foo.Bar": {"type": "Direct", "resolved": "1.2.3"},
        "Other": {"type": "Project"},
        "Unlicensed": {"type": "Transitive", "resolved": "0.1.0"}}}}))
    (root / "deploy/api.Containerfile").write_text("ARG BUN_VERSION=1.4.3\nARG OMP_VERSION=18.12.5\n")
    cache = pathlib.Path(tmp) / "cache" / "foo.bar" / "1.2.3"
    cache.mkdir(parents=True)
    (cache / "foo.bar.nuspec").write_text(NUSPEC)
    return root, pathlib.Path(tmp) / "cache"


class SbomTests(unittest.TestCase):
    def test_build_is_valid_and_deterministic(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            a = gs.build_sbom(root, cache, "2026-01-01T00:00:00Z")
            b = gs.build_sbom(root, cache, "2026-01-01T00:00:00Z")
            self.assertEqual(gs.render(a), gs.render(b))
            self.assertEqual(gs.validate(a), [])
            pkgs = {p["name"]: p for p in a["packages"]}
            self.assertEqual(pkgs["Foo.Bar"]["licenseDeclared"], "MIT")
            self.assertEqual(pkgs["Unlicensed"]["licenseDeclared"], "NOASSERTION")
            self.assertEqual(pkgs["Foo.Bar"]["externalRefs"][0]["referenceLocator"], "pkg:nuget/Foo.Bar@1.2.3")
            self.assertIn("bun", pkgs)
            self.assertIn("@oh-my-pi/pi-coding-agent", pkgs)
            self.assertNotIn("Other", pkgs)
            types = {(r["relationshipType"]) for r in a["relationships"]}
            self.assertEqual(types, {"DESCRIBES", "DEPENDS_ON"})

    def test_validate_reports_problems(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            doc = gs.build_sbom(root, cache, "2026-01-01T00:00:00Z")
            bad = copy.deepcopy(doc)
            del bad["packages"][0]["downloadLocation"]
            bad["relationships"].append({"spdxElementId": "SPDXRef-nope", "relationshipType": "DEPENDS_ON", "relatedSpdxElement": "SPDXRef-DOCUMENT"})
            problems = gs.validate(bad)
            self.assertTrue(any("downloadLocation" in p for p in problems))
            self.assertTrue(any("SPDXRef-nope" in p for p in problems))

    def test_check_detects_stale_and_ignores_created(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = pathlib.Path(tmp) / "sbom.json"
            base = ["--root", str(root), "--cache", str(cache), "--output", str(out)]
            self.assertEqual(gs.main(base), 0)
            self.assertEqual(gs.main(base + ["--check"]), 0)
            doc = json.loads(out.read_text())
            doc["creationInfo"]["created"] = "2000-01-01T00:00:00Z"
            out.write_text(gs.render(doc))
            self.assertEqual(gs.main(base + ["--check"]), 0)
            doc["packages"].pop()
            out.write_text(gs.render(doc))
            self.assertEqual(gs.main(base + ["--check"]), 1)


class CommittedLicenceFallbackTests(unittest.TestCase):
    @staticmethod
    def licence(doc, name):
        return {p["name"]: p for p in doc["packages"]}[name]["licenseDeclared"]

    @staticmethod
    def committed_file(tmp, root, cache, licence, version="1.2.3"):
        """Write a committed SBOM built from the full cache, then rewrite Foo.Bar to the given licence and version."""
        doc = gs.build_sbom(root, cache, "2026-01-01T00:00:00Z")
        for pkg in doc["packages"]:
            if pkg["name"] == "Foo.Bar":
                pkg["licenseDeclared"] = licence
                pkg["versionInfo"] = version
        out = pathlib.Path(tmp) / "committed.json"
        out.write_text(gs.render(doc))
        return out

    def test_cache_wins_over_committed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = self.committed_file(tmp, root, cache, "Apache-2.0")
            doc = gs.build_sbom(root, cache, "2026-01-01T00:00:00Z", committed=gs.committed_licences(out))
            self.assertEqual(self.licence(doc, "Foo.Bar"), "MIT")

    def test_committed_licence_used_for_same_name_and_version(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = self.committed_file(tmp, root, cache, "MIT")
            empty = pathlib.Path(tmp) / "empty-cache"
            empty.mkdir()
            doc = gs.build_sbom(root, empty, "2026-01-01T00:00:00Z", committed=gs.committed_licences(out))
            self.assertEqual(self.licence(doc, "Foo.Bar"), "MIT")
            self.assertEqual(self.licence(doc, "Unlicensed"), "NOASSERTION")

    def test_committed_licence_not_carried_to_another_version(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = self.committed_file(tmp, root, cache, "MIT", version="1.2.2")
            empty = pathlib.Path(tmp) / "empty-cache"
            empty.mkdir()
            doc = gs.build_sbom(root, empty, "2026-01-01T00:00:00Z", committed=gs.committed_licences(out))
            self.assertEqual(self.licence(doc, "Foo.Bar"), "NOASSERTION")

    def test_missing_or_unreadable_committed_file_yields_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            bad = pathlib.Path(tmp) / "bad.json"
            bad.write_text("not json")
            self.assertEqual(gs.committed_licences(pathlib.Path(tmp) / "missing.json"), {})
            self.assertEqual(gs.committed_licences(bad), {})

    def test_check_passes_with_empty_cache_when_committed_matches(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = pathlib.Path(tmp) / "sbom.json"
            self.assertEqual(gs.main(["--root", str(root), "--cache", str(cache), "--output", str(out)]), 0)
            empty = pathlib.Path(tmp) / "empty-cache"
            empty.mkdir()
            self.assertEqual(gs.main(["--root", str(root), "--cache", str(empty), "--output", str(out), "--check"]), 0)

    def test_check_fails_with_empty_cache_for_new_package_version(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, cache = tree(tmp)
            out = pathlib.Path(tmp) / "sbom.json"
            self.assertEqual(gs.main(["--root", str(root), "--cache", str(cache), "--output", str(out)]), 0)
            lock = root / "src/P/packages.lock.json"
            lock.write_text(lock.read_text().replace("1.2.3", "1.2.4"))
            empty = pathlib.Path(tmp) / "empty-cache"
            empty.mkdir()
            self.assertEqual(gs.main(["--root", str(root), "--cache", str(empty), "--output", str(out), "--check"]), 1)


if __name__ == "__main__":
    unittest.main()
