#!/usr/bin/env python3
"""Unit tests for rebrand.py. Every test renames inside a throwaway git repo under
/tmp/ag-rename-test/, never in the work tree that holds this file.

    python3 -m unittest scripts/rebrand/test_rebrand.py        # from the repo root
    python3 -m unittest discover -s scripts/rebrand            # or from anywhere
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("rebrand", HERE / "rebrand.py")
rebrand = importlib.util.module_from_spec(_spec)
sys.modules["rebrand"] = rebrand
_spec.loader.exec_module(rebrand)

TMP_BASE = Path("/tmp/ag-rename-test")
CONFIG_PATH = HERE / "rebrand.config.json"
BOUNDARY_WORDS = (
    "compose composer Composer composers decompose decomposition composition Composition component company "
    "compile prompt StringComparison Omps ompost Ompose Omposition recompose COMPOSE ComposerViewModel "
    "LocalPodmanComposeProvider EngineComposition compos theompos IOmpOS"
)


class RepoCase(unittest.TestCase):
    """Base class: a throwaway git repo plus helpers."""

    def setUp(self):
        TMP_BASE.mkdir(parents=True, exist_ok=True)
        self.tmp = Path(tempfile.mkdtemp(dir=TMP_BASE))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.repo = self.tmp / "repo"
        self.reports = self.tmp / "reports"
        self.repo.mkdir()
        self.git("init", "-q")
        self.git("config", "user.email", "t@example.invalid")
        self.git("config", "user.name", "t")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "core.hooksPath", "/dev/null")

    # -- helpers
    def git(self, *args: str) -> str:
        res = subprocess.run(["git", "-C", str(self.repo), *args], capture_output=True, text=True, check=True)
        return res.stdout

    def write(self, rel: str, data: str | bytes) -> Path:
        p = self.repo / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data.encode("utf-8") if isinstance(data, str) else data)
        return p

    def read(self, rel: str) -> str:
        return (self.repo / rel).read_text(encoding="utf-8")

    def readb(self, rel: str) -> bytes:
        return (self.repo / rel).read_bytes()

    def commit(self):
        self.git("add", "-A")
        self.git("commit", "-q", "-m", "snapshot", "--allow-empty")

    def exists(self, rel: str) -> bool:
        return os.path.lexists(self.repo / rel)

    def run_tool(self, command: str, *extra: str, config: Path | None = None):
        argv = [command, "--root", str(self.repo), "--report-dir", str(self.reports),
                "--config", str(config or CONFIG_PATH), *extra]
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stderr(err):
            rc = rebrand.main(argv, out=out)
        return rc, out.getvalue(), err.getvalue()

    def report(self) -> dict:
        return json.loads((self.reports / "rebrand-report.json").read_text(encoding="utf-8"))

    def snapshot(self) -> dict:
        snap = {}
        for dirpath, dirnames, filenames in os.walk(self.repo):
            dirnames[:] = [d for d in dirnames if d != ".git"]
            for name in filenames + [d for d in dirnames if os.path.islink(os.path.join(dirpath, d))]:
                full = Path(dirpath) / name
                rel = str(full.relative_to(self.repo))
                snap[rel] = ("link", os.readlink(full)) if full.is_symlink() else ("file", full.read_bytes(), full.stat().st_mode)
        return snap

    def full_state(self) -> tuple:
        return (self.snapshot(), self.git("ls-files", "-s"), self.git("status", "--porcelain"))

    def apply(self, *extra: str):
        rc, out, err = self.run_tool("apply", *extra)
        self.assertEqual(rc, 0, out + err)
        return out


# --------------------------------------------------------------------------- config


class ConfigTests(unittest.TestCase):
    def test_config_loads_and_is_consistent(self):
        cfg = rebrand.Config.load(CONFIG_PATH)
        self.assertEqual(set(cfg.token_map), {"OmpOS", "OmpOs", "Ompos", "ompOs", "ompos", "OMPOS"})
        self.assertEqual(
            {k: v for k, v in cfg.token_map.items() if k[0] == "O" and k != "OMPOS"} | {"ompOs": cfg.token_map["ompOs"]},
            {"OmpOS": "Autonogrammer", "OmpOs": "Autonogrammer", "Ompos": "Autonogrammer", "ompOs": "autonogrammer"},
        )
        self.assertEqual(cfg.token_map["ompos"], "autonogrammer")
        self.assertEqual(cfg.token_map["OMPOS"], "AUTONOGRAMMER")
        self.assertEqual(len(cfg.abbr), 5)
        for glob, *_rest in cfg.excl:
            pass
        for raw in json.loads(CONFIG_PATH.read_text())["excludedGlobs"]:
            self.assertIsNone(cfg.brand_re.search(raw["glob"]), f"glob {raw['glob']} names a brand segment")

    def test_every_map_key_is_matched_by_the_brand_regex(self):
        cfg = rebrand.Config.load(CONFIG_PATH)
        for key in cfg.token_map:
            m = cfg.brand_re.fullmatch(key)
            self.assertIsNotNone(m, key)

    def test_glob_translation(self):
        rx = rebrand.glob_to_regex("docs/runbooks/evidence/**")
        self.assertTrue(rx.match("docs/runbooks/evidence/a/b.md"))
        self.assertFalse(rx.match("docs/runbooks/evidenceX/b.md"))
        rx = rebrand.glob_to_regex("**/packages.lock.json")
        self.assertTrue(rx.match("packages.lock.json"))
        self.assertTrue(rx.match("src/A/packages.lock.json"))
        rx = rebrand.glob_to_regex("tests/*/TestData/frames/**")
        self.assertTrue(rx.match("tests/X.Tests/TestData/frames/f.ndjson"))
        self.assertFalse(rx.match("tests/X/Y/TestData/frames/f.ndjson"))
        self.assertFalse(rebrand.glob_to_regex("LICENSE").match("docs/LICENSE"))


# --------------------------------------------------------------------------- boundaries and casings


class BoundaryTests(RepoCase):
    def test_words_that_merely_contain_the_letters_never_change(self):
        self.write("a.txt", BOUNDARY_WORDS + "\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("a.txt"), BOUNDARY_WORDS + "\n")

    def test_each_casing_form(self):
        cases = {
            "OmpOS": "Autonogrammer", "OmpOs": "Autonogrammer", "Ompos": "Autonogrammer",
            "ompOs": "autonogrammer", "ompos": "autonogrammer", "OMPOS": "AUTONOGRAMMER",
        }
        body = "\n".join(f"x {k} y" for k in cases) + "\n"
        self.write("c.txt", body)
        self.commit()
        self.apply()
        self.assertEqual(self.read("c.txt"), "\n".join(f"x {v} y" for v in cases.values()) + "\n")

    def test_identifier_shapes(self):
        pairs = {
            "OmpOSTokens": "AutonogrammerTokens",
            "theOmpOS": "theAutonogrammer",
            "OmpOsJsonContext": "AutonogrammerJsonContext",
            "ompOsFile": "autonogrammerFile",
            "OMPOS_API_ORIGIN": "AUTONOGRAMMER_API_ORIGIN",
            "OMPOS_LIVE_OMP": "AUTONOGRAMMER_LIVE_OMP",
            "--ompos-gap-8": "--autonogrammer-gap-8",
            "ompos-gap": "autonogrammer-gap",
            "X-Ompos-Contract-Version": "X-Autonogrammer-Contract-Version",
            "X-OmpOS-Signature": "X-Autonogrammer-Signature",
            ".ompos/mcp.json": ".autonogrammer/mcp.json",
            "ompos://terminal/1": "autonogrammer://terminal/1",
            "com.swcstudiospace.ompos": "com.swcstudiospace.autonogrammer",
            "io.ompos.cloud-agent": "io.autonogrammer.cloud-agent",
            "ompos_hooks_consent": "autonogrammer_hooks_consent",
            "OmpOS.Harness.Remote": "Autonogrammer.Harness.Remote",
            "avares://OmpOS.Native.Views/x.axaml": "avares://Autonogrammer.Native.Views/x.axaml",
            "ghcr.io/swcstudiospace/ompos-agent-worker:1": "ghcr.io/swcstudiospace/autonogrammer-agent-worker:1",
            "plugins/ompos-hooks-omp": "plugins/autonogrammer-hooks-omp",
            "ompos.cloud.sessions": "autonogrammer.cloud.sessions",
            "/etc/ompos/hooks.json": "/etc/autonogrammer/hooks.json",
            "OmpOS2": "Autonogrammer2",
        }
        self.write("ids.txt", "\n".join(pairs) + "\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("ids.txt"), "\n".join(pairs.values()) + "\n")

    def test_escape_sequence_letter_before_a_token_does_not_hide_it(self):
        self.write("a.cs", 'var s = "Welcome\\nOMPOS_SIG 1\\n\\tompos x";\nvar t = "nOMPOS and tompos";\n')
        self.commit()
        self.apply()
        self.assertEqual(self.read("a.cs"),
                         'var s = "Welcome\\nAUTONOGRAMMER_SIG 1\\n\\tautonogrammer x";\nvar t = "nOMPOS and tompos";\n')

    def test_dry_run_reports_rejects_with_reasons_and_flags_suspects(self):
        self.write("a.txt", BOUNDARY_WORDS + "\nOmpOS x\n")
        self.commit()
        rc, out, _ = self.run_tool("dry-run")
        self.assertEqual(rc, 0)
        rep = self.report()
        by_word = {r["word"]: r for r in rep["rejected"]}
        self.assertEqual(by_word["compose"]["severity"], "benign")
        self.assertIn("compose", by_word["compose"]["reason"])
        self.assertEqual(by_word["IOmpOS"]["severity"], "suspect")
        self.assertIn("left edge", by_word["IOmpOS"]["reason"])
        self.assertEqual(by_word["Omposition"]["severity"], "suspect")
        self.assertEqual(by_word["theompos"]["severity"], "suspect")
        self.assertIn("prompt", rep["otherOmpWords"])
        self.assertIn("Omps", rep["otherOmpWords"])
        self.assertIn("compose", out)
        self.assertEqual(sorted(w for w, r in by_word.items() if r["severity"] == "suspect"),
                         ["IOmpOS", "Ompose", "Omposition", "ompost", "theompos"])
        self.assertEqual(rep["summary"]["suspectRejectedWords"], 5)

    def test_dry_run_token_table_has_counts_and_two_examples(self):
        self.write("a.txt", "OmpOS\nOmpOS\nOmpOS\nompos\n")
        self.write("b.txt", "OmpOS\n")
        self.commit()
        self.run_tool("dry-run")
        toks = {t["token"]: t for t in self.report()["tokens"]}
        self.assertEqual(toks["OmpOS"]["count"], 4)
        self.assertEqual(toks["OmpOS"]["files"], 2)
        self.assertEqual(toks["OmpOS"]["examples"], ["a.txt:1", "a.txt:2"])
        self.assertEqual(toks["ompos"]["examples"], ["a.txt:4"])
        self.assertEqual(toks["OmpOS"]["replacement"], "Autonogrammer")

    def test_unmapped_casing_uses_fallback_and_is_reported(self):
        self.write("a.txt", "OmpoS here\n")
        self.commit()
        self.run_tool("dry-run")
        self.assertEqual(self.report()["unmappedCasings"], {"OmpoS": 1})
        self.apply()
        self.assertEqual(self.read("a.txt"), "Autonogrammer here\n")

    def test_digit_adjacent_tokens_are_flagged(self):
        self.write("a.txt", "abc9ompos7 and plain ompos\n")
        self.commit()
        self.run_tool("dry-run")
        self.assertEqual([f["word"] for f in self.report()["flags"]], ["abc9ompos7"])


# --------------------------------------------------------------------------- engine protection


ENGINE_TEXT = (
    "omp acp\n"
    "omp --mode rpc --no-ui\n"
    "~/.omp/agent/agents and $HOME/.omp and .omp/rules\n"
    "OMP_PROFILE OMP_AUTH_BROKER_URL PI_CODING_AGENT_DIR\n"
    "omp://providers.md\n"
    "@oh-my-pi/pi-coding-agent oh-my-pi\n"
    "models.yml agent.db\n"
    "omp_result omp.extensions\n"
    "SUBSTRATE_TOKEN_OMP FAKE_OMP_PATH\n"
    "plugins/gsd-core-omp\n"
    "Omp could not take the message.\n"
    "OmpLauncher OmpAcpClient FakeOmp ompProviderId Harness__Acp__OmpPath\n"
)


class EngineTests(RepoCase):
    def test_engine_tokens_survive_brand_rename(self):
        self.write("e.txt", ENGINE_TEXT + "OmpOS runs omp acp and OMPOS_LIVE_OMP.\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("e.txt"), ENGINE_TEXT + "Autonogrammer runs omp acp and AUTONOGRAMMER_LIVE_OMP.\n")

    def test_hooks_plugin_keeps_omp_suffix_but_loses_brand(self):
        self.write("plugins/ompos-hooks-omp/package.json", '{"name": "ompos-hooks-omp", "omp": {"extensions": []}}\n')
        self.write("plugins/gsd-core-omp/index.ts", "// OmpOS steering\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("plugins/autonogrammer-hooks-omp/package.json"),
                         '{"name": "autonogrammer-hooks-omp", "omp": {"extensions": []}}\n')
        self.assertEqual(self.read("plugins/gsd-core-omp/index.ts"), "// Autonogrammer steering\n")

    def test_product_abbreviations_are_renamed_exactly(self):
        self.write("tests/OmpOS.Api.IntegrationTests/OmpApiFactory.cs",
                   "class OmpApiFactory : F<Program> {}\nclass OmpApiFactoryHelper {}\nvar x = new OmpApiFactory();\n")
        self.write("services/OmpOS.Api/Telemetry/OmpTelemetry.cs", "static class OmpTelemetry { const string S = \"OmpOS.Api\"; }\n")
        self.write("services/OmpOS.Api/Reg.cs", "s.AddOmpOpenApi().AddOmpWebServices().AddOmpWebDemoServices().AddOmpOpenApiX();\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("tests/Autonogrammer.Api.IntegrationTests/ApiFactory.cs"),
                         "class ApiFactory : F<Program> {}\nclass OmpApiFactoryHelper {}\nvar x = new ApiFactory();\n")
        self.assertEqual(self.read("services/Autonogrammer.Api/Telemetry/ApiTelemetry.cs"),
                         'static class ApiTelemetry { const string S = "Autonogrammer.Api"; }\n')
        self.assertEqual(self.read("services/Autonogrammer.Api/Reg.cs"),
                         "s.AddAutonogrammerOpenApi().AddAutonogrammerWebServices().AddAutonogrammerWebDemoServices().AddOmpOpenApiX();\n")

    def test_engine_adapter_identifiers_are_kept_by_default_and_counted(self):
        self.write("src/OmpOS.Harness.OmpLauncher/OmpLauncher.cs", "class OmpLauncher { OmpPath p; FakeOmp f; }\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("src/Autonogrammer.Harness.OmpLauncher/OmpLauncher.cs"),
                         "class OmpLauncher { OmpPath p; FakeOmp f; }\n")
        self.run_tool("dry-run")
        eng = self.report()["engine"]
        self.assertEqual(eng["mode"], "keep")
        self.assertGreaterEqual(eng["adapterIdentifiers"]["distinct"], 3)

    def test_engine_adapter_switch_renames_identifiers_only(self):
        self.write("src/OmpOS.Harness.OmpLauncher/OmpLauncher.cs", "class OmpLauncher { OmpPath p; FakeOmp f; int ompProviderId; }\n")
        self.write("e.txt", ENGINE_TEXT)
        self.commit()
        self.apply("--engine-adapters", "rename")
        self.assertEqual(self.read("src/Autonogrammer.Harness.AutonogrammerLauncher/AutonogrammerLauncher.cs"),
                         "class AutonogrammerLauncher { AutonogrammerPath p; FakeAutonogrammer f; int autonogrammerProviderId; }\n")
        text = self.read("e.txt")
        for kept in ("omp acp", "omp --mode rpc", "~/.omp/agent", "$HOME/.omp", ".omp/rules", "OMP_PROFILE",
                     "PI_CODING_AGENT_DIR", "omp://providers.md", "@oh-my-pi/pi-coding-agent", "models.yml",
                     "agent.db", "omp_result", "omp.extensions", "SUBSTRATE_TOKEN_OMP", "FAKE_OMP_PATH",
                     "gsd-core-omp", "Omp could not take the message."):
            self.assertIn(kept, text)
        self.assertIn("AutonogrammerLauncher", text)
        self.assertIn("Harness__Acp__AutonogrammerPath", text)

    def test_keep_pattern_counts_are_recorded_as_proof(self):
        self.write("e.txt", ENGINE_TEXT)
        self.commit()
        self.run_tool("dry-run")
        counts = self.report()["engine"]["keepPatternCounts"]
        self.assertEqual(counts["omp acp"], 1)
        self.assertEqual(counts["omp://"] if "omp://" in counts else counts["omp:// URLs"], 1)
        self.assertEqual(counts["OMP_* variables"], 2)


# --------------------------------------------------------------------------- urls


URL_TEXT = (
    "see https://github.com/swcstudiospace/OmpOS/issues\n"
    "git@github.com:swcstudiospace/OmpOS.git\n"
    "https://ompos-web.vercel.app and https://ompos-px32dymvw-swcstudiospace.vercel.app\n"
    "http://ompos-api.railway.internal:8080\n"
    "The Railway service ompos-api exposes it.\n"
    "issuer is ompos-api in code\n"
    "http://ompos-agent-s1.railway.internal:8080/\n"
    "schema https://ompos.dev/schemas/hooks.v1.json\n"
    "ghcr.io/swcstudiospace/ompos-agent-worker:1 and https://web.ompos.local/\n"
)


class UrlTests(RepoCase):
    def test_external_names_are_protected_and_listed_by_default(self):
        self.write("u.md", URL_TEXT)
        self.commit()
        self.apply()
        self.assertEqual(
            self.read("u.md"),
            "see https://github.com/swcstudiospace/OmpOS/issues\n"
            "git@github.com:swcstudiospace/OmpOS.git\n"
            "https://ompos-web.vercel.app and https://ompos-px32dymvw-swcstudiospace.vercel.app\n"
            "http://ompos-api.railway.internal:8080\n"
            "The Railway service ompos-api exposes it.\n"
            "issuer is autonogrammer-api in code\n"
            "http://autonogrammer-agent-s1.railway.internal:8080/\n"
            "schema https://ompos.dev/schemas/hooks.v1.json\n"
            "ghcr.io/swcstudiospace/autonogrammer-agent-worker:1 and https://web.autonogrammer.local/\n",
        )

    def test_dry_run_lists_every_protected_occurrence_as_owner_action(self):
        self.write("u.md", URL_TEXT)
        self.commit()
        rc, out, _ = self.run_tool("dry-run")
        rep = self.report()
        texts = [h["text"] for h in rep["urlProtected"]]
        self.assertEqual(len(texts), 7, texts)
        self.assertIn("swcstudiospace/OmpOS", texts)
        self.assertIn("ompos-web.vercel.app", texts)
        self.assertIn("ompos.dev", texts)
        self.assertTrue(all(h["file"] == "u.md" and h["line"] >= 1 for h in rep["urlProtected"]))
        self.assertTrue(any("GitHub repo" in a for a in rep["ownerActions"]))
        self.assertIn("swcstudiospace/OmpOS", out)

    def test_urls_flag_rewrites_them(self):
        self.write("u.md", URL_TEXT)
        self.commit()
        self.apply("--urls")
        text = self.read("u.md")
        self.assertNotRegex(text, r"(?i)ompos")
        self.assertIn("https://github.com/swcstudiospace/Autonogrammer/issues", text)
        self.assertIn("https://autonogrammer-web.vercel.app", text)
        self.assertIn("autonogrammer.dev", text)

    def test_protected_url_does_not_shield_other_tokens_on_the_same_line(self):
        self.write("u.md", "OmpOS lives at https://github.com/swcstudiospace/OmpOS and ompos is the brand\n")
        self.commit()
        self.apply()
        self.assertEqual(self.read("u.md"),
                         "Autonogrammer lives at https://github.com/swcstudiospace/OmpOS and autonogrammer is the brand\n")

    def test_verify_tolerates_protected_urls_but_reports_them(self):
        self.write("u.md", "https://github.com/swcstudiospace/OmpOS\n")
        self.commit()
        self.apply()
        rc, out, _ = self.run_tool("verify")
        self.assertEqual(rc, 0, out)
        self.assertIn("1 URL tokens still protected", out)


# --------------------------------------------------------------------------- paths


class PathTests(RepoCase):
    def build_tree(self):
        self.write("OmpOS.slnx", "<Project Path=\"src/OmpOS.Foo/OmpOS.Foo.csproj\" />\n")
        self.write("src/OmpOS.Foo/OmpOS.Foo.csproj", "<Project><ItemGroup><ProjectReference Include=\"../OmpOS.Bar/OmpOS.Bar.csproj\" /></ItemGroup></Project>\n")
        self.write("src/OmpOS.Foo/Sub/OmposThing.cs", "class OmposThing {}\n")
        self.write("src/OmpOS.Foo/OmpOsJsonContext.cs", "class OmpOsJsonContext {}\n")
        self.write("src/OmpOS.Bar/OmpOS.Bar.csproj", "<Project/>\n")
        self.write("tests/OmpOS.Foo.Tests/OmpOS.Foo.Tests.csproj", "<Project/>\n")
        self.write("tests/OmpOS.Foo.Tests/ompos-fixture.json", "{}\n")
        self.write("plugins/ompos-hooks-omp/src/bridge.mjs", "export {}\n")
        self.write("docs/guide.md", "# OmpOS guide\n")
        self.write("plain/readme.md", "nothing here\n")

    def test_moves_are_git_mv_and_deepest_first(self):
        self.build_tree()
        self.commit()
        rc, out, err = self.run_tool("dry-run")
        self.assertEqual(rc, 0, err)
        ops = self.report()["paths"]["ops"]
        depths = [o["depth"] for o in ops]
        self.assertEqual(depths, sorted(depths, reverse=True))
        for o in ops:
            if o["type"] == "file":
                self.assertEqual(os.path.dirname(o["old"]), os.path.dirname(o["new"]))
        self.apply()
        for rel in (
            "Autonogrammer.slnx",
            "src/Autonogrammer.Foo/Autonogrammer.Foo.csproj",
            "src/Autonogrammer.Foo/Sub/AutonogrammerThing.cs",
            "src/Autonogrammer.Foo/AutonogrammerJsonContext.cs",
            "src/Autonogrammer.Bar/Autonogrammer.Bar.csproj",
            "tests/Autonogrammer.Foo.Tests/Autonogrammer.Foo.Tests.csproj",
            "tests/Autonogrammer.Foo.Tests/autonogrammer-fixture.json",
            "plugins/autonogrammer-hooks-omp/src/bridge.mjs",
            "plain/readme.md",
        ):
            self.assertTrue(self.exists(rel), rel)
        for rel in ("OmpOS.slnx", "src/OmpOS.Foo", "src/OmpOS.Bar", "tests/OmpOS.Foo.Tests", "plugins/ompos-hooks-omp"):
            self.assertFalse(self.exists(rel), rel)
        self.assertEqual(
            self.read("src/Autonogrammer.Foo/Autonogrammer.Foo.csproj"),
            "<Project><ItemGroup><ProjectReference Include=\"../Autonogrammer.Bar/Autonogrammer.Bar.csproj\" /></ItemGroup></Project>\n",
        )
        status = self.git("status", "--porcelain")
        self.assertRegex(status, r"(?m)^R[ M] src/OmpOS\.Foo/Sub/OmposThing\.cs -> src/Autonogrammer\.Foo/Sub/AutonogrammerThing\.cs")
        self.assertRegex(status, r"(?m)^R[ M] OmpOS\.slnx -> Autonogrammer\.slnx")
        self.assertEqual(self.git("log", "--oneline").count("\n"), 1, "the tool must never commit")

    def test_plan_counts_directories_and_files(self):
        self.build_tree()
        self.commit()
        self.run_tool("dry-run")
        paths = self.report()["paths"]
        self.assertEqual(paths["dirs"], 4)  # src/OmpOS.Foo, src/OmpOS.Bar, tests/OmpOS.Foo.Tests, plugins/ompos-hooks-omp
        self.assertEqual(paths["innerFiles"], 6)
        self.assertEqual(paths["looseFiles"], 1)
        self.assertEqual(paths["collisions"], [])

    def test_collision_with_existing_file_refuses_and_changes_nothing(self):
        self.write("OmpOS.txt", "a\n")
        self.write("Autonogrammer.txt", "b\n")
        self.write("note.md", "OmpOS\n")
        self.commit()
        before = self.full_state()
        rc, out, err = self.run_tool("dry-run")
        self.assertEqual(rc, 2)
        self.assertIn("COLLISION", out)
        rc, out, err = self.run_tool("apply")
        self.assertEqual(rc, 2)
        self.assertIn("collision", err)
        self.assertEqual(self.full_state(), before)

    def test_collision_between_two_sources_is_case_insensitive(self):
        self.write("a/OmpOS.x", "1\n")
        self.write("a/ompos.x", "2\n")  # -> Autonogrammer.x and autonogrammer.x
        self.commit()
        rc, _, err = self.run_tool("apply")
        self.assertEqual(rc, 2)
        self.assertIn("collision", err)
        self.assertTrue(self.exists("a/OmpOS.x"))

    def test_collision_with_existing_directory_refuses(self):
        self.write("src/OmpOS.A/f.cs", "x\n")
        self.write("src/Autonogrammer.A/g.cs", "y\n")
        self.commit()
        rc, _, err = self.run_tool("apply")
        self.assertEqual(rc, 2)
        self.assertIn("already exists", err)
        self.assertTrue(self.exists("src/OmpOS.A/f.cs"))

    def test_collision_with_ignored_directory_on_disk_refuses(self):
        self.write(".gitignore", "obj/\n")
        self.write("src/OmpOS.A/f.cs", "x\n")
        self.commit()
        self.write("src/Autonogrammer.A/obj/o.bin", "y\n")  # ignored, invisible to git, present on disk
        self.assertEqual(self.git("status", "--porcelain"), "")
        rc, _, err = self.run_tool("apply")
        self.assertEqual(rc, 2)
        self.assertIn("already exists on disk", err)
        self.assertTrue(self.exists("src/OmpOS.A/f.cs"))

    def test_mixed_directory_refuses(self):
        cfg = json.loads(CONFIG_PATH.read_text())
        cfg["excludedGlobs"].append({"glob": "src/OmpOS.Mixed/keep.txt", "class": "pin", "content": True, "path": True, "reason": "t"})
        cfg_path = self.tmp / "mixed.json"
        cfg_path.write_text(json.dumps(cfg))
        self.write("src/OmpOS.Mixed/keep.txt", "k\n")
        self.write("src/OmpOS.Mixed/other.txt", "o\n")
        self.commit()
        rc, _, err = self.run_tool("apply", config=cfg_path)
        self.assertEqual(rc, 2)
        self.assertIn("mixed directory", err)

    def test_untracked_paths_are_renamed_with_os_rename(self):
        self.write("keep.txt", "k\n")
        self.commit()
        self.write("src/OmpOS.New/f.cs", "OmpOS\n")
        rc, _, err = self.run_tool("apply", "--allow-dirty")
        self.assertEqual(rc, 0, err)
        self.assertEqual(self.read("src/Autonogrammer.New/f.cs"), "Autonogrammer\n")
        self.assertFalse(self.exists("src/OmpOS.New"))

    def test_dirty_tree_is_refused_unless_allowed(self):
        self.write("a.txt", "OmpOS\n")
        self.commit()
        self.write("scratch.txt", "dirty\n")
        rc, _, err = self.run_tool("apply")
        self.assertEqual(rc, 2)
        self.assertIn("dirty", err)
        self.assertEqual(self.read("a.txt"), "OmpOS\n")
        rc, _, err = self.run_tool("apply", "--allow-dirty")
        self.assertEqual(rc, 0, err)
        self.assertEqual(self.read("a.txt"), "Autonogrammer\n")


# --------------------------------------------------------------------------- idempotency and dry-run


class IdempotencyTests(RepoCase):
    def seed(self):
        self.write("src/OmpOS.Foo/OmpOS.Foo.csproj", "<Project>OmpOS</Project>\n")
        self.write("README.md", "OmpOS and ompos and OMPOS_X and compose\n")
        self.write("docs/runbooks/evidence/e.md", "OmpOS\n")
        self.commit()

    def test_second_apply_changes_nothing(self):
        self.seed()
        self.apply()
        self.commit()
        first = self.full_state()
        out = self.apply()
        self.assertIn("0 moves, 0 files rewritten", out)
        self.assertEqual(self.full_state(), first)
        rc, out, _ = self.run_tool("dry-run")
        s = self.report()["summary"]
        self.assertEqual((s["brandTokensToRewrite"], s["pathOps"], s["filesToRewrite"]), (0, 0, 0))

    def test_applying_to_a_newer_base_renames_only_the_new_content(self):
        self.seed()
        self.apply()
        self.commit()
        old_bytes = self.snapshot()
        self.write("src/OmpOS.Newer/OmpOS.Newer.csproj", "<Project>OmpOS</Project>\n")
        self.write("README.md", self.read("README.md") + "a new ompos line\n")
        self.commit()
        self.run_tool("dry-run")
        rep = self.report()
        self.assertEqual(rep["summary"]["dirOps"], 1)
        self.assertEqual(rep["summary"]["fileOps"], 1)
        self.assertEqual(rep["summary"]["filesToRewrite"], 2)
        self.apply()
        self.assertEqual(self.read("README.md"), "Autonogrammer and autonogrammer and AUTONOGRAMMER_X and compose\na new autonogrammer line\n")
        self.assertEqual(self.read("src/Autonogrammer.Newer/Autonogrammer.Newer.csproj"), "<Project>Autonogrammer</Project>\n")
        for rel, val in old_bytes.items():
            if rel != "README.md":
                self.assertEqual(self.snapshot()[rel], val, rel)

    def test_dry_run_writes_nothing_to_the_repo(self):
        self.seed()
        before = self.full_state()
        index_before = (self.repo / ".git" / "index").read_bytes()
        rc, out, err = self.run_tool("dry-run")
        self.assertEqual(rc, 0, err)
        self.assertEqual(self.full_state(), before)
        self.assertEqual((self.repo / ".git" / "index").read_bytes(), index_before)
        self.assertTrue((self.reports / "rebrand-report.md").is_file())
        self.assertTrue((self.reports / "rebrand-report.json").is_file())
        self.assertEqual(self.git("log", "--oneline").count("\n"), 1)

    def test_default_subcommand_is_dry_run(self):
        self.seed()
        before = self.full_state()
        out = io.StringIO()
        rc = rebrand.main(["--root", str(self.repo), "--report-dir", str(self.reports), "--config", str(CONFIG_PATH)], out=out)
        self.assertEqual(rc, 0)
        self.assertEqual(self.full_state(), before)
        self.assertIn("Matched brand tokens", out.getvalue())

    def test_report_dir_inside_root_is_refused(self):
        self.seed()
        out = io.StringIO()
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            rc = rebrand.main(["dry-run", "--root", str(self.repo), "--report-dir", str(self.repo / "rep"),
                               "--config", str(CONFIG_PATH)], out=out)
        self.assertEqual(rc, 2)
        self.assertFalse((self.repo / "rep").exists())

    def test_cli_entry_point_runs_as_a_script(self):
        self.seed()
        res = subprocess.run(
            [sys.executable, str(HERE / "rebrand.py"), "--root", str(self.repo), "--report-dir", str(self.reports)],
            capture_output=True, text=True,
        )
        self.assertEqual(res.returncode, 0, res.stderr)
        self.assertIn("Rebrand report (dry-run)", res.stdout)
        res = subprocess.run([sys.executable, str(HERE / "rebrand.py"), "--help"], capture_output=True, text=True)
        self.assertEqual(res.returncode, 0)
        self.assertIn("--engine-adapters", res.stdout)


# --------------------------------------------------------------------------- exclusions, binaries, encodings


class ExclusionTests(RepoCase):
    def test_binary_and_excluded_content(self):
        png = b"\x89PNG\r\n\x1a\n\x00\x00OmpOS\x00ompos"
        self.write("assets/brand/ompos-mark-256.png", png)
        self.write("data/blob.bin", b"\x00\x01OmpOS\x00\x02")
        self.write("apps/OmpOS.Native/Assets/ompos.ico", b"\x00\x00\x01\x00OmpOS")
        self.write("docs/runbooks/evidence/railway-ompos-api-2026-10-21.md", "OmpOS run\n")
        self.write("docs/runbooks/evidence/sub/OmpOS/x.txt", "OmpOS\n")
        self.write(".planning/milestones/v0.9.1/frames/rpc.ndjson", '{"x":"/tmp/ompos-rpc-live"}\n')
        self.write(".planning/phases/p/PLAN.md", "OmpOS phase\n")
        self.write(".gitleaksignore", "abc:tests/OmpOS.X/f.ndjson:generic-api-key:4\n")
        self.write("LICENSE", "OmpOS text\n")
        self.write("LICENSES/MIT.txt", "OmpOS text\n")
        self.write("THIRD-PARTY-NOTICES.md", "src/OmpOS.UI\n")
        self.write("docs/api/openapi.json", '{"title": "OmpOS.Api | v1"}\n')
        self.write("src/OmpOS.X/packages.lock.json", '{"ompos.contracts": {"type": "Project"}}\n')
        self.write("tests/OmpOS.Acp.Tests/TestData/frames/acp.ndjson", '{"m":"/tmp/ompos-rpc-live"}\n')
        self.write("scripts/rebrand/note.txt", "OmpOS\n")
        self.write("docs/runbooks/rebrand-autonogrammer.md", "OmpOS\n")
        self.write("docs/plans/v0.16.0-research/01-rename-inventory.md", "OmpOS\n")
        self.write("docs/plans/v0.16.0-research/02-ui.md", "OmpOS ui\n")
        self.commit()
        self.apply()
        # binaries: bytes identical, brand-named paths renamed
        self.assertEqual(self.readb("assets/brand/autonogrammer-mark-256.png"), png)
        self.assertEqual(self.readb("data/blob.bin"), b"\x00\x01OmpOS\x00\x02")
        self.assertEqual(self.readb("apps/Autonogrammer.Native/Assets/autonogrammer.ico"), b"\x00\x00\x01\x00OmpOS")
        # frozen content and paths
        self.assertEqual(self.read("docs/runbooks/evidence/railway-ompos-api-2026-10-21.md"), "OmpOS run\n")
        self.assertEqual(self.read("docs/runbooks/evidence/sub/OmpOS/x.txt"), "OmpOS\n")
        self.assertEqual(self.read(".planning/milestones/v0.9.1/frames/rpc.ndjson"), '{"x":"/tmp/ompos-rpc-live"}\n')
        self.assertEqual(self.read(".gitleaksignore"), "abc:tests/OmpOS.X/f.ndjson:generic-api-key:4\n")
        self.assertEqual(self.read("LICENSE"), "OmpOS text\n")
        self.assertEqual(self.read("LICENSES/MIT.txt"), "OmpOS text\n")
        self.assertEqual(self.read("THIRD-PARTY-NOTICES.md"), "src/OmpOS.UI\n")
        self.assertEqual(self.read("docs/api/openapi.json"), '{"title": "OmpOS.Api | v1"}\n')
        self.assertEqual(self.read("src/Autonogrammer.X/packages.lock.json"), '{"ompos.contracts": {"type": "Project"}}\n')
        self.assertEqual(self.read("tests/Autonogrammer.Acp.Tests/TestData/frames/acp.ndjson"), '{"m":"/tmp/ompos-rpc-live"}\n')
        # the rename docs
        self.assertEqual(self.read("scripts/rebrand/note.txt"), "OmpOS\n")
        self.assertEqual(self.read("docs/runbooks/rebrand-autonogrammer.md"), "OmpOS\n")
        self.assertEqual(self.read("docs/plans/v0.16.0-research/01-rename-inventory.md"), "OmpOS\n")
        # everything else is rewritten
        self.assertEqual(self.read(".planning/phases/p/PLAN.md"), "Autonogrammer phase\n")
        self.assertEqual(self.read("docs/plans/v0.16.0-research/02-ui.md"), "Autonogrammer ui\n")

    def test_excluded_tokens_are_counted_per_class(self):
        self.write("LICENSES/MIT.txt", "OmpOS OmpOS\n")
        self.write("docs/runbooks/evidence/e.md", "OmpOS\n")
        self.write("a.md", "OmpOS\n")
        self.commit()
        self.run_tool("dry-run")
        exc = self.report()["categories"]["excludedContent"]
        self.assertEqual(exc["licence"]["tokens"], 2)
        self.assertEqual(exc["evidence"]["tokens"], 1)
        self.assertEqual(self.report()["categories"]["zones"]["root"]["tokens"], 1)

    def test_legacy_name_holder_file_keeps_its_old_literals_on_every_rerun(self):
        legacy = 'internal static class LegacyBrandNames { public const string Dir = "OmpOS"; public const string Marker = "# ompos:begin"; }\n'
        self.write("src/OmpOS.Harness.OmpConfig/LegacyBrandNames.cs", legacy)
        self.write("src/OmpOS.Harness.OmpConfig/Other.cs", 'const string Dir = "OmpOS";\n')
        self.commit()
        self.apply()
        self.assertEqual(self.read("src/Autonogrammer.Harness.OmpConfig/LegacyBrandNames.cs"), legacy)
        self.assertEqual(self.read("src/Autonogrammer.Harness.OmpConfig/Other.cs"), 'const string Dir = "Autonogrammer";\n')
        self.commit()
        self.apply()
        self.assertEqual(self.read("src/Autonogrammer.Harness.OmpConfig/LegacyBrandNames.cs"), legacy)

    def test_changelog_released_sections_are_frozen(self):
        text = (
            "# Changelog of OmpOS\n\n## [Unreleased]\n\n- renamed OmpOS things\n\n"
            "## [0.1.0] - 2026-01-01\n\n- First OmpOS release (ompos)\n\n"
            "[Unreleased]: https://github.com/swcstudiospace/OmpOS/compare/v0.1.0...HEAD\n"
            "[0.1.0]: https://github.com/swcstudiospace/OmpOS/releases/tag/v0.1.0\n"
        )
        self.write("CHANGELOG.md", text)
        self.commit()
        self.apply()
        want = text.replace("# Changelog of OmpOS", "# Changelog of Autonogrammer").replace("renamed OmpOS things", "renamed Autonogrammer things")
        self.assertEqual(self.read("CHANGELOG.md"), want)
        self.commit()
        self.apply("--urls")
        want = want.replace("swcstudiospace/OmpOS", "swcstudiospace/Autonogrammer")
        self.assertEqual(self.read("CHANGELOG.md"), want)
        self.assertIn("First OmpOS release (ompos)", want)

    def test_bom_and_crlf_are_preserved(self):
        self.write("bom.cs", b"\xef\xbb\xbfnamespace OmpOS.Core;\r\n// ompos\r\n")
        self.write("crlf.txt", b"OmpOS\r\nline two ompos\r\nlast OMPOS_X without newline")
        self.write("lf.txt", b"OmpOS\nompos\n")
        self.write("mixed.txt", b"OmpOS\r\nompos\n")
        self.commit()
        self.apply()
        self.assertEqual(self.readb("bom.cs"), b"\xef\xbb\xbfnamespace Autonogrammer.Core;\r\n// autonogrammer\r\n")
        self.assertEqual(self.readb("crlf.txt"), b"Autonogrammer\r\nline two autonogrammer\r\nlast AUTONOGRAMMER_X without newline")
        self.assertEqual(self.readb("lf.txt"), b"Autonogrammer\nautonogrammer\n")
        self.assertEqual(self.readb("mixed.txt"), b"Autonogrammer\r\nautonogrammer\n")

    def test_non_utf8_text_is_left_alone_and_reported(self):
        raw = b"caf\xe9 OmpOS\n"
        self.write("latin1.txt", raw)
        self.write("utf16.txt", "OmpOS".encode("utf-16"))
        self.commit()
        self.apply()
        self.assertEqual(self.readb("latin1.txt"), raw)
        self.assertEqual(self.readb("utf16.txt"), "OmpOS".encode("utf-16"))
        self.run_tool("dry-run")
        self.assertIn("latin1.txt", self.report()["nonUtf8"])

    def test_file_mode_is_preserved(self):
        p = self.write("run.sh", "#!/bin/sh\necho OmpOS\n")
        p.chmod(0o755)
        self.commit()
        self.apply()
        self.assertEqual(os.stat(self.repo / "run.sh").st_mode & 0o777, 0o755)
        self.assertEqual(self.read("run.sh"), "#!/bin/sh\necho Autonogrammer\n")

    def test_symlinks_are_untouched(self):
        self.write("real/target.txt", "OmpOS\n")
        os.symlink("target.txt", self.repo / "real" / "OmpOS.link")
        os.symlink("../real/OmpOS.txt", self.repo / "dangling-ompos")
        self.write("src/OmpOS.D/f.txt", "x\n")
        os.symlink("f.txt", self.repo / "src" / "OmpOS.D" / "inner-link")
        os.symlink("src/OmpOS.D", self.repo / "dir-link")
        self.commit()
        self.apply()
        self.assertEqual(os.readlink(self.repo / "real" / "OmpOS.link"), "target.txt")
        self.assertEqual(os.readlink(self.repo / "dangling-ompos"), "../real/OmpOS.txt")
        self.assertEqual(os.readlink(self.repo / "dir-link"), "src/OmpOS.D")
        self.assertEqual(os.readlink(self.repo / "src" / "Autonogrammer.D" / "inner-link"), "f.txt")
        self.assertEqual(self.read("real/target.txt"), "Autonogrammer\n")
        self.assertFalse(self.exists("src/OmpOS.D"))
        rc, _, _ = self.run_tool("dry-run")
        self.assertEqual(self.report()["summary"]["symlinksSkipped"], 4)


# --------------------------------------------------------------------------- verify


class VerifyTests(RepoCase):
    def seed(self):
        self.write("src/OmpOS.Foo/a.cs", "OmpOS compose composer OMP_X omp acp ~/.omp\n")
        self.write("docs/e.md", "decompose OmpOS decomposition\n")
        self.commit()

    def test_verify_fails_before_and_passes_after_apply(self):
        self.seed()
        rc, out, _ = self.run_tool("verify")
        self.assertEqual(rc, 1)
        self.assertIn("verify FAILED", out)
        self.run_tool("dry-run")
        baseline = self.tmp / "baseline.json"
        shutil.copy(self.reports / "rebrand-report.json", baseline)
        self.apply()
        rc, out, _ = self.run_tool("verify", "--baseline", str(baseline))
        self.assertEqual(rc, 0, out)
        self.assertIn("verify OK", out)

    def test_verify_with_baseline_detects_substring_damage(self):
        self.seed()
        self.run_tool("dry-run")
        baseline = self.tmp / "baseline.json"
        shutil.copy(self.reports / "rebrand-report.json", baseline)
        self.apply()
        self.write("docs/e.md", self.read("docs/e.md").replace("decompose", "deautonogrammer"))
        rc, out, _ = self.run_tool("verify", "--baseline", str(baseline))
        self.assertEqual(rc, 1)
        self.assertIn("rejected near-match", out)

    def test_verify_with_baseline_detects_engine_token_damage(self):
        self.seed()
        self.run_tool("dry-run")
        baseline = self.tmp / "baseline.json"
        shutil.copy(self.reports / "rebrand-report.json", baseline)
        self.apply()
        self.write("src/Autonogrammer.Foo/a.cs", self.read("src/Autonogrammer.Foo/a.cs").replace("omp acp", "autonogrammer acp"))
        rc, out, _ = self.run_tool("verify", "--baseline", str(baseline))
        self.assertEqual(rc, 1)
        self.assertIn("omp acp", out)

    def test_verify_reports_leftover_paths(self):
        self.write("OmpOS.txt", "plain\n")
        self.commit()
        rc, out, _ = self.run_tool("verify")
        self.assertEqual(rc, 1)
        self.assertIn("1 paths still carry a brand token", out)


if __name__ == "__main__":
    unittest.main()
