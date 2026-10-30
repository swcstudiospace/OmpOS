import pathlib
import subprocess
import tempfile
import unittest

SCRIPT = pathlib.Path(__file__).with_name("check-skip-markers.sh")


def run(files, rows):
    with tempfile.TemporaryDirectory() as tmp:
        root = pathlib.Path(tmp)
        for rel, text in files.items():
            path = root / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")
        docs = root / "docs"
        docs.mkdir()
        (docs / "open-questions.md").write_text("\n".join(
            f"| SUP-{i:03d} | Is the skip marker `{kind}` in `{rel}` still needed? | why |" for i, (kind, rel) in enumerate(rows, 1)), encoding="utf-8")
        return subprocess.run(["bash", str(SCRIPT), str(root)], capture_output=True, text=True)


class SkipMarkerTests(unittest.TestCase):
    def test_matched_marker_passes(self):
        r = run({"tests/A/T.cs": 'Assert.Skip("no");\n'}, [("Assert.Skip", "tests/A/T.cs")])
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_unmatched_marker_fails(self):
        r = run({"tests/A/T.cs": 'Assert.SkipWhen(x, "no");\n'}, [("Assert.Skip", "tests/A/T.cs")])
        self.assertEqual(r.returncode, 1)
        self.assertIn("Assert.SkipWhen in tests/A/T.cs", r.stderr)
        self.assertIn("names no live marker", r.stderr)

    def test_fact_skip_attribute(self):
        r = run({"tests/A/T.cs": '[Fact(SkipUnless = nameof(X), Skip = "no")]\n'}, [("Fact(Skip=...)", "tests/A/T.cs")])
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_comments_and_strings_are_ignored(self):
        r = run({"tests/A/T.cs": '// Assert.Skip("x")\nvar s = "Assert.Skip(";\n'}, [])
        self.assertEqual(r.returncode, 0, r.stderr)

    def test_stale_row_fails(self):
        r = run({"tests/A/T.cs": "class T {}\n"}, [("Assert.Skip", "tests/A/T.cs")])
        self.assertEqual(r.returncode, 1)
        self.assertIn("SUP-001 names no live marker", r.stderr)


if __name__ == "__main__":
    unittest.main()
