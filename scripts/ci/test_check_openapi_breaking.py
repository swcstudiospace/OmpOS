import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location("cob", pathlib.Path(__file__).with_name("check-openapi-breaking.py"))
cob = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cob)


def doc(paths):
    return {"openapi": "3.0.1", "paths": paths, "components": {"schemas": {}}}


def op(params=None, responses=None, body=None):
    o = {"responses": responses or {"200": {"description": "ok"}}}
    if params is not None:
        o["parameters"] = params
    if body is not None:
        o["requestBody"] = {"content": {"application/json": {"schema": body}}}
    return o


def obj(props, required=()):
    return {"type": "object", "properties": props, "required": list(required)}


class DiffTests(unittest.TestCase):
    def test_identical_is_clean(self):
        d = doc({"/a": {"get": op()}})
        self.assertEqual(cob.diff(d, d), [])

    def test_added_path_and_optional_parameter_are_not_breaking(self):
        old = doc({"/a": {"get": op()}})
        new = doc({"/a": {"get": op([{"name": "q", "in": "query", "schema": {"type": "string"}}])}, "/b": {"get": op()}})
        self.assertEqual(cob.diff(old, new), [])

    def test_removed_path_and_method(self):
        old = doc({"/a": {"get": op(), "post": op()}, "/b": {"get": op()}})
        new = doc({"/a": {"get": op()}})
        self.assertEqual(sorted(cob.diff(old, new)), ["/b: path removed", "POST /a: method removed"])

    def test_parameter_removed_and_newly_required(self):
        p = {"name": "q", "in": "query", "schema": {"type": "string"}}
        old = doc({"/a": {"get": op([p])}})
        self.assertIn("GET /a: parameter 'q' (query) removed", cob.diff(old, doc({"/a": {"get": op([])}})))
        new = doc({"/a": {"get": op([dict(p, required=True)])}})
        self.assertIn("GET /a: parameter 'q' (query) is newly required", cob.diff(old, new))
        added = doc({"/a": {"get": op([p, {"name": "r", "in": "query", "required": True, "schema": {"type": "string"}}])}})
        self.assertIn("GET /a: new required parameter 'r' (query)", cob.diff(old, added))

    def test_response_code_removed(self):
        old = doc({"/a": {"get": op(responses={"200": {}, "404": {}})}})
        new = doc({"/a": {"get": op(responses={"200": {}})}})
        self.assertEqual(cob.diff(old, new), ["GET /a: response 404 removed"])

    def test_response_property_removed_and_type_changed(self):
        resp = lambda props: {"200": {"content": {"application/json": {"schema": obj(props)}}}}
        old = doc({"/a": {"get": op(responses=resp({"x": {"type": "string"}, "y": {"type": "integer"}}))}})
        new = doc({"/a": {"get": op(responses=resp({"y": {"type": "string"}}))}})
        out = cob.diff(old, new)
        self.assertIn("GET /a response 200: property 'x' removed", out)
        self.assertIn("GET /a response 200.y: type changed from integer to string", out)

    def test_request_property_newly_required_and_ref_resolution(self):
        old = doc({"/a": {"post": op(body={"$ref": "#/components/schemas/B"})}})
        old["components"]["schemas"]["B"] = obj({"n": {"type": "string"}})
        new = doc({"/a": {"post": op(body={"$ref": "#/components/schemas/B"})}})
        new["components"]["schemas"]["B"] = obj({"n": {"type": "string"}}, required=["n"])
        self.assertEqual(cob.diff(old, new), ["POST /a request: property 'n' is newly required"])

    def test_new_optional_request_property_is_fine(self):
        old = doc({"/a": {"post": op(body=obj({"n": {"type": "string"}}))}})
        new = doc({"/a": {"post": op(body=obj({"n": {"type": "string"}, "m": {"type": "string"}}))}})
        self.assertEqual(cob.diff(old, new), [])

    def test_a_self_referencing_schema_ends_and_reports_a_removed_property_once(self):
        node = lambda props: {"type": "object", "properties": dict(props, child={"$ref": "#/components/schemas/Node"})}
        resp = {"200": {"content": {"application/json": {"schema": {"$ref": "#/components/schemas/Node"}}}}}
        old = doc({"/a": {"get": op(responses=resp)}})
        old["components"]["schemas"]["Node"] = node({"name": {"type": "string"}})
        unchanged = doc({"/a": {"get": op(responses=resp)}})
        unchanged["components"]["schemas"]["Node"] = node({"name": {"type": "string"}})
        self.assertEqual(cob.diff(old, unchanged), [])
        changed = doc({"/a": {"get": op(responses=resp)}})
        changed["components"]["schemas"]["Node"] = node({})
        self.assertEqual(cob.diff(old, changed), ["GET /a response 200: property 'name' removed"])

    def test_schemas_that_refer_to_each_other_end(self):
        a = lambda extra: {"type": "object", "properties": dict(extra, b={"$ref": "#/components/schemas/B"})}
        b = {"type": "object", "properties": {"a": {"$ref": "#/components/schemas/A"}}}
        resp = {"200": {"content": {"application/json": {"schema": {"$ref": "#/components/schemas/A"}}}}}
        old = doc({"/a": {"get": op(responses=resp)}})
        old["components"]["schemas"].update({"A": a({"id": {"type": "integer"}}), "B": b})
        new = doc({"/a": {"get": op(responses=resp)}})
        new["components"]["schemas"].update({"A": a({"id": {"type": "string"}}), "B": b})
        self.assertEqual(cob.diff(old, new), ["GET /a response 200.id: type changed from integer to string"])

    def test_a_removed_response_body_is_breaking_and_an_added_one_is_not(self):
        with_body = {"200": {"content": {"application/json": {"schema": obj({"x": {"type": "string"}})}}}}
        without_body = {"200": {"description": "ok"}}
        self.assertEqual(
            cob.diff(doc({"/a": {"get": op(responses=with_body)}}), doc({"/a": {"get": op(responses=without_body)}})),
            ["GET /a response 200: response body removed"],
        )
        self.assertEqual(cob.diff(doc({"/a": {"get": op(responses=without_body)}}), doc({"/a": {"get": op(responses=with_body)}})), [])

    def test_allowance_from_env_or_commit_message(self):
        self.assertEqual(cob.allowance({"ALLOW_BREAKING_API": "v2"}, ""), "v2")
        self.assertEqual(cob.allowance({}, "feat: x\n\nBREAKING-API: drop legacy route\n"), "drop legacy route")
        self.assertIsNone(cob.allowance({}, "feat: x\nBREAKING-API:   \n"))
        self.assertIsNone(cob.allowance({}, "feat: x"))


if __name__ == "__main__":
    unittest.main()
