# Getting help with OmpOS

## Where to ask

| You want to | Go to |
| --- | --- |
| Learn how OmpOS works, install it, or configure it | the documentation: the [README](README.md) and the guides under [docs/](docs/) (the GitBook site is linked from the repository's About box once it is published) |
| Ask a how-to question, share an idea, or show what you built | [GitHub Discussions](https://github.com/swcstudiospace/OmpOS/discussions) |
| Report a bug | the [bug report form](https://github.com/swcstudiospace/OmpOS/issues/new?template=bug_report.yml) |
| Propose a feature | the [feature request form](https://github.com/swcstudiospace/OmpOS/issues/new?template=feature_request.yml), after a search of existing issues and discussions |
| Fix or improve the documentation | the [documentation form](https://github.com/swcstudiospace/OmpOS/issues/new?template=documentation.yml), or a pull request (see [CONTRIBUTING.md](CONTRIBUTING.md)) |
| Report a vulnerability | **privately**, as described in [SECURITY.md](SECURITY.md); never in a public issue |
| Report a conduct problem | as described in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |

## Before you open an issue

- Search the open and closed issues and the discussions.
- Check [docs/open-questions.md](docs/open-questions.md) and [docs/handoff.md](docs/handoff.md): known limits and blocked items are recorded there.
- Include the OmpOS version or commit, the head (desktop, web, Android, API), your operating system, the `omp` version (`omp --version`) and the exact steps.
- Redact secrets. Never paste API keys, tokens or `.env` contents.

## What is not supported

- Support is best effort from volunteers and the maintainers: there is no service-level agreement and no guaranteed response time.
- Problems in the `omp` engine itself, in the sibling plugin repositories (`claude-ultrathink`, `agent-swarm`, `agent-substrate`), or in a model provider's service belong in those projects or with that vendor.
- OmpOS does not provide, resell or proxy model inference or accounts. Provider keys are yours; billing and quota questions go to the provider.
- Releases before 0.15.x, forks, and modified builds.
- Hosted deployments run by someone else, and Windows, macOS, iOS and Android runs: the maintainers have not run OmpOS on those platforms (see the open questions for what is verified).
- Consulting, custom development and private support.
