# Skill Registry

**Delegator use only.** Any agent that launches sub-agents reads this registry to resolve compact rules, then injects them directly into sub-agent prompts. Sub-agents do NOT read this registry or individual SKILL.md files.

## User Skills

| Trigger | Skill | Path |
|---------|-------|------|
| When creating a pull request, opening a PR, or preparing changes for review. | branch-pr | `C:/Users/edwin.campo/.codex/skills/branch-pr/SKILL.md` |
| When writing Go tests, using teatest, or adding test coverage. | go-testing | `C:/Users/edwin.campo/.codex/skills/go-testing/SKILL.md` |
| When creating a GitHub issue, reporting a bug, or requesting a feature. | issue-creation | `C:/Users/edwin.campo/.codex/skills/issue-creation/SKILL.md` |
| When user says "judgment day", "judgment-day", "review adversarial", "dual review", "doble review", "juzgar", "que lo juzguen". | judgment-day | `C:/Users/edwin.campo/.codex/skills/judgment-day/SKILL.md` |
| When user asks to create a new skill, add agent instructions, or document patterns for AI. | skill-creator | `C:/Users/edwin.campo/.codex/skills/skill-creator/SKILL.md` |

## Compact Rules

Pre-digested rules per skill. Delegators copy matching blocks into sub-agent prompts as `## Project Standards (auto-resolved)`.

### branch-pr
- Toda PR DEBE enlazar un issue aprobado; sin `status:approved` la PR no corresponde.
- Usá exactamente UNA label `type:*`.
- La rama DEBE seguir `type/description` en minúsculas.
- Commits con conventional commits; no inventes formatos.
- Antes de abrir PR, verificá checks automáticos y shellcheck en scripts tocados.
- El body debe incluir `Closes #N`, resumen corto, tabla de cambios y plan de pruebas.

### go-testing
- Preferí tests table-driven con `t.Run()` para cubrir casos múltiples.
- En Bubbletea, testeá transiciones de estado del model directamente.
- Para flujos interactivos, usá `teatest` y esperá el estado final explícitamente.
- Golden tests solo para salidas estables y legibles.
- Mantené tests chicos, deterministas y con nombres que describan conducta.

### issue-creation
- No existen issues en blanco: SIEMPRE usá template.
- Antes de crear issue, buscá duplicados.
- Todo issue nace con `status:needs-review`; PR recién cuando tenga `status:approved`.
- Preguntas van a Discussions, no a Issues.
- Completá pasos de reproducción, esperado, actual y contexto mínimo útil.

### judgment-day
- Resolvé skills/proyecto antes de lanzar jueces; inyectá reglas compactas a todos.
- Lanzá DOS jueces ciegos en paralelo; nunca revisión secuencial.
- Sintetizá hallazgos en confirmados, sospechosos y contradicciones.
- `WARNING real` se corrige; `WARNING theoretical` se reporta como info.
- Re-juzgá después de fixes solo cuando haga falta; no entres en loops eternos.
- Si falta skill registry, avisá y seguí con revisión genérica.

### skill-creator
- Creá skill solo para patrones repetidos o convenciones no obvias.
- Estructura mínima: `skills/{name}/SKILL.md`; `assets/` y `references/` son opcionales.
- Frontmatter obligatorio: `name`, `description` con trigger, `license`, `metadata.author`, `metadata.version`.
- `references/` apunta a archivos locales, no URLs web.
- El contenido debe priorizar reglas críticas, ejemplos mínimos y comandos útiles.
- Nombrá skills en minúsculas con guiones.

## Project Conventions

| File | Path | Notes |
|------|------|-------|
| `AGENTS.md` | `C:/dev/Walos-app/AGENTS.md` | Índice principal de reglas del proyecto |

Read the convention files listed above for project-specific patterns and rules. All referenced paths have been extracted — no need to read index files to discover more.
