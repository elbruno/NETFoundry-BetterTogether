# Contributing

Corrections, clearer instructions, and improvements to episode demos are welcome. Open an issue for a bug or proposal, or submit a focused pull request explaining what changed and how you checked it.

## Where content belongs

```text
episodes/
  NN-YYYY-MM-DD/
    README.md
    demos/
      NN-demo-name/
        README.md
        (demo source)
```

Use the existing episode folder for related contributions. Keep published folder names stable. Create a demo folder only when it contains actual demo content; add episode-local assets or slides only when needed.

The root README is the series index. Each episode README links to its event, recording, demos, and resources. Add only confirmed links and information, and update the index when adding an episode or recording.

Keep promotional drafts, social-media copy, promotional artwork, agendas, and rehearsal notes out of this repository. They belong in the private planning repository. Episode assets here must support audience-facing demos or learning resources.

## Demo checklist

- Keep the demo independently runnable and document tested SDK/package versions and prerequisites.
- Provide safe configuration instructions, exact setup/run commands, and the expected result in its README.
- Include one episode-level PowerShell setup script that configures every
  runnable demo's required settings, with a non-mutating validation mode,
  offline tests, clear documentation of changes, and cleanup commands. Keep
  secret values out of logs and command-line arguments.
- Explain required cloud resources, possible charges, and cleanup. Do not delete shared or pre-existing resources.
- Never commit credentials, real configuration secrets, private messages, or production access links. Use environment variables or .NET user secrets.
- Run the documented steps and relevant tests before opening a pull request. For documentation changes, check links and accuracy.
- Preserve earlier episodes' behavior. Explain compatibility fixes instead of silently upgrading old demos.
- For live teaching, use a short `Program.cs`: create the client, define the prompt, call the model, and print the results. Add a concise Goal/A/B/C header and useful conceptual comments. Avoid CLI options, helper frameworks, custom exception handling, and defensive scaffolding in introductory samples. Keep necessary simulation/rendering code separate in larger demos.

Maintainers may tag published episode snapshots as `episode-NN`. Existing broadcast tags remain unchanged.
